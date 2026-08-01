namespace FFXIVAura;

public sealed partial class Plugin
{
    private PartyCooldownLayoutEditMode GetPartyCooldownLayoutMode(IReadOnlyList<PartyCooldownMemberRow> rows)
        => this.ResolvePartyCooldownLayoutPreview(
            this.GetAutomaticPartyCooldownLayoutMode(
                PartyCooldownBoardLayoutCalculator.CountAllianceGroups(rows)));

    private PartyCooldownLayoutEditMode GetPartyCooldownLayoutMode()
    {
        var snapshot = this.GetPartyCooldownFrameSnapshot();
        var groupCount = snapshot.DisplayMembers
            .Select(member => member.AllianceGroup)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.Ordinal)
            .Count();
        return this.ResolvePartyCooldownLayoutPreview(this.GetAutomaticPartyCooldownLayoutMode(groupCount));
    }

    private PartyCooldownLayoutEditMode ResolvePartyCooldownLayoutPreview(PartyCooldownLayoutEditMode automaticMode)
        => PartyCooldownLayoutModeTracker.ResolvePreview(
            this.configVisible,
            this.config.PartyCooldownLayoutEditMode,
            automaticMode);

    private PartyCooldownLayoutEditMode GetAutomaticPartyCooldownLayoutMode(int observedGroupCount)
    {
        if (!this.playerFrameContext.IsLoggedIn)
        {
            this.partyCooldownLayoutModeTracker.Reset();
            return PartyCooldownLayoutEditMode.EightPlayer;
        }

        var snapshot = this.GetPartyCooldownFrameSnapshot();
        var diagnostics = snapshot.RosterDiagnostics;
        var allianceObserved = diagnostics.HasAllianceSource
                               || diagnostics.Source == PartyCooldownRosterSource.Alliance
                               || diagnostics.AlliancePartyCount >= 2
                               || observedGroupCount >= 2;
        var partyMemberCount = Math.Max(
            diagnostics.PartyListLength,
            Math.Max(diagnostics.MemberCount, snapshot.Members.Count));
        return this.partyCooldownLayoutModeTracker.Resolve(
            allianceObserved,
            partyMemberCount,
            snapshot.TimestampUtc);
    }
}
