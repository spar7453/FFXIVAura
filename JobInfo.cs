namespace FFXIVAura;

public static class JobInfo
{
    private static readonly Dictionary<uint, string> JobIds = new()
    {
        [19] = "PLD", [21] = "WAR", [32] = "DRK", [37] = "GNB",
        [24] = "WHM", [28] = "SCH", [33] = "AST", [40] = "SGE",
        [20] = "MNK", [22] = "DRG", [30] = "NIN", [34] = "SAM", [39] = "RPR", [41] = "VPR",
        [23] = "BRD", [31] = "MCH", [38] = "DNC",
        [25] = "BLM", [27] = "SMN", [35] = "RDM", [42] = "PCT",
    };

    private static readonly Dictionary<string, uint> BaseClassIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PLD"] = 1,  // GLD
        ["MNK"] = 2,  // PGL
        ["WAR"] = 3,  // MRD
        ["DRG"] = 4,  // LNC
        ["BRD"] = 5,  // ARC
        ["WHM"] = 6,  // CNJ
        ["BLM"] = 7,  // THM
        ["SMN"] = 26, // ACN
        ["SCH"] = 26, // ACN
        ["NIN"] = 29, // ROG
    };

    private static readonly HashSet<string> Tanks = ["PLD", "WAR", "DRK", "GNB"];
    private static readonly HashSet<string> Healers = ["WHM", "SCH", "AST", "SGE"];
    private static readonly HashSet<string> Melee = ["MNK", "DRG", "NIN", "SAM", "RPR", "VPR"];
    private static readonly HashSet<string> Ranged = ["BRD", "MCH", "DNC"];
    private static readonly HashSet<string> Casters = ["BLM", "SMN", "RDM", "PCT"];

    public static string Code(uint classJobId) => JobIds.GetValueOrDefault(classJobId, "JOB");

    public static uint Id(string code)
    {
        foreach (var (id, jobCode) in JobIds)
        {
            if (string.Equals(jobCode, code, StringComparison.OrdinalIgnoreCase))
                return id;
        }

        return 0;
    }

    public static HashSet<uint> ApplicableClassJobIds(string code)
    {
        var ids = new HashSet<uint>();
        var jobId = Id(code);
        if (jobId != 0)
            ids.Add(jobId);

        if (BaseClassIds.TryGetValue(code, out var classId))
            ids.Add(classId);

        return ids;
    }

    public static bool CanUseRoleAction(string job, AbilityDefinition ability)
    {
        if (!string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase))
            return false;

        var id = ability.Id;
        if (Tanks.Contains(job))
            return id is "rampart" or "low-blow" or "provoke" or "interject" or "reprisal" or "arms-length" or "shirk";
        if (Healers.Contains(job))
            return id is "repose" or "esuna" or "swiftcast" or "lucid-dreaming" or "surecast" or "rescue";
        if (Melee.Contains(job))
            return id is "second-wind" or "leg-sweep" or "bloodbath" or "feint" or "arms-length" or "true-north";
        if (Ranged.Contains(job))
            return id is "leg-graze" or "foot-graze" or "head-graze" or "peloton" or "second-wind" or "arms-length";
        if (Casters.Contains(job))
            return id is "addling" or "sleep" or "swiftcast" or "lucid-dreaming" or "surecast";

        return false;
    }
}
