namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawTrackedSkillEditor(IconWindowConfig iconWindow, string job, uint level)
    {
        if (job == "JOB")
            return;

        var tracked = this.GetTrackedAbilityList(iconWindow, job);

        ImGui.TextUnformatted($"추적 스킬: {job}");
        if (ImGui.Button("현재 표시 기본값 사용"))
        {
            this.ResetTrackedAbilitiesToDefault(iconWindow, job, level);
            this.QueueConfigSave();
        }

        ImGui.SameLine();
        if (ImGui.Button("추적 초기화"))
        {
            this.ClearTrackedAbilities(iconWindow, job);
            this.QueueConfigSave();
        }

        var editor = this.BuildTrackedSkillEditorData(iconWindow, job, level, tracked);

        ImGui.Spacing();
        foreach (var (id, label) in TrackedEditorTabs)
        {
            if (id != TrackedEditorTabs[0].Id)
                ImGui.SameLine();

            var tabLabel = $"{label} ({editor.TabCounts.GetValueOrDefault(id)})";
            if (ImGui.Selectable(tabLabel, string.Equals(this.config.TrackedEditorTab, id, StringComparison.OrdinalIgnoreCase), ImGuiSelectableFlags.DontClosePopups, new Vector2(82f, 0f)))
            {
                this.config.TrackedEditorTab = id;
                this.QueueConfigSave();
            }
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(240f);
        var search = this.config.TrackedSkillSearch ?? string.Empty;
        if (ImGui.InputTextWithHint("##FFXIVAuraTrackedSkillSearch", "스킬 이름 또는 ID 검색", ref search, 80))
        {
            this.config.TrackedSkillSearch = search;
            this.QueueConfigSave();
        }

        if (!string.IsNullOrWhiteSpace(this.config.TrackedSkillSearch))
        {
            ImGui.SameLine();
            if (ImGui.Button("검색 지우기"))
            {
                this.config.TrackedSkillSearch = string.Empty;
                this.QueueConfigSave();
            }
        }

        if (editor.ManualTracking)
        {
            ImGui.TextDisabled("수동 추적 모드");
        }
        else
        {
            ImGui.TextDisabled(editor.Excluded.Count > 0
                ? $"자동 표시 모드 / 제외 {editor.Excluded.Count}개"
                : "자동 표시 모드");
        }

        if (editor.ManualTracking)
        {
            if (iconWindow.TrackedByJob.TryGetValue(job, out var trackedList))
            {
                this.DrawTrackedOrderEditorTabbed(iconWindow, job, editor.CurrentCandidates, trackedList);
                this.DrawOrderEditorResizeHandle(iconWindow);
            }
        }

        ImGui.BeginChild("FFXIVAuraTrackedSkillList", new Vector2(560f, 390f), true);
        foreach (var ability in editor.Candidates)
        {
            var selected = editor.ManualTracking
                ? this.IsAbilityTracked(iconWindow, job, ability.Id)
                : !this.IsAbilityExcluded(editor.ExcludedFilter, ability);
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
                if (editor.ManualTracking)
                {
                    this.TrackAbility(iconWindow, job, level, ability.Id);
                }
                else
                {
                    this.IncludeAbility(iconWindow, job, ability.Id);
                }
            }
            else
            {
                if (editor.ManualTracking)
                    this.UntrackAbility(iconWindow, job, ability.Id);
                else
                    this.ExcludeAbility(iconWindow, job, ability.Id);
            }

            this.QueueConfigSave();
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
            var nextHeight = Math.Clamp(iconWindow.OrderEditorHeight + ImGui.GetIO().MouseDelta.Y, MinOrderEditorHeight, MaxOrderEditorHeight);
            if (Math.Abs(nextHeight - iconWindow.OrderEditorHeight) > 0.1f)
            {
                iconWindow.OrderEditorHeight = nextHeight;
                this.QueueConfigSave();
            }
        }
    }

    private void DrawTrackedOrderEditorTabbed(IconWindowConfig iconWindow, string job, List<AbilityDefinition> allCandidates, List<string> tracked)
    {
        if (tracked.Count == 0)
            return;

        var visible = this.ResolveTrackedOrderAbilities(job, allCandidates, tracked);

        var rows = this.GetOverlayPositionRows(iconWindow, job, visible, new Vector2(iconWindow.Width, iconWindow.Height));
        if (rows.Count == 0)
            return;

        iconWindow.ActiveOrderRow = Math.Clamp(iconWindow.ActiveOrderRow, 0, rows.Count - 1);

        ImGui.Spacing();
        ImGui.TextUnformatted("표시 순서");
        ImGui.BeginChild("FFXIVAuraTrackedOrderList", new Vector2(560f, Math.Clamp(iconWindow.OrderEditorHeight, MinOrderEditorHeight, MaxOrderEditorHeight)), true);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            this.draggedTrackedId = null;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (rowIndex > 0)
                ImGui.SameLine();

            if (ImGui.Selectable($"{rowIndex + 1}줄", iconWindow.ActiveOrderRow == rowIndex, ImGuiSelectableFlags.DontClosePopups, new Vector2(58f, 0f)))
            {
                iconWindow.ActiveOrderRow = rowIndex;
                this.QueueConfigSave();
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
            var trackedIndex = this.FindTrackedAbilityIndex(tracked, id, job);
            if (trackedIndex < 0)
                continue;

            var displayName = string.IsNullOrWhiteSpace(ability.Name) ? ability.Id : ability.Name;
            ImGui.PushID($"tabbed-order-{id}");

            if (ImGui.SmallButton("▲") && rowItemIndex > 0)
            {
                var previousId = selectedRow[rowItemIndex - 1].Ability.Id;
                if (this.SwapTrackedSkills(tracked, job, id, previousId))
                    this.SaveAndRealignTrackedOrder(iconWindow, job);
            }

            ImGui.SameLine(0f, 4f);
            if (ImGui.SmallButton("▼") && rowItemIndex < selectedRow.Count - 1)
            {
                var nextId = selectedRow[rowItemIndex + 1].Ability.Id;
                if (this.SwapTrackedSkills(tracked, job, id, nextId))
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
                && this.MoveTrackedSkill(tracked, job, this.draggedTrackedId, trackedIndex))
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
                var lastTrackedIndex = this.FindTrackedAbilityIndex(tracked, selectedRow[^1].Ability.Id, job);
                if (lastTrackedIndex >= 0 && this.MoveTrackedSkill(tracked, job, this.draggedTrackedId, lastTrackedIndex + 1))
                    this.SaveAndRealignTrackedOrder(iconWindow, job);
            }
        }

        ImGui.EndChild();
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
