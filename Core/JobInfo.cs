namespace FFXIVAura;

public static class JobInfo
{
    private const uint FramedClassJobIconBase = 62100;

    private static readonly Dictionary<uint, string> JobIds = new()
    {
        [19] = "PLD",
        [21] = "WAR",
        [32] = "DRK",
        [37] = "GNB",
        [24] = "WHM",
        [28] = "SCH",
        [33] = "AST",
        [40] = "SGE",
        [20] = "MNK",
        [22] = "DRG",
        [30] = "NIN",
        [34] = "SAM",
        [39] = "RPR",
        [41] = "VPR",
        [23] = "BRD",
        [31] = "MCH",
        [38] = "DNC",
        [25] = "BLM",
        [27] = "SMN",
        [35] = "RDM",
        [42] = "PCT",
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

    private static readonly HashSet<string> Tanks = new(StringComparer.OrdinalIgnoreCase) { "PLD", "WAR", "DRK", "GNB" };
    private static readonly HashSet<string> Healers = new(StringComparer.OrdinalIgnoreCase) { "WHM", "SCH", "AST", "SGE" };
    private static readonly HashSet<string> Melee = new(StringComparer.OrdinalIgnoreCase) { "MNK", "DRG", "NIN", "SAM", "RPR", "VPR" };
    private static readonly HashSet<string> Ranged = new(StringComparer.OrdinalIgnoreCase) { "BRD", "MCH", "DNC" };
    private static readonly HashSet<string> Casters = new(StringComparer.OrdinalIgnoreCase) { "BLM", "SMN", "RDM", "PCT" };

    private static readonly HashSet<string> TankRoleActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "rampart", "low-blow", "provoke", "interject", "reprisal", "arms-length", "shirk",
    };

    private static readonly HashSet<string> HealerRoleActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "repose", "esuna", "swiftcast", "lucid-dreaming", "surecast", "rescue",
    };

    private static readonly HashSet<string> MeleeRoleActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "second-wind", "leg-sweep", "bloodbath", "feint", "arms-length", "true-north",
    };

    private static readonly HashSet<string> RangedRoleActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "leg-graze", "foot-graze", "head-graze", "peloton", "second-wind", "arms-length",
    };

    private static readonly HashSet<string> CasterRoleActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "addle", "sleep", "swiftcast", "lucid-dreaming", "surecast",
    };

    public static string Code(uint classJobId) => JobIds.GetValueOrDefault(classJobId, "JOB");

    public static uint IconId(string code)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        var classJobId = Id(normalizedCode);
        return IconId(classJobId);
    }

    public static uint IconId(uint classJobId)
    {
        return JobIds.ContainsKey(classJobId) ? FramedClassJobIconBase + classJobId : 0;
    }

    public static uint Id(string code)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        foreach (var (id, jobCode) in JobIds)
        {
            if (string.Equals(jobCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
                return id;
        }

        return 0;
    }

    public static HashSet<uint> ApplicableClassJobIds(string code)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        var ids = new HashSet<uint>();
        var jobId = Id(normalizedCode);
        if (jobId != 0)
            ids.Add(jobId);

        if (BaseClassIds.TryGetValue(normalizedCode, out var classId))
            ids.Add(classId);

        return ids;
    }

    public static int PartyRoleSortOrder(string code)
    {
        var jobCode = code?.Trim() ?? string.Empty;
        if (Tanks.Contains(jobCode))
            return 1;
        if (Healers.Contains(jobCode))
            return 2;
        if (Melee.Contains(jobCode) || Ranged.Contains(jobCode) || Casters.Contains(jobCode))
            return 3;

        return 4;
    }

    public static bool CanUseRoleAction(string job, AbilityDefinition ability)
    {
        if (!string.Equals(ability.Job?.Trim(), "ROLE", StringComparison.OrdinalIgnoreCase))
            return false;

        var jobCode = job?.Trim() ?? string.Empty;
        var id = ability.Id?.Trim() ?? string.Empty;
        if (Tanks.Contains(jobCode))
            return TankRoleActions.Contains(id);
        if (Healers.Contains(jobCode))
            return HealerRoleActions.Contains(id);
        if (Melee.Contains(jobCode))
            return MeleeRoleActions.Contains(id);
        if (Ranged.Contains(jobCode))
            return RangedRoleActions.Contains(id);
        if (Casters.Contains(jobCode))
            return CasterRoleActions.Contains(id);

        return false;
    }
}
