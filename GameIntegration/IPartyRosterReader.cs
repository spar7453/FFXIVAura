namespace FFXIVAura;

internal readonly record struct PartyListHeader(
    int Length,
    bool IsAlliance,
    int PartyId)
{
    private const int PartyMemberSlotCount = 8;

    public int PartySlotCount => Math.Clamp(this.Length, 0, PartyMemberSlotCount);
}

internal readonly record struct PartyRosterLocalIdentity(
    uint EntityId,
    ulong ContentId);

internal readonly record struct PartyCooldownCrossRealmMemberRead(
    PartyCooldownMemberSnapshot Member,
    int ContainerGroupIndex);

internal readonly record struct PartyCooldownFlatAllianceMemberRead(
    PartyCooldownMemberSnapshot Member,
    int SlotIndex);

internal readonly record struct PartyCooldownRosterSourceRead(
    PartyListHeader PartyListHeader,
    PartyCooldownCrossRealmHeader CrossRealmHeader,
    IReadOnlyList<PartyCooldownCrossRealmMemberRead> CrossRealmMembers,
    PartyRosterLocalIdentity LocalIdentity);

internal interface IPartyRosterReader
{
    PartyListHeader ReadPartyListHeader();

    PartyCooldownRosterSourceRead ReadSource();

    IReadOnlyList<PartyCooldownMemberSnapshot> ReadPartySlotMembers(int partySlotCount);

    IReadOnlyList<PartyCooldownFlatAllianceMemberRead> ReadFlatAllianceMembers();

    PartyCooldownMemberSnapshot? ReadLocalPlayer();

    PartyCooldownHudRosterOrder ReadHudRosterOrder(bool isAlliance);

    void Reset();
}
