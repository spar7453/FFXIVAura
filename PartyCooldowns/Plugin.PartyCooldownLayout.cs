namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private bool ShouldUsePartyCooldownAllianceLayout(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows)
        => this.ResolvePartyCooldownLayoutPreview(
            iconWindow,
            this.ShouldUseAutomaticPartyCooldownAllianceLayout(rows));

    private bool ShouldUsePartyCooldownAllianceLayout(IconWindowConfig iconWindow)
        => this.ResolvePartyCooldownLayoutPreview(
            iconWindow,
            this.ShouldUseAutomaticPartyCooldownAllianceLayout());

    private bool ShouldUseAutomaticPartyCooldownAllianceLayout(IReadOnlyList<PartyCooldownMemberRow> rows)
        => this.ShouldUsePartyCooldownAllianceLayout(GetPartyCooldownAllianceGroupCount(rows));

    private bool ShouldUseAutomaticPartyCooldownAllianceLayout()
    {
        var snapshot = this.GetPartyCooldownFrameSnapshot();
        var groupCount = snapshot.DisplayMembers
            .Select(member => member.AllianceGroup)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.Ordinal)
            .Count();
        return this.ShouldUsePartyCooldownAllianceLayout(groupCount);
    }

    private bool ResolvePartyCooldownLayoutPreview(IconWindowConfig iconWindow, bool automaticAllianceLayout)
    {
        bool? preview = this.partyCooldownAllianceLayoutPreviewByWindow.TryGetValue(iconWindow.Id, out var alliancePreview)
            ? alliancePreview
            : null;
        return PartyCooldownLayoutModeTracker.ResolvePreview(this.configVisible, preview, automaticAllianceLayout);
    }

    private void SetPartyCooldownLayoutPreview(IconWindowConfig iconWindow, bool useAllianceLayout)
        => this.partyCooldownAllianceLayoutPreviewByWindow[iconWindow.Id] = useAllianceLayout;

    private bool ShouldUsePartyCooldownAllianceLayout(int observedGroupCount)
    {
        if (!ClientState.IsLoggedIn)
        {
            this.partyCooldownLayoutModeTracker.Reset();
            return false;
        }

        var snapshot = this.GetPartyCooldownFrameSnapshot();
        var diagnostics = snapshot.RosterDiagnostics;
        var allianceObserved = diagnostics.HasAllianceSource
                               || diagnostics.Source == PartyCooldownRosterSource.Alliance
                               || diagnostics.AlliancePartyCount >= 2
                               || observedGroupCount >= 2;
        return this.partyCooldownLayoutModeTracker.Resolve(allianceObserved, snapshot.TimestampUtc);
    }
}
