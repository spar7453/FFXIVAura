using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownLayoutModeTrackerTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownLayoutModeTracker selects and retains four-player layouts", SelectsAndRetainsFourPlayerLayouts),
        ("PartyCooldownLayoutModeTracker retains alliance mode through brief gaps", RetainsAllianceModeThroughBriefGaps),
        ("PartyCooldownLayoutModeTracker resets alliance mode explicitly", ResetsAllianceModeExplicitly),
        ("PartyCooldownLayoutModeTracker applies previews only while settings are open", AppliesPreviewsOnlyWhileSettingsAreOpen),
    ];

    private static void SelectsAndRetainsFourPlayerLayouts()
    {
        var tracker = new PartyCooldownLayoutModeTracker(TimeSpan.FromSeconds(3));
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        Equal(PartyCooldownLayoutEditMode.FourPlayer, tracker.Resolve(false, 4, now));
        Equal(PartyCooldownLayoutEditMode.FourPlayer, tracker.Resolve(false, 1, now.AddSeconds(1)));
        Equal(PartyCooldownLayoutEditMode.EightPlayer, tracker.Resolve(false, 8, now.AddSeconds(2)));
        Equal(PartyCooldownLayoutEditMode.EightPlayer, tracker.Resolve(false, 0, now.AddSeconds(3)));
    }

    private static void RetainsAllianceModeThroughBriefGaps()
    {
        var tracker = new PartyCooldownLayoutModeTracker(TimeSpan.FromSeconds(3));
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        tracker.Resolve(allianceObserved: false, partyMemberCount: 4, now);
        Equal(PartyCooldownLayoutEditMode.Alliance, tracker.Resolve(allianceObserved: true, partyMemberCount: 24, now));
        Equal(PartyCooldownLayoutEditMode.Alliance, tracker.Resolve(allianceObserved: false, partyMemberCount: 4, now.AddSeconds(2.9)));
        Equal(PartyCooldownLayoutEditMode.FourPlayer, tracker.Resolve(allianceObserved: false, partyMemberCount: 4, now.AddSeconds(3)));
    }

    private static void ResetsAllianceModeExplicitly()
    {
        var tracker = new PartyCooldownLayoutModeTracker(TimeSpan.FromSeconds(3));
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        tracker.Resolve(allianceObserved: true, partyMemberCount: 24, now);
        tracker.Reset();

        Equal(
            PartyCooldownLayoutEditMode.EightPlayer,
            tracker.Resolve(allianceObserved: false, partyMemberCount: 1, now.AddSeconds(1)));
    }

    private static void AppliesPreviewsOnlyWhileSettingsAreOpen()
    {
        Equal(
            PartyCooldownLayoutEditMode.FourPlayer,
            PartyCooldownLayoutModeTracker.ResolvePreview(
                configVisible: true,
                PartyCooldownLayoutEditMode.FourPlayer,
                PartyCooldownLayoutEditMode.Alliance));
        Equal(
            PartyCooldownLayoutEditMode.EightPlayer,
            PartyCooldownLayoutModeTracker.ResolvePreview(
                configVisible: true,
                PartyCooldownLayoutEditMode.EightPlayer,
                PartyCooldownLayoutEditMode.Alliance));
        Equal(
            PartyCooldownLayoutEditMode.Alliance,
            PartyCooldownLayoutModeTracker.ResolvePreview(
                configVisible: false,
                PartyCooldownLayoutEditMode.FourPlayer,
                PartyCooldownLayoutEditMode.Alliance));
    }
}
