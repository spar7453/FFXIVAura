using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownLayoutModeTrackerTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownLayoutModeTracker retains alliance mode through brief gaps", RetainsAllianceModeThroughBriefGaps),
        ("PartyCooldownLayoutModeTracker resets alliance mode explicitly", ResetsAllianceModeExplicitly),
        ("PartyCooldownLayoutModeTracker applies previews only while settings are open", AppliesPreviewsOnlyWhileSettingsAreOpen),
    ];

    private static void RetainsAllianceModeThroughBriefGaps()
    {
        var tracker = new PartyCooldownLayoutModeTracker(TimeSpan.FromSeconds(3));
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        True(tracker.Resolve(allianceObserved: true, now), "observed alliance should select the alliance layout");
        True(tracker.Resolve(allianceObserved: false, now.AddSeconds(2.9)), "brief source gaps should retain the alliance layout");
        True(!tracker.Resolve(allianceObserved: false, now.AddSeconds(3)), "expired source gaps should return to the regular layout");
    }

    private static void ResetsAllianceModeExplicitly()
    {
        var tracker = new PartyCooldownLayoutModeTracker(TimeSpan.FromSeconds(3));
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        tracker.Resolve(allianceObserved: true, now);
        tracker.Reset();

        True(!tracker.Resolve(allianceObserved: false, now.AddSeconds(1)), "reset should clear retained alliance mode");
    }

    private static void AppliesPreviewsOnlyWhileSettingsAreOpen()
    {
        True(
            PartyCooldownLayoutModeTracker.ResolvePreview(configVisible: true, alliancePreview: true, automaticAllianceLayout: false),
            "alliance preview should override automatic regular mode while settings are open");
        True(
            !PartyCooldownLayoutModeTracker.ResolvePreview(configVisible: true, alliancePreview: false, automaticAllianceLayout: true),
            "regular preview should override automatic alliance mode while settings are open");
        True(
            PartyCooldownLayoutModeTracker.ResolvePreview(configVisible: false, alliancePreview: false, automaticAllianceLayout: true),
            "closing settings should restore automatic mode");
        True(
            !PartyCooldownLayoutModeTracker.ResolvePreview(configVisible: true, alliancePreview: null, automaticAllianceLayout: false),
            "missing preview should keep automatic mode");
    }
}
