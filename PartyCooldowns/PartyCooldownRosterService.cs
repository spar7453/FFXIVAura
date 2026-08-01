namespace FFXIVAura;

internal readonly record struct PartyCooldownRosterReadResult(
    IReadOnlyList<PartyCooldownMemberSnapshot> Members,
    IReadOnlyList<PartyCooldownMemberSnapshot> DisplayMembers,
    PartyCooldownRosterDiagnostics Diagnostics);

internal sealed class PartyCooldownRosterService
{
    private const int AllianceGroupCount = 3;
    private const int AllianceGroupMemberSlotCount = 8;

    private readonly record struct RosterMemberCollection(
        PartyCooldownRosterMemberCollector Collector,
        PartyCooldownRosterReadCounts Counts);

    private readonly IPartyRosterReader reader;
    private readonly TimeSpan cacheDuration;
    private PartyCooldownRosterReadResult? cache;
    private DateTime cacheBuiltAtUtc = DateTime.MinValue;

    public PartyCooldownRosterService(IPartyRosterReader reader, TimeSpan cacheDuration)
    {
        this.reader = reader;
        this.cacheDuration = cacheDuration < TimeSpan.Zero ? TimeSpan.Zero : cacheDuration;
    }

    public long CacheHitCount { get; private set; }

    public long CacheMissCount { get; private set; }

    public PartyCooldownRosterReadResult GetRoster(bool forceRefresh = false)
    {
        FrameThreadGuard.AssertUiThread();
        return this.GetRoster(forceRefresh, DateTime.UtcNow);
    }

    internal PartyCooldownRosterReadResult GetRoster(bool forceRefresh, DateTime nowUtc)
    {
        if (this.TryGetCached(forceRefresh, nowUtc, out var cached))
            return cached;

        this.CacheMissCount++;
        var source = this.reader.ReadSource();
        var collection = this.CollectMembers(source);
        var usedSoloFallback = this.TryAddSoloMember(collection.Collector.Members);
        var result = this.CreateResult(source, collection, usedSoloFallback);
        this.cache = result;
        this.cacheBuiltAtUtc = nowUtc;
        return result;
    }

    public void ResetRuntimeState()
    {
        this.reader.Reset();
        this.cache = null;
        this.cacheBuiltAtUtc = DateTime.MinValue;
    }

    private bool TryGetCached(
        bool forceRefresh,
        DateTime nowUtc,
        out PartyCooldownRosterReadResult result)
    {
        if (this.cache is not { } cached
            || !PartyCooldownRosterCachePolicy.CanReuse(
                forceRefresh,
                this.cacheBuiltAtUtc,
                nowUtc,
                this.cacheDuration))
        {
            result = default;
            return false;
        }

        this.CacheHitCount++;
        result = cached;
        return true;
    }

    private RosterMemberCollection CollectMembers(PartyCooldownRosterSourceRead source)
    {
        var header = source.PartyListHeader;
        var capacity = Math.Max(
            header.IsAlliance
                ? AllianceGroupCount * AllianceGroupMemberSlotCount
                : header.Length,
            1);
        var collector = new PartyCooldownRosterMemberCollector(capacity);
        if (header.IsAlliance)
            return this.CollectAllianceMembers(source, collector);

        var partySlotMemberCount = this.AddPartySlotMembers(
            collector,
            string.Empty,
            header.PartySlotCount);
        return new RosterMemberCollection(
            collector,
            new PartyCooldownRosterReadCounts(partySlotMemberCount, 0, 0, false));
    }

    private RosterMemberCollection CollectAllianceMembers(
        PartyCooldownRosterSourceRead source,
        PartyCooldownRosterMemberCollector collector)
    {
        var groupedAllianceMemberCount = AddCrossRealmMembers(
            collector,
            source.CrossRealmMembers,
            source.CrossRealmHeader);
        if (groupedAllianceMemberCount >= AllianceGroupCount * AllianceGroupMemberSlotCount)
        {
            return new RosterMemberCollection(
                collector,
                new PartyCooldownRosterReadCounts(0, groupedAllianceMemberCount, 0, false));
        }

        var localAllianceGroupIndex = source.CrossRealmHeader.LocalGroupIndex;
        var partySlotMemberCount = this.AddPartySlotMembers(
            collector,
            PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: true, localAllianceGroupIndex),
            source.PartyListHeader.PartySlotCount);
        var flatAllianceMemberCount = this.AddFlatAllianceMembers(
            collector,
            localAllianceGroupIndex);
        return new RosterMemberCollection(
            collector,
            new PartyCooldownRosterReadCounts(
                partySlotMemberCount,
                groupedAllianceMemberCount,
                flatAllianceMemberCount,
                partySlotMemberCount > 0 || flatAllianceMemberCount > 0));
    }

    private int AddPartySlotMembers(
        PartyCooldownRosterMemberCollector collector,
        string allianceGroup,
        int partySlotCount)
    {
        var added = 0;
        foreach (var member in this.reader.ReadPartySlotMembers(partySlotCount))
        {
            if (collector.AddPartyList(member, allianceGroup))
                added++;
        }

        return added;
    }

    private static int AddCrossRealmMembers(
        PartyCooldownRosterMemberCollector collector,
        IReadOnlyList<PartyCooldownCrossRealmMemberRead> sourceMembers,
        PartyCooldownCrossRealmHeader header)
    {
        var added = 0;
        foreach (var sourceMember in sourceMembers)
        {
            var label = PartyCooldownAllianceGroups.ContainerGroupLabel(
                sourceMember.ContainerGroupIndex,
                header.RawLocalGroupIndex,
                header.LocalGroupIndex);
            if (collector.AddCrossRealm(sourceMember.Member, label))
                added++;
        }

        return added;
    }

    private int AddFlatAllianceMembers(
        PartyCooldownRosterMemberCollector collector,
        int localGroupIndex)
    {
        var added = 0;
        foreach (var sourceMember in this.reader.ReadFlatAllianceMembers())
        {
            var label = PartyCooldownAllianceGroups.AllianceSlotLabel(
                sourceMember.SlotIndex,
                localGroupIndex);
            if (collector.AddPartyList(sourceMember.Member, label))
                added++;
        }

        return added;
    }

    private bool TryAddSoloMember(List<PartyCooldownMemberSnapshot> members)
    {
        if (members.Count != 0 || this.reader.ReadLocalPlayer() is not { } player)
            return false;

        members.Add(player);
        return true;
    }

    private PartyCooldownRosterReadResult CreateResult(
        PartyCooldownRosterSourceRead source,
        RosterMemberCollection collection,
        bool usedSoloFallback)
    {
        var members = collection.Collector.Members;
        var hasAllianceSource = source.PartyListHeader.IsAlliance;
        var hudRosterOrder = this.reader.ReadHudRosterOrder(hasAllianceSource);
        PartyCooldownMemberOrdering.ApplyInGameOrder(members, hudRosterOrder.EntityIds);
        var displayMembers = PartyCooldownRoster.CreateDisplayMembers(
            members,
            source.LocalIdentity.EntityId,
            source.LocalIdentity.ContentId,
            excludeLocalPlayer: true);
        var rosterSource = PartyCooldownRosterReadClassifier.ResolveSource(
            hasAllianceSource,
            source.PartyListHeader.Length,
            collection.Counts);
        var readMode = PartyCooldownRosterReadClassifier.ResolveReadMode(
            collection.Counts,
            usedSoloFallback);
        var allianceMemberCount = CountAllianceMembers(members);
        var header = source.CrossRealmHeader;
        var diagnostics = PartyCooldownRoster.CreateDiagnostics(
            rosterSource,
            readMode,
            source.PartyListHeader.Length,
            members,
            displayMembers,
            source.LocalIdentity.EntityId,
            source.LocalIdentity.ContentId,
            hasAllianceSource ? AllianceGroupCount : 0,
            hasAllianceSource ? allianceMemberCount : 0,
            hasAllianceSource,
            collection.Counts.UsedFlatAllianceFallback,
            header.LocalGroupIndex,
            header.RawLocalGroupIndex,
            header.HudLocalGroupIndex,
            header.ObservedHudGroupIndex,
            header.UsedRetainedHudGroup,
            header.GroupCount,
            hudRosterOrder.AllianceMemberCount);
        return new PartyCooldownRosterReadResult(members, displayMembers, diagnostics);
    }

    private static int CountAllianceMembers(IReadOnlyList<PartyCooldownMemberSnapshot> members)
    {
        var count = 0;
        foreach (var member in members)
        {
            for (var group = 0; group < AllianceGroupCount; group++)
            {
                if (!string.Equals(
                        member.AllianceGroup,
                        PartyCooldownAllianceGroups.GroupLabel(group),
                        StringComparison.Ordinal))
                {
                    continue;
                }

                count++;
                break;
            }
        }

        return count;
    }
}
