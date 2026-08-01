namespace FFXIVAura;

internal static class AbilityDataValidator
{
    public static bool IsValid(IReadOnlyList<AbilityDefinition> definitions)
    {
        if (definitions.Count == 0)
            return false;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actionIds = new HashSet<uint>();
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Id)
                || !ids.Add(definition.Id.Trim())
                || string.IsNullOrWhiteSpace(definition.Name)
                || string.IsNullOrWhiteSpace(definition.Job)
                || !IsKnownJob(definition.Job)
                || definition.ActionId == 0
                || !actionIds.Add(definition.ActionId)
                || definition.ActionIds is null
                || definition.IconId == 0
                || definition.Level == 0
                || definition.Charges == 0
                || !float.IsFinite(definition.Cooldown)
                || definition.Cooldown < 0f)
            {
                return false;
            }
        }

        var replacementGroups = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.ReplacementGroup))
            .GroupBy(
                definition => $"{definition.Job.Trim()}:{definition.ReplacementGroup.Trim()}",
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return replacementGroups.Length > 0
               && replacementGroups.All(group =>
                   group.Count() >= 2
                   && group.Select(definition => definition.Level).Distinct().Count() == group.Count());
    }

    private static bool IsKnownJob(string job)
        => string.Equals(job.Trim(), "ROLE", StringComparison.OrdinalIgnoreCase)
           || JobInfo.Id(job.Trim()) > 0;
}
