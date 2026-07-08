using FFXIVAura.Tests;

var tests = new List<(string Name, Action Run)>();

tests.AddRange(CooldownMathTests.Cases);
tests.AddRange(JobInfoTests.Cases);
tests.AddRange(AbilityDataTests.Cases);
tests.AddRange(IconWindowIdentityTests.Cases);
tests.AddRange(IconWindowCloneTests.Cases);
tests.AddRange(OverlayPositionKeysTests.Cases);
tests.AddRange(RuntimeScopeKeysTests.Cases);
tests.AddRange(KeybindTextFormatterTests.Cases);
tests.AddRange(AuraSearchIndexTests.Cases);
tests.AddRange(AuraSearchDisplayResultTests.Cases);
tests.AddRange(OverlayTests.Cases);
tests.AddRange(SkillPositionLayoutTests.Cases);
tests.AddRange(PartyCooldownBoardLayoutTests.Cases);
tests.AddRange(PartyCooldownDefinitionSelectorTests.Cases);
tests.AddRange(PartyCooldownDefinitionIdentityTests.Cases);
tests.AddRange(PartyCooldownStatusResolverTests.Cases);
tests.AddRange(PartyCooldownLogMatcherTests.Cases);
tests.AddRange(PartyCooldownOwnerResolverTests.Cases);
tests.AddRange(ConfigTests.Cases);
tests.AddRange(NativeTooltipTests.Cases);
tests.AddRange(PartyAuraTests.Cases);
tests.AddRange(PerformanceFrameStatsTests.Cases);
tests.AddRange(PerformanceProfilerTests.Cases);
tests.AddRange(PerformanceProfileCsvTests.Cases);
tests.AddRange(AuraStatusFrameIndexTests.Cases);
tests.AddRange(ActionKeybindIndexTests.Cases);
tests.AddRange(LoginStabilizationStateTests.Cases);

var failed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Console.WriteLine($"{failed} test(s) failed.");
    return failed;
}

Console.WriteLine($"{tests.Count} tests passed.");
return 0;
