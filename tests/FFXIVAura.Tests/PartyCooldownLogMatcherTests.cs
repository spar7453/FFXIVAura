using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownLogMatcherTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownLogMatcher matches actor names and worlds", PartyCooldownLogMatcherMatchesActorNamesAndWorlds),
        ("PartyCooldownLogMatcher rejects pet and wrong-world actors", PartyCooldownLogMatcherRejectsWrongActors),
        ("PartyCooldownLogMatcher recognizes completed action use templates", PartyCooldownLogMatcherRecognizesCompletedActionUseTemplates),
        ("PartyCooldownLogMatcher detects ambiguous worldless matches", PartyCooldownLogMatcherDetectsAmbiguousWorldlessMatches),
        ("PartyCooldownLogMatcher normalizes action names", PartyCooldownLogMatcherNormalizesActionNames),
        ("PartyCooldownLogMatcher deduplicates repeated use logs", PartyCooldownLogMatcherDeduplicatesRepeatedUseLogs),
    ];

    private static void PartyCooldownLogMatcherMatchesActorNamesAndWorlds()
    {
        True(PartyCooldownLogMatcher.IsSameActor("메론사와", 0, "메론사와", 12), "empty source world should fall back to name matching");
        True(PartyCooldownLogMatcher.IsSameActor("메론사와@펜리르", 12, "메론사와", 12), "world suffix should not break actor name matching");
    }

    private static void PartyCooldownLogMatcherRejectsWrongActors()
    {
        True(!PartyCooldownLogMatcher.IsSameActor("요정 에오스", 0, "민트트", 0), "pet names should not match party members");
        True(!PartyCooldownLogMatcher.IsSameActor("메론사와", 12, "메론사와", 13), "different nonzero worlds should not match");
    }

    private static void PartyCooldownLogMatcherRecognizesCompletedActionUseTemplates()
    {
        True(PartyCooldownLogMatcher.IsCompletedActionUseTemplate("메론사와가 내단을 시전했습니다."), "Korean completed cast logs should match");
        True(PartyCooldownLogMatcher.IsCompletedActionUseTemplate("Melon uses Second Wind."), "English use logs should match");
        True(!PartyCooldownLogMatcher.IsCompletedActionUseTemplate("메론사와가 글레어를 시전합니다."), "cast-start logs should not start cooldowns");
    }

    private static void PartyCooldownLogMatcherDetectsAmbiguousWorldlessMatches()
    {
        True(PartyCooldownLogMatcher.IsAmbiguousActorFallback(0, 2), "worldless duplicate names should be ambiguous");
        True(!PartyCooldownLogMatcher.IsAmbiguousActorFallback(12, 2), "world-known matches should use the world id to disambiguate");
        True(!PartyCooldownLogMatcher.IsAmbiguousActorFallback(0, 1), "a single worldless name match is usable");
    }

    private static void PartyCooldownLogMatcherNormalizesActionNames()
    {
        Equal("내단", PartyCooldownLogMatcher.NormalizeActionName(" 내단 "));
        Equal(string.Empty, PartyCooldownLogMatcher.NormalizeActionName(" "));
    }

    private static void PartyCooldownLogMatcherDeduplicatesRepeatedUseLogs()
    {
        var now = new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc);
        True(PartyCooldownLogMatcher.IsDuplicateUse(now, now.AddMilliseconds(500), TimeSpan.FromSeconds(1)), "logs inside the dedupe window should be duplicates");
        True(!PartyCooldownLogMatcher.IsDuplicateUse(now, now.AddSeconds(2), TimeSpan.FromSeconds(1)), "logs outside the dedupe window should be accepted");
    }
}
