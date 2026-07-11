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
        ("PartyCooldownStatusSampleSelector uses the longest timer across status ids", UsesLongestTimerAcrossStatusIds),
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
}
