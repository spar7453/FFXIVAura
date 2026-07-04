namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawTrackedSkillEditor(IconWindowConfig iconWindow, string job, uint level)
    {
        if (job == "JOB")
            return;

        if (!iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
        {
            tracked = [];
            iconWindow.TrackedByJob[job] = tracked;
        }

        ImGui.TextUnformatted($"추적 스킬: {job}");
        if (ImGui.Button("현재 표시 기본값 사용"))
        {
            tracked.Clear();
            tracked.AddRange(this.GetJobCandidates(job, level)
                .OrderBy(a => a.Job == "ROLE" ? 1 : 0)
                .ThenByDescending(a => a.Cooldown)
                .ThenBy(a => a.Level)
                .Select(a => a.Id));
            PluginInterface.SavePluginConfig(this.config);
        }

        ImGui.SameLine();
        if (ImGui.Button("추적 초기화"))
        {
            tracked.Clear();
            PluginInterface.SavePluginConfig(this.config);
        }

        var allCandidates = this.GetJobCandidates(job, level).ToList();
        var tabCounts = TrackedEditorTabs.ToDictionary(
            tab => tab.Id,
            tab => allCandidates.Count(a => this.IsInTrackedEditorTab(a, tab.Id)),
            StringComparer.OrdinalIgnoreCase);

        ImGui.Spacing();
        foreach (var (id, label) in TrackedEditorTabs)
        {
            if (id != TrackedEditorTabs[0].Id)
                ImGui.SameLine();

            var tabLabel = $"{label} ({tabCounts.GetValueOrDefault(id)})";
            if (ImGui.Selectable(tabLabel, string.Equals(this.config.TrackedEditorTab, id, StringComparison.OrdinalIgnoreCase), ImGuiSelectableFlags.DontClosePopups, new Vector2(82f, 0f)))
            {
                this.config.TrackedEditorTab = id;
                PluginInterface.SavePluginConfig(this.config);
            }
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(240f);
        var search = this.config.TrackedSkillSearch ?? string.Empty;
        if (ImGui.InputTextWithHint("##FFXIVAuraTrackedSkillSearch", "스킬 이름 또는 ID 검색", ref search, 80))
        {
            this.config.TrackedSkillSearch = search;
            PluginInterface.SavePluginConfig(this.config);
        }

        if (!string.IsNullOrWhiteSpace(this.config.TrackedSkillSearch))
        {
            ImGui.SameLine();
            if (ImGui.Button("검색 지우기"))
            {
                this.config.TrackedSkillSearch = string.Empty;
                PluginInterface.SavePluginConfig(this.config);
            }
        }

        this.DrawTrackedOrderEditorTabbed(iconWindow, job, allCandidates, tracked);
        this.DrawOrderEditorResizeHandle(iconWindow);

        var candidates = allCandidates
            .Where(a => this.IsInTrackedEditorTab(a, this.config.TrackedEditorTab))
            .Where(a => this.MatchesTrackedSkillSearch(a, this.config.TrackedSkillSearch))
            .OrderByDescending(a => tracked.Any(id => string.Equals(id, a.Id, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(a => a.Job == "ROLE" ? 1 : 0)
            .ThenBy(a => a.Level)
            .ThenBy(a => string.IsNullOrWhiteSpace(a.Name) ? a.Id : a.Name, StringComparer.CurrentCulture)
            .ToList();

        ImGui.BeginChild("FFXIVAuraTrackedSkillList", new Vector2(560f, 390f), true);
        foreach (var ability in candidates)
        {
            var selected = tracked.Any(id => string.Equals(id, ability.Id, StringComparison.OrdinalIgnoreCase));
            var changed = false;

            ImGui.PushID($"track-{ability.Id}");
            changed |= ImGui.Checkbox("##enabled", ref selected);
            ImGui.SameLine(0f, 6f);
            this.DrawSkillListIcon(ability, 24f);
            ImGui.SameLine(0f, 8f);

            var displayName = string.IsNullOrWhiteSpace(ability.Name) ? ability.Id : ability.Name;
            ImGui.TextUnformatted($"{displayName}  Lv{ability.Level}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{ability.Job} / {displayName}\n{ability.Id}\nAction {ability.ActionId}");
            ImGui.PopID();

            if (!changed)
                continue;

            if (selected)
            {
                if (!tracked.Any(id => string.Equals(id, ability.Id, StringComparison.OrdinalIgnoreCase)))
                    tracked.Add(ability.Id);
            }
            else
            {
                tracked.RemoveAll(id => string.Equals(id, ability.Id, StringComparison.OrdinalIgnoreCase));
            }

            PluginInterface.SavePluginConfig(this.config);
        }

        ImGui.EndChild();
    }

    private void DrawOrderEditorResizeHandle(IconWindowConfig iconWindow)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.05f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.08f));
        ImGui.Button("##FFXIVAuraOrderResize", new Vector2(560f, 12f));
        ImGui.PopStyleColor(3);

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var centerY = (min.Y + max.Y) * 0.5f;
        var centerX = (min.X + max.X) * 0.5f;
        var active = ImGui.IsItemHovered() || ImGui.IsItemActive();
        var lineColor = ImGui.GetColorU32(new Vector4(0.70f, 0.76f, 0.82f, active ? 0.72f : 0.36f));
        var handleColor = ImGui.GetColorU32(new Vector4(0.82f, 0.88f, 0.94f, active ? 0.95f : 0.62f));
        var draw = ImGui.GetWindowDrawList();

        draw.AddLine(new Vector2(min.X, centerY), new Vector2(max.X, centerY), lineColor, 1f);
        draw.AddLine(new Vector2(centerX - 6f, centerY - 2f), new Vector2(centerX, centerY - 7f), handleColor, 1.5f);
        draw.AddLine(new Vector2(centerX + 6f, centerY - 2f), new Vector2(centerX, centerY - 7f), handleColor, 1.5f);
        draw.AddLine(new Vector2(centerX - 6f, centerY + 2f), new Vector2(centerX, centerY + 7f), handleColor, 1.5f);
        draw.AddLine(new Vector2(centerX + 6f, centerY + 2f), new Vector2(centerX, centerY + 7f), handleColor, 1.5f);

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            iconWindow.OrderEditorHeight = Math.Clamp(iconWindow.OrderEditorHeight + ImGui.GetIO().MouseDelta.Y, 90f, 520f);
            PluginInterface.SavePluginConfig(this.config);
        }
    }
    private void DrawTrackedOrderEditorTabbed(IconWindowConfig iconWindow, string job, List<AbilityDefinition> allCandidates, List<string> tracked)
    {
        if (tracked.Count == 0)
            return;

        var byId = allCandidates
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (tracked.RemoveAll(id => !byId.ContainsKey(id)) > 0)
            PluginInterface.SavePluginConfig(this.config);

        var visible = tracked
            .Select(id => byId.TryGetValue(id, out var ability) ? ability : null)
            .Where(ability => ability is not null)
            .Cast<AbilityDefinition>()
            .ToList();

        var rows = this.GetOverlayPositionRows(iconWindow, job, visible, new Vector2(iconWindow.Width, iconWindow.Height));
        if (rows.Count == 0)
            return;

        iconWindow.ActiveOrderRow = Math.Clamp(iconWindow.ActiveOrderRow, 0, rows.Count - 1);

        ImGui.Spacing();
        ImGui.TextUnformatted("표시 순서");
        ImGui.BeginChild("FFXIVAuraTrackedOrderList", new Vector2(560f, Math.Clamp(iconWindow.OrderEditorHeight, 90f, 520f)), true);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            this.draggedTrackedId = null;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (rowIndex > 0)
                ImGui.SameLine();

            if (ImGui.Selectable($"{rowIndex + 1}줄", iconWindow.ActiveOrderRow == rowIndex, ImGuiSelectableFlags.DontClosePopups, new Vector2(58f, 0f)))
            {
                iconWindow.ActiveOrderRow = rowIndex;
                PluginInterface.SavePluginConfig(this.config);
            }
        }

        ImGui.Separator();
        var selectedRow = rows[iconWindow.ActiveOrderRow]
            .OrderBy(item => item.Position.X)
            .ThenBy(item => item.Index)
            .ToList();

        for (var rowItemIndex = 0; rowItemIndex < selectedRow.Count; rowItemIndex++)
        {
            var item = selectedRow[rowItemIndex];
            var ability = item.Ability;
            var id = ability.Id;
            var trackedIndex = tracked.FindIndex(trackedId => string.Equals(trackedId, id, StringComparison.OrdinalIgnoreCase));
            if (trackedIndex < 0)
                continue;

            var displayName = string.IsNullOrWhiteSpace(ability.Name) ? ability.Id : ability.Name;
            ImGui.PushID($"tabbed-order-{id}");

            if (ImGui.SmallButton("▲") && rowItemIndex > 0)
            {
                var previousId = selectedRow[rowItemIndex - 1].Ability.Id;
                if (this.SwapTrackedSkills(tracked, id, previousId))
                    this.SaveAndRealignTrackedOrder(iconWindow, job);
            }

            ImGui.SameLine(0f, 4f);
            if (ImGui.SmallButton("▼") && rowItemIndex < selectedRow.Count - 1)
            {
                var nextId = selectedRow[rowItemIndex + 1].Ability.Id;
                if (this.SwapTrackedSkills(tracked, id, nextId))
                    this.SaveAndRealignTrackedOrder(iconWindow, job);
            }

            ImGui.SameLine(0f, 8f);
            this.DrawSkillListIcon(ability, 20f);
            ImGui.SameLine(0f, 6f);
            ImGui.Selectable($"{displayName}  Lv{ability.Level}##drag-row", false, ImGuiSelectableFlags.None, new Vector2(0f, 22f));
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 2f))
                this.draggedTrackedId = id;

            if (!string.IsNullOrEmpty(this.draggedTrackedId)
                && !string.Equals(this.draggedTrackedId, id, StringComparison.OrdinalIgnoreCase)
                && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                && this.MoveTrackedSkill(tracked, this.draggedTrackedId, trackedIndex))
            {
                this.SaveAndRealignTrackedOrder(iconWindow, job);
            }

            if (string.Equals(this.draggedTrackedId, id, StringComparison.OrdinalIgnoreCase))
            {
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(new Vector4(0.45f, 0.72f, 1f, 0.95f)), 2f, ImDrawFlags.None, 1.5f);
            }

            ImGui.PopID();
        }

        if (selectedRow.Count > 0)
        {
            ImGui.InvisibleButton("##tracked-order-bottom-drop", new Vector2(Math.Max(1f, ImGui.GetContentRegionAvail().X), 24f));
            if (!string.IsNullOrEmpty(this.draggedTrackedId)
                && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
            {
                var lastTrackedIndex = tracked.FindIndex(trackedId => string.Equals(trackedId, selectedRow[^1].Ability.Id, StringComparison.OrdinalIgnoreCase));
                if (lastTrackedIndex >= 0 && this.MoveTrackedSkill(tracked, this.draggedTrackedId, lastTrackedIndex + 1))
                    this.SaveAndRealignTrackedOrder(iconWindow, job);
            }
        }

        ImGui.EndChild();
    }

    private void SaveAndRealignTrackedOrder(IconWindowConfig iconWindow, string job)
    {
        var level = (uint)(PlayerState.EffectiveLevel > 0 ? PlayerState.EffectiveLevel : PlayerState.Level);
        this.AlignOverlayIcons(iconWindow, job, level, preferTrackedOrder: true);
        PluginInterface.SavePluginConfig(this.config);
    }

    private void UntrackAbility(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
            tracked.RemoveAll(id => string.Equals(id, abilityId, StringComparison.OrdinalIgnoreCase));

        if (iconWindow.IconPositionsByJob.TryGetValue(job, out var positions))
            positions.Remove(abilityId);
    }

    private bool MoveTrackedSkill(List<string> tracked, string id, int targetIndex)
    {
        var currentIndex = tracked.FindIndex(item => string.Equals(item, id, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0 || currentIndex == targetIndex)
            return false;

        var item = tracked[currentIndex];
        tracked.RemoveAt(currentIndex);
        if (currentIndex < targetIndex)
            targetIndex--;

        targetIndex = Math.Clamp(targetIndex, 0, tracked.Count);
        tracked.Insert(targetIndex, item);
        return true;
    }

    private bool SwapTrackedSkills(List<string> tracked, string firstId, string secondId)
    {
        var firstIndex = tracked.FindIndex(item => string.Equals(item, firstId, StringComparison.OrdinalIgnoreCase));
        var secondIndex = tracked.FindIndex(item => string.Equals(item, secondId, StringComparison.OrdinalIgnoreCase));
        if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex)
            return false;

        (tracked[firstIndex], tracked[secondIndex]) = (tracked[secondIndex], tracked[firstIndex]);
        return true;
    }

    private bool MatchesTrackedSkillSearch(AbilityDefinition ability, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        var query = search.Trim();
        var displayName = string.IsNullOrWhiteSpace(ability.Name) ? ability.Id : ability.Name;
        return displayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || ability.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
               || ability.ActionId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
               || ability.Level.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInTrackedEditorTab(AbilityDefinition ability, string tab)
    {
        if (string.Equals(tab, "Role", StringComparison.OrdinalIgnoreCase))
            return string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase))
            return false;

        var category = this.GetActionCategory(ability.ActionId);
        var name = category.Name;
        return tab switch
        {
            "WeaponSkill" => CategoryNameContains(name, "무기", "Weapon") || category.RowId == 3,
            "Spell" => CategoryNameContains(name, "마법", "Spell") || category.RowId == 2,
            "Ability" => CategoryNameContains(name, "능력", "Ability") || category.RowId == 4,
            _ => true,
        };
    }

    private static bool CategoryNameContains(string name, params string[] needles)
    {
        return needles.Any(needle => name.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private (uint RowId, string Name) GetActionCategory(uint actionId)
    {
        if (this.actionCategoryCache.TryGetValue(actionId, out var cached))
            return cached;

        var category = (RowId: 0u, Name: string.Empty);
        try
        {
            var sheet = DataManager.GetExcelSheet<GameAction>();
            if (sheet is not null)
            {
                var row = sheet.GetRow(actionId);
                category = (row.ActionCategory.RowId, row.ActionCategory.Value.Name.ExtractText());
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action category for {actionId}.");
        }

        this.actionCategoryCache[actionId] = category;
        return category;
    }

    private void DrawSkillListIcon(AbilityDefinition ability, float size)
    {
        var lookup = new GameIconLookup(ability.IconId, false, true, null);
        var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
        ImGui.Image(texture.Handle, new Vector2(size, size));
    }
}
