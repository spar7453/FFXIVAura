namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private IEnumerable<AbilityDefinition> GetVisibleAbilities(string job, uint level, IconWindowConfig? iconWindow = null)
    {
        iconWindow ??= this.GetActiveIconWindow();
        var candidates = this.GetJobCandidates(job, level).ToList();
        if (iconWindow.TrackedByJob.TryGetValue(job, out var tracked) && tracked.Count > 0)
        {
            var resolved = new List<AbilityDefinition>();
            var seen = new HashSet<uint>();
            foreach (var trackedId in tracked)
            {
                var ability = this.ResolveTrackedAbilityForLevel(trackedId, job, level, candidates);
                if (ability is not null && seen.Add(ability.ActionId))
                    resolved.Add(ability);
            }

            return resolved;
        }

        return candidates
            .OrderBy(a => a.Job == "ROLE" ? 1 : 0)
            .ThenBy(a => this.GetTrackedOrder(iconWindow, job, a.Id))
            .ThenByDescending(a => a.Cooldown)
            .ThenBy(a => a.Level);
    }

    private AbilityDefinition? ResolveTrackedAbilityForLevel(string trackedId, string job, uint level, IReadOnlyList<AbilityDefinition> candidates)
    {
        var exact = candidates.FirstOrDefault(a => string.Equals(a.Id, trackedId, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var trackedAbility = this.FindTrackedAbilityDefinition(trackedId, job);
        if (trackedAbility is null)
            return null;

        var equivalenceGroup = this.GetActionEquivalenceGroup(trackedAbility.ActionId);
        if (equivalenceGroup == 0)
            return null;

        return candidates
            .Where(a => a.Level <= level)
            .Where(a => string.Equals(a.Job, trackedAbility.Job, StringComparison.OrdinalIgnoreCase)
                        || (string.Equals(trackedAbility.Job, "ROLE", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(a.Job, "ROLE", StringComparison.OrdinalIgnoreCase)))
            .Where(a => this.GetActionEquivalenceGroup(a.ActionId) == equivalenceGroup)
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
            Job = row.IsRoleAction ? "ROLE" : job,
            Level = (byte)Math.Min(row.ClassJobLevel, byte.MaxValue),
            Cooldown = row.Recast100ms / 10f,
            Charges = Math.Max(row.MaxCharges, (byte)1),
            IconId = row.Icon,
        };
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
        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            return 0;

        try
        {
            return sheet.GetRow(actionId).EquivalenceGroup;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action equivalence group for {actionId}.");
            return 0;
        }
    }

    private IEnumerable<AbilityDefinition> GetDisplayAbilities(string job, uint level, IconWindowConfig iconWindow)
    {
        return this.GetVisibleAbilities(job, level, iconWindow)
            .Where(ability => this.ShouldDisplayAbility(ability, iconWindow));
    }

    private bool ShouldDisplayAbility(AbilityDefinition ability, IconWindowConfig iconWindow)
    {
        var state = this.GetCooldown(ability);
        return iconWindow.DisplayCondition switch
        {
            IconDisplayCondition.InCombat => this.IsInCombat(),
            IconDisplayCondition.OutOfCombat => !this.IsInCombat(),
            IconDisplayCondition.CoolingOnly => state.IsCooling || (state.MaxCharges > 1 && state.CurrentCharges == 0),
            IconDisplayCondition.ReadyOnly => !state.IsCooling && !(state.MaxCharges > 1 && state.CurrentCharges == 0),
            _ => true,
        };
    }

    private bool IsInCombat()
    {
        return Condition[ConditionFlag.InCombat]
               || (ObjectTable.LocalPlayer is not null && ObjectTable.LocalPlayer.StatusFlags.HasFlag(StatusFlags.InCombat));
    }

    private IEnumerable<AbilityDefinition> GetJobCandidates(string job, uint level)
    {
        var configured = this.abilities
            .Where(a => a.Level <= level)
            .Where(a => string.Equals(a.Job, job, StringComparison.OrdinalIgnoreCase)
                        || JobInfo.CanUseRoleAction(job, a));

        var seen = new HashSet<uint>();
        foreach (var ability in configured)
        {
            seen.Add(ability.ActionId);
            yield return ability;
        }

        foreach (var ability in this.GetGameActionCandidates(job, level))
        {
            if (seen.Add(ability.ActionId))
                yield return ability;
        }
    }

    private int GetTrackedOrder(IconWindowConfig iconWindow, string job, string id)
    {
        if (!iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
            return int.MaxValue;

        var index = tracked.FindIndex(item => string.Equals(item, id, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }

    private IEnumerable<AbilityDefinition> GetGameActionCandidates(string job, uint level)
    {
        var classJobIds = JobInfo.ApplicableClassJobIds(job);
        if (classJobIds.Count == 0)
            yield break;

        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            yield break;

        foreach (var row in sheet)
        {
            if (row.RowId == 0 || !classJobIds.Contains(row.ClassJob.RowId) || row.ClassJobLevel == 0 || row.ClassJobLevel > level)
                continue;

            var category = row.ActionCategory.RowId;
            if (category is not (2 or 3 or 4))
                continue;

            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            yield return new AbilityDefinition
            {
                Id = $"{job.ToLowerInvariant()}-{row.RowId}",
                Name = name,
                ActionId = row.RowId,
                ActionIds = [row.RowId],
                Job = job,
                Level = (byte)Math.Min(row.ClassJobLevel, byte.MaxValue),
                Cooldown = row.Recast100ms / 10f,
                Charges = 1,
                IconId = row.Icon,
            };
        }
    }
}
