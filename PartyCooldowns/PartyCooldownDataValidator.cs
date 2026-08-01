namespace FFXIVAura;

internal static class PartyCooldownDataValidator
{
    public static bool IsValid(IReadOnlyList<PartyCooldownDefinition> definitions)
    {
        if (definitions.Count == 0)
            return false;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actionIds = new HashSet<uint>();
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Id)
                || !ids.Add(definition.Id.Trim())
                || !Enum.TryParse<PartyCooldownCategory>(
                    definition.Category,
                    ignoreCase: true,
                    out _)
                || string.IsNullOrWhiteSpace(definition.Job)
                || !IsKnownJob(definition.Job)
                || definition.ActionId == 0
                || !actionIds.Add(definition.ActionId)
                || definition.StatusIds is null
                || definition.StatusIds.Any(statusId => statusId == 0)
                || definition.StatusIds.Distinct().Count() != definition.StatusIds.Length
                || !string.IsNullOrWhiteSpace(definition.ReplacementGroup)
                || !float.IsFinite(definition.Cooldown)
                || definition.Cooldown < 0f
                || !float.IsFinite(definition.Duration)
                || definition.Duration < 0f
                || (definition.Duration > 0f && definition.StatusIds.Length == 0)
                || definition.Charges == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsKnownJob(string job)
        => string.Equals(job.Trim(), "ROLE", StringComparison.OrdinalIgnoreCase)
           || JobInfo.Id(job.Trim()) > 0;
}
