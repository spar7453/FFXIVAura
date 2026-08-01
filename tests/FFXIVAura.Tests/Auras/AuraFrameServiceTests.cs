using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraFrameServiceTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraFrameService reads a character once per frame", ReadsCharacterOncePerFrame),
        ("AuraFrameService aggregates party and own aura counts", AggregatesPartyAndOwnAuraCounts),
    ];

    private static void ReadsCharacterOncePerFrame()
    {
        var statusRuntime = new StubStatusSnapshotRuntime
        {
            LocalPlayerBatch = Batch(1, new StatusSnapshot(42, 1, 7, 12f)),
        };
        var service = CreateService(statusRuntime, new StubPartyRosterReader(0));
        var playerContext = CreatePlayerContext();

        service.BeginFrame(playerContext);
        var first = service.FindPlayer(42);
        var second = service.FindPlayer(42);

        True(first is not null && second is not null, "player aura should be available");
        var firstValue = first.GetValueOrDefault();
        Near(12f, firstValue.Remaining);
        Equal((ushort)7, firstValue.Param);
        Equal(1, statusRuntime.LocalPlayerReadCount);

        service.BeginFrame(playerContext);
        _ = service.FindPlayer(42);
        Equal(2, statusRuntime.LocalPlayerReadCount);
    }

    private static void AggregatesPartyAndOwnAuraCounts()
    {
        var statusRuntime = new StubStatusSnapshotRuntime();
        statusRuntime.PartyBatches[0] = Batch(10, new StatusSnapshot(42, 1, 0, 20f));
        statusRuntime.PartyBatches[1] = Batch(20, new StatusSnapshot(42, 99, 0, 30f));
        var service = CreateService(statusRuntime, new StubPartyRosterReader(2));

        service.BeginFrame(CreatePlayerContext());
        var all = service.FindParty(42, ownOnly: false);
        var own = service.FindParty(42, ownOnly: true);
        var key = AuraStatusGroupKey.Create("Test Aura", 100, 1);

        True(all is not null, "party aura should be aggregated");
        var allValue = all.GetValueOrDefault();
        Equal(2, allValue.Count);
        Equal(1, allValue.OwnCount);
        Near(30f, allValue.Remaining);
        True(own is not null, "own-only party aura should be aggregated");
        var ownValue = own.GetValueOrDefault();
        Equal(1, ownValue.Count);
        Equal(1, ownValue.OwnCount);
        True(service.TryGetPartyGroup(key, ownOnly: false, out var group), "group aggregate should be available");
        Equal(2, group.Count);
        Equal(1, group.OwnCount);
        Equal(2, statusRuntime.PartyMemberReadCount);
    }

    private static AuraFrameService CreateService(
        IStatusSnapshotRuntime statusRuntime,
        IPartyRosterReader partyRosterReader)
        => new(
            statusRuntime,
            partyRosterReader,
            new PerformanceProfiler(),
            () => true,
            statusId => statusId == 42
                ? new AuraStatusDefinition("Test Aura", 100, 1)
                : new AuraStatusDefinition($"Status {statusId}", 0, 0));

    private static StatusSnapshotBatch Batch(uint ownerEntityId, params StatusSnapshot[] statuses)
        => new(true, ownerEntityId, StatusSnapshotOrigin.Live, statuses);

    private static PlayerFrameContext CreatePlayerContext()
        => new(
            IsLoggedIn: true,
            IsPlayerLoaded: true,
            Job: "VPR",
            Level: 100,
            EffectiveLevel: 100,
            IsLevelSynced: false,
            IsInCombat: true,
            BetweenAreas: false,
            BetweenAreas51: false,
            IsMounted: false,
            HasLocalPlayer: true,
            LocalPlayerEntityId: 1,
            HasTarget: false,
            TargetEntityId: 0,
            HasSoftTarget: false,
            SoftTargetEntityId: 0);

    private sealed class StubStatusSnapshotRuntime : IStatusSnapshotRuntime
    {
        private static readonly StatusSnapshotBatch Empty =
            new(false, 0, StatusSnapshotOrigin.None, Array.Empty<StatusSnapshot>());

        public Dictionary<int, StatusSnapshotBatch> PartyBatches { get; } = new();

        public StatusSnapshotBatch LocalPlayerBatch { get; init; } = Empty;

        public StatusSnapshotBatch TargetBatch { get; init; } = Empty;

        public int LocalPlayerReadCount { get; private set; }

        public int PartyMemberReadCount { get; private set; }

        public StatusSnapshotBatch ReadLocalPlayer(string scope)
        {
            this.LocalPlayerReadCount++;
            return this.LocalPlayerBatch;
        }

        public StatusSnapshotBatch ReadTarget(string scope)
            => this.TargetBatch;

        public StatusSnapshotBatch ReadPartyMember(int index, string scope)
        {
            this.PartyMemberReadCount++;
            return this.PartyBatches.GetValueOrDefault(index, Empty);
        }

        public StatusSnapshotBatch ReadAllianceMember(int index, string scope)
            => Empty;

        public void VisitBattleCharacters(string scope, Action<StatusSnapshotBatch> visitor)
        {
        }

        public uint GetOwnerEntityId(uint entityId)
            => 0;

        public StatusSnapshotRuntimeDiagnostics CreateDiagnostics()
            => default;

        public void BeginFrame()
        {
        }

        public void ResetRuntimeState()
        {
        }
    }

    private sealed class StubPartyRosterReader(int partyMemberCount) : IPartyRosterReader
    {
        public PartyListHeader ReadPartyListHeader()
            => new(partyMemberCount, false, 0);

        public PartyCooldownRosterSourceRead ReadSource()
            => throw new NotSupportedException();

        public IReadOnlyList<PartyCooldownMemberSnapshot> ReadPartySlotMembers(int partySlotCount)
            => throw new NotSupportedException();

        public IReadOnlyList<PartyCooldownFlatAllianceMemberRead> ReadFlatAllianceMembers()
            => throw new NotSupportedException();

        public PartyCooldownMemberSnapshot? ReadLocalPlayer()
            => throw new NotSupportedException();

        public PartyCooldownHudRosterOrder ReadHudRosterOrder(bool isAlliance)
            => throw new NotSupportedException();

        public void Reset()
        {
        }
    }
}
