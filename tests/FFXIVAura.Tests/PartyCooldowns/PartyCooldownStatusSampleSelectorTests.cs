using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownStatusSampleSelectorTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownStatusSampleSelector prioritizes party list source timers", PrioritizesPartyListSourceTimers),
        ("PartyCooldownStatusSampleSelector keeps higher-priority shorter timers", KeepsHigherPriorityShorterTimers),
        ("PartyCooldownStatusSampleSelector uses longer equal-priority timers", UsesLongerEqualPriorityTimers),
        ("PartyCooldownStatusSampleSelector prefers live samples over fallback samples", PrefersLiveSamples),
        ("PartyCooldownStatusSampleSelector prefers lower-source live samples over fallback samples", PrefersLiveSamplesAcrossSourcePriorities),
        ("PartyCooldownStatusSampleSelector uses the longest timer across status ids", UsesLongestTimerAcrossStatusIds),
        ("PartyCooldownStatusSampleSelector prefers live samples across status ids", PrefersLiveSamplesAcrossStatusIds),
    ];

    private static void PrioritizesPartyListSourceTimers()
    {
        Equal(
            PartyCooldownStatusSamplePriority.PartyListSource,
            PartyCooldownStatusSampleSelector.GetPriority(fromPartyList: true, onSourceMember: true));
        Equal(
            PartyCooldownStatusSamplePriority.PartyListRecipient,
            PartyCooldownStatusSampleSelector.GetPriority(fromPartyList: true, onSourceMember: false));
        Equal(
            PartyCooldownStatusSamplePriority.ObjectSource,
            PartyCooldownStatusSampleSelector.GetPriority(fromPartyList: false, onSourceMember: true));
    }

    private static void KeepsHigherPriorityShorterTimers()
    {
        var partyListTimer = new PartyCooldownActiveStatus(
            100,
            8.2f,
            PartyCooldownStatusSamplePriority.PartyListSource);
        var objectTimer = new PartyCooldownActiveStatus(
            100,
            9.1f,
            PartyCooldownStatusSamplePriority.ObjectSource);

        True(
            !PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(objectTimer, partyListTimer),
            "object status must not replace the party-list source timer");
        True(
            PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(partyListTimer, objectTimer),
            "party-list source timer should replace an object status");
    }

    private static void UsesLongerEqualPriorityTimers()
    {
        var shorter = new PartyCooldownActiveStatus(
            100,
            8.2f,
            PartyCooldownStatusSamplePriority.PartyListRecipient);
        var longer = shorter with { Remaining = 8.7f };

        True(
            PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(longer, shorter),
            "the longer timer should win when sample priorities match");
        True(
            !PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(shorter, longer),
            "a shorter timer should not replace an equal-priority timer");
    }

    private static void PrefersLiveSamples()
    {
        var fallback = new PartyCooldownActiveStatus(
            100,
            9f,
            PartyCooldownStatusSamplePriority.PartyListSource,
            StatusSnapshotOrigin.Fallback);
        var live = fallback with { Remaining = 8f, Origin = StatusSnapshotOrigin.Live };

        True(
            PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(live, fallback),
            "a live sample should replace an equal-priority fallback sample");
        True(
            !PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(fallback, live),
            "a fallback sample should not replace an equal-priority live sample");
    }

    private static void PrefersLiveSamplesAcrossSourcePriorities()
    {
        var fallback = new PartyCooldownActiveStatus(
            100,
            9f,
            PartyCooldownStatusSamplePriority.PartyListSource,
            StatusSnapshotOrigin.Fallback);
        var live = new PartyCooldownActiveStatus(
            100,
            8f,
            PartyCooldownStatusSamplePriority.ObjectRecipient,
            StatusSnapshotOrigin.Live);

        True(
            PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(live, fallback),
            "a live object sample should replace a higher-source fallback sample");
        True(
            !PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(fallback, live),
            "a fallback party-list sample should not replace a live object sample");
    }

    private static void UsesLongestTimerAcrossStatusIds()
    {
        var shortSourceStatus = new PartyCooldownActiveStatus(
            100,
            2f,
            PartyCooldownStatusSamplePriority.PartyListSource);
        var longRecipientStatus = new PartyCooldownActiveStatus(
            101,
            14f,
            PartyCooldownStatusSamplePriority.PartyListRecipient);

        True(
            PartyCooldownStatusSampleSelector.IsPreferredAcrossStatusIds(longRecipientStatus, shortSourceStatus),
            "a longer related status should determine the active timer regardless of observation source");
        True(
            !PartyCooldownStatusSampleSelector.IsPreferredAcrossStatusIds(shortSourceStatus, longRecipientStatus),
            "a short source-only status must not hide a longer recipient effect");
    }

    private static void PrefersLiveSamplesAcrossStatusIds()
    {
        var fallback = new PartyCooldownActiveStatus(
            100,
            14f,
            PartyCooldownStatusSamplePriority.PartyListSource,
            StatusSnapshotOrigin.Fallback);
        var live = new PartyCooldownActiveStatus(
            101,
            8f,
            PartyCooldownStatusSamplePriority.ObjectRecipient,
            StatusSnapshotOrigin.Live);

        True(
            PartyCooldownStatusSampleSelector.IsPreferredAcrossStatusIds(live, fallback),
            "a live related status should replace a longer fallback status");
        True(
            !PartyCooldownStatusSampleSelector.IsPreferredAcrossStatusIds(fallback, live),
            "a fallback related status should not hide a live status");
    }
}
