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
        ("JobInfo maps job icons from class job ids", MapsJobIconsFromClassJobIds),
        ("JobInfo orders party roles", OrdersPartyRoles),
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

    private static void MapsJobIconsFromClassJobIds()
    {
        Equal(62119u, JobInfo.IconId("PLD"));
        Equal(62119u, JobInfo.IconId(19));
        Equal(62132u, JobInfo.IconId("DRK"));
        Equal(62132u, JobInfo.IconId(32));
        Equal(62141u, JobInfo.IconId("VPR"));
        Equal(0u, JobInfo.IconId("GLD"));
        Equal(0u, JobInfo.IconId("JOB"));
    }

    private static void OrdersPartyRoles()
    {
        Equal(1, JobInfo.PartyRoleSortOrder(" pld "));
        Equal(2, JobInfo.PartyRoleSortOrder("SCH"));
        Equal(3, JobInfo.PartyRoleSortOrder("mch"));
        Equal(3, JobInfo.PartyRoleSortOrder("PCT"));
        Equal(4, JobInfo.PartyRoleSortOrder("JOB"));
    }
}
