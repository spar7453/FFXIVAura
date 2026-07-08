using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class JobInfoTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("JobInfo allows caster Addle role action", AllowsCasterAddleRoleAction),
        ("JobInfo matches role actions case-insensitively", MatchesRoleActionsCaseInsensitively),
        ("JobInfo trims job ids and class ids", TrimsJobIdsAndClassIds),
    ];

    private static void AllowsCasterAddleRoleAction()
    {
        var addle = new AbilityDefinition { Id = "addle", Job = "ROLE" };
        var typo = new AbilityDefinition { Id = "addling", Job = "ROLE" };

        True(JobInfo.CanUseRoleAction("BLM", addle), "casters should include Addle");
        True(!JobInfo.CanUseRoleAction("BLM", typo), "unknown role ids should not be accepted");
    }

    private static void MatchesRoleActionsCaseInsensitively()
    {
        var addle = new AbilityDefinition { Id = " ADDLE ", Job = " role " };
        var rampart = new AbilityDefinition { Id = "RAMPART", Job = "ROLE" };

        True(JobInfo.CanUseRoleAction(" pct ", addle), "role matching should trim and ignore case");
        True(JobInfo.CanUseRoleAction("pld", rampart), "job matching should ignore case");
        True(!JobInfo.CanUseRoleAction("pld", addle), "role groups should stay job-specific");
    }

    private static void TrimsJobIdsAndClassIds()
    {
        Equal(22u, JobInfo.Id(" drg "));

        var drgIds = JobInfo.ApplicableClassJobIds(" drg ");
        True(drgIds.Contains(22u), "job id should be included");
        True(drgIds.Contains(4u), "base class id should be included");
    }
}
