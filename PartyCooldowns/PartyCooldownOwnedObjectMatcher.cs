namespace FFXIVAura;

internal interface IPartyCooldownOwnedObjectReader
{
    void AddMatchingOwnerEntityIds(string normalizedSourceName, ISet<uint> output);
}

internal sealed class DalamudPartyCooldownOwnedObjectReader(IObjectTable objectTable)
    : IPartyCooldownOwnedObjectReader
{
    public void AddMatchingOwnerEntityIds(string normalizedSourceName, ISet<uint> output)
    {
        foreach (var gameObject in objectTable)
        {
            var ownerEntityId = gameObject?.OwnerId ?? 0;
            if (!PartyCooldownOwnerResolver.IsValidEntityId(ownerEntityId))
                continue;

            var objectName = PartyCooldownLogMatcher.NormalizeActorName(gameObject!.Name.ToString());
            if (string.Equals(normalizedSourceName, objectName, StringComparison.Ordinal))
                output.Add(ownerEntityId);
        }
    }
}

internal sealed class PartyCooldownOwnedObjectMatcher
{
    private readonly IPartyCooldownOwnedObjectReader reader;
    private readonly Action<string> noteDiagnostic;
    private readonly HashSet<uint> matchingOwnerEntityIds = [];
    private readonly HashSet<uint> partyMemberEntityIds = [];

    public PartyCooldownOwnedObjectMatcher(
        IPartyCooldownOwnedObjectReader reader,
        Action<string> noteDiagnostic)
    {
        this.reader = reader;
        this.noteDiagnostic = noteDiagnostic;
    }

    public bool TryFindMember(
        string sourceName,
        uint localPlayerEntityId,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail,
        out PartyCooldownIgnoredLogReason ignoredReason)
    {
        var normalizedSourceName = PartyCooldownLogMatcher.NormalizeActorName(sourceName);
        if (normalizedSourceName.Length == 0)
        {
            member = default;
            detail = "소환수/객체 이름이 비어 있습니다.";
            ignoredReason = PartyCooldownIgnoredLogReason.OwnerNotFound;
            return false;
        }

        if (!this.TryResolveOwner(
                normalizedSourceName,
                localPlayerEntityId,
                displayMembers,
                out var ownerMatch,
                out detail))
        {
            member = default;
            ignoredReason = PartyCooldownIgnoredLogReason.OwnerNotFound;
            return false;
        }

        return TryApplyOwnerMatch(
            ownerMatch,
            displayMembers,
            out member,
            out detail,
            out ignoredReason);
    }

    public void ResetRuntimeState()
    {
        this.matchingOwnerEntityIds.Clear();
        this.partyMemberEntityIds.Clear();
    }

    private bool TryResolveOwner(
        string normalizedSourceName,
        uint localPlayerEntityId,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownOwnedObjectOwnerMatch ownerMatch,
        out string detail)
    {
        this.matchingOwnerEntityIds.Clear();
        this.partyMemberEntityIds.Clear();
        try
        {
            AddMemberEntityIds(displayMembers, this.partyMemberEntityIds);
            this.reader.AddMatchingOwnerEntityIds(normalizedSourceName, this.matchingOwnerEntityIds);
            ownerMatch = PartyCooldownOwnedObjectOwnerResolver.Resolve(
                localPlayerEntityId,
                this.matchingOwnerEntityIds,
                this.partyMemberEntityIds);
            detail = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            ownerMatch = default;
            detail = $"소환수/객체 목록을 읽지 못했습니다: {ex.GetType().Name}";
            this.noteDiagnostic($"partyCooldownOwnedObjectReadFailed:{ex.GetType().Name}");
            return false;
        }
        finally
        {
            this.matchingOwnerEntityIds.Clear();
            this.partyMemberEntityIds.Clear();
        }
    }

    private static void AddMemberEntityIds(
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        ISet<uint> output)
    {
        foreach (var candidate in displayMembers)
        {
            if (PartyCooldownOwnerResolver.IsValidEntityId(candidate.EntityId))
                output.Add(candidate.EntityId);
        }
    }

    private static bool TryApplyOwnerMatch(
        PartyCooldownOwnedObjectOwnerMatch ownerMatch,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail,
        out PartyCooldownIgnoredLogReason ignoredReason)
    {
        if (ownerMatch.Kind == PartyCooldownOwnedObjectOwnerMatchKind.LocalPlayer)
        {
            member = default;
            ignoredReason = PartyCooldownIgnoredLogReason.LocalPlayerExcluded;
            detail = "로컬 플레이어의 소환수/객체 행동이라 제외했습니다.";
            return false;
        }

        if (ownerMatch.Kind == PartyCooldownOwnedObjectOwnerMatchKind.PartyMember
            && TryFindMemberByEntityId(displayMembers, ownerMatch.OwnerEntityId, out member))
        {
            ignoredReason = PartyCooldownIgnoredLogReason.None;
            detail = "소환수/객체 소유자 매칭";
            return true;
        }

        member = default;
        if (ownerMatch.Kind == PartyCooldownOwnedObjectOwnerMatchKind.Ambiguous)
        {
            ignoredReason = PartyCooldownIgnoredLogReason.Ambiguous;
            detail = "같은 이름의 소환수/객체 소유 파티원이 여러 명이라 추적하지 않았습니다.";
            return false;
        }

        ignoredReason = PartyCooldownIgnoredLogReason.OwnerNotFound;
        detail = string.Empty;
        return false;
    }

    private static bool TryFindMemberByEntityId(
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        uint entityId,
        out PartyCooldownMemberSnapshot member)
    {
        foreach (var candidate in displayMembers)
        {
            if (candidate.EntityId == entityId)
            {
                member = candidate;
                return true;
            }
        }

        member = default;
        return false;
    }
}
