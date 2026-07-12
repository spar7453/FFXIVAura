namespace FFXIVAura;

internal sealed class PartyCooldownRosterMemberCollector
{
    private readonly HashSet<string> seenKeys = new(StringComparer.Ordinal);
    private readonly HashSet<uint> seenEntityIds = [];

    public PartyCooldownRosterMemberCollector(int capacity)
    {
        this.Members = new List<PartyCooldownMemberSnapshot>(Math.Max(capacity, 1));
    }

    public List<PartyCooldownMemberSnapshot> Members { get; }

    public bool AddCrossRealm(PartyCooldownMemberSnapshot member, string allianceGroup)
        => this.Add(member, allianceGroup, replaceEntitylessMatch: false);

    public bool AddPartyList(PartyCooldownMemberSnapshot member, string allianceGroup)
    {
        if (member.EntityId == 0)
            return false;

        return this.Add(member, allianceGroup, replaceEntitylessMatch: true);
    }

    private bool Add(
        PartyCooldownMemberSnapshot member,
        string allianceGroup,
        bool replaceEntitylessMatch)
    {
        if (member.EntityId != 0 && !this.seenEntityIds.Add(member.EntityId))
            return false;

        var snapshot = member with { AllianceGroup = allianceGroup };
        if (this.seenKeys.Add(snapshot.Key))
        {
            this.Members.Add(snapshot);
            return true;
        }

        if (replaceEntitylessMatch && this.TryReplaceEntitylessMatch(snapshot))
            return true;

        if (member.EntityId != 0)
            this.seenEntityIds.Remove(member.EntityId);
        return false;
    }

    private bool TryReplaceEntitylessMatch(PartyCooldownMemberSnapshot snapshot)
    {
        var existingIndex = this.Members.FindIndex(candidate =>
            string.Equals(candidate.Key, snapshot.Key, StringComparison.Ordinal));
        if (existingIndex < 0 || this.Members[existingIndex].EntityId != 0)
            return false;

        var existing = this.Members[existingIndex];
        this.Members[existingIndex] = snapshot with
        {
            AllianceGroup = string.IsNullOrWhiteSpace(existing.AllianceGroup)
                ? snapshot.AllianceGroup
                : existing.AllianceGroup,
        };
        return true;
    }
}
