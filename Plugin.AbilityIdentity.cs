namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private ExcludedAbilityFilter BuildExcludedAbilityFilter(IconWindowConfig iconWindow, string job)
    {
        var ids = iconWindow.ExcludedByJob.TryGetValue(job, out var excluded)
            ? excluded.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var equivalenceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            var ability = this.FindTrackedAbilityDefinition(id, job);
            var key = ability is null ? string.Empty : this.GetAbilityEquivalenceKey(ability);
            if (!string.IsNullOrEmpty(key))
                equivalenceKeys.Add(key);
        }

        return new ExcludedAbilityFilter(ids, equivalenceKeys);
    }

    private bool IsAbilityExcluded(ExcludedAbilityFilter excluded, AbilityDefinition ability)
    {
        if (excluded.Ids.Contains(ability.Id))
            return true;

        var key = this.GetAbilityEquivalenceKey(ability);
        return !string.IsNullOrEmpty(key) && excluded.EquivalenceKeys.Contains(key);
    }

    private bool TrackedAbilityIdMatchesAbility(string trackedId, string job, AbilityDefinition ability)
    {
        if (string.Equals(trackedId, ability.Id, StringComparison.OrdinalIgnoreCase))
            return true;

        var trackedAbility = this.FindTrackedAbilityDefinition(trackedId, job);
        return trackedAbility is not null && this.AbilitiesShareEquivalence(trackedAbility, ability);
    }

    private bool TrackedAbilityIdsMatch(string firstId, string secondId, string job)
    {
        if (string.Equals(firstId, secondId, StringComparison.OrdinalIgnoreCase))
            return true;

        var secondAbility = this.FindTrackedAbilityDefinition(secondId, job);
        return secondAbility is not null && this.TrackedAbilityIdMatchesAbility(firstId, job, secondAbility);
    }

    private bool AbilitiesShareEquivalence(AbilityDefinition first, AbilityDefinition second)
    {
        var firstKey = this.GetAbilityEquivalenceKey(first);
        return !string.IsNullOrEmpty(firstKey)
               && string.Equals(firstKey, this.GetAbilityEquivalenceKey(second), StringComparison.OrdinalIgnoreCase);
    }

    private string GetAbilityEquivalenceKey(AbilityDefinition ability)
    {
        var group = this.GetActionEquivalenceGroup(ability.ActionId);
        var category = this.GetActionCategory(ability.ActionId).RowId;
        return group == 0 || category == 0 ? string.Empty : $"{ability.Job}:{category}:{group}";
    }

    private readonly record struct ExcludedAbilityFilter(HashSet<string> Ids, HashSet<string> EquivalenceKeys);

    private AbilityDefinition? ResolveTrackedAbilityForLevel(string trackedId, string job, uint level, IReadOnlyList<AbilityDefinition> candidates)
    {
        var exact = candidates.FirstOrDefault(a => string.Equals(a.Id, trackedId, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var trackedAbility = this.FindTrackedAbilityDefinition(trackedId, job);
        if (trackedAbility is null)
            return null;

        var equivalenceKey = this.GetAbilityEquivalenceKey(trackedAbility);
        if (string.IsNullOrEmpty(equivalenceKey))
            return null;

        return candidates
            .Where(a => a.Level <= level)
            .Where(a => string.Equals(this.GetAbilityEquivalenceKey(a), equivalenceKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Level)
            .ThenByDescending(a => a.ActionId)
            .FirstOrDefault();
    }

    private AbilityDefinition? FindTrackedAbilityDefinition(string trackedId, string job)
    {
        var configured = this.abilities.FirstOrDefault(a => string.Equals(a.Id, trackedId, StringComparison.OrdinalIgnoreCase));
        if (configured is not null)
            return configured;

        var actionId = ParseActionIdFromGeneratedAbilityId(trackedId);
        if (actionId == 0)
            return null;

        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            return null;

        try
        {
            var row = sheet.GetRow(actionId);
            if (row.RowId == 0)
                return null;

            var name = row.Name.ExtractText();
            return new AbilityDefinition
            {
                Id = trackedId,
                Name = string.IsNullOrWhiteSpace(name) ? trackedId : name,
                ActionId = row.RowId,
                ActionIds = [row.RowId],
                ActionCategoryId = row.ActionCategory.RowId,
                Job = row.IsRoleAction ? "ROLE" : job,
                Level = (byte)Math.Min(row.ClassJobLevel, byte.MaxValue),
                Cooldown = row.Recast100ms / 10f,
                Charges = Math.Max(row.MaxCharges, (byte)1),
                IconId = row.Icon,
            };
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to resolve tracked action {trackedId} for {job}.");
            return null;
        }
    }

    private static uint ParseActionIdFromGeneratedAbilityId(string trackedId)
    {
        var dash = trackedId.LastIndexOf('-');
        if (dash < 0 || dash == trackedId.Length - 1)
            return 0;

        return uint.TryParse(trackedId[(dash + 1)..], out var actionId) ? actionId : 0;
    }

    private byte GetActionEquivalenceGroup(uint actionId)
    {
        if (this.actionEquivalenceGroupCache.TryGetValue(actionId, out var cached))
            return cached;

        byte group = 0;
        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            return 0;

        try
        {
            group = sheet.GetRow(actionId).EquivalenceGroup;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action equivalence group for {actionId}.");
        }

        this.actionEquivalenceGroupCache[actionId] = group;
        return group;
    }
}
