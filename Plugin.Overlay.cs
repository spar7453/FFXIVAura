namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawOverlay()
    {
        var job = JobInfo.Code(PlayerState.ClassJob.RowId);
        var level = (uint)(PlayerState.EffectiveLevel > 0 ? PlayerState.EffectiveLevel : PlayerState.Level);
        this.EnsureIconWindows();

        foreach (var iconWindow in this.config.IconWindows)
            this.DrawIconWindow(iconWindow, job, level);
    }

    private void DrawIconWindow(IconWindowConfig iconWindow, string job, uint level)
    {
        var visible = iconWindow.Role == IconWindowRole.SkillCooldowns
            ? this.GetDisplayAbilities(job, level, iconWindow).ToList()
            : [];
        if (visible.Count > 0)
            this.AutoAlignWhenVisibleSkillsChanged(iconWindow, job, level, visible);

        var auras = iconWindow.Role == IconWindowRole.SkillCooldowns
            ? []
            : this.GetDisplayAuras(iconWindow).ToList();
        if (visible.Count == 0 && auras.Count == 0 && this.config.LockOverlay)
            return;

        ImGui.SetNextWindowPos(iconWindow.Position, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(0f);
        ImGui.SetNextWindowSize(new Vector2(iconWindow.Width, iconWindow.Height), ImGuiCond.Always);
        var flags = ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.NoDecoration;
        if (this.config.LockOverlay)
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (!ImGui.Begin($"FFXIVAuraOverlay-{iconWindow.Id}", flags))
        {
            ImGui.End();
            ImGui.PopStyleVar();
            return;
        }

        iconWindow.Position = ImGui.GetWindowPos();
        ImGui.SetWindowFontScale(iconWindow.FontScale);

        var iconSize = iconWindow.IconSize;
        var gap = iconWindow.Gap;
        var areaSize = ImGui.GetContentRegionAvail();
        var areaOrigin = ImGui.GetCursorScreenPos();
        if (!this.config.LockOverlay)
            this.DrawOverlayEditStage(ImGui.GetWindowDrawList(), areaOrigin, areaOrigin + areaSize);

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            this.draggedOverlayId = null;
            this.draggedOverlayMouseStart = Vector2.Zero;
            this.draggedOverlayPositionStart = Vector2.Zero;
        }

        for (var i = 0; i < visible.Count; i++)
        {
            var ability = visible[i];
            var localPos = this.GetOverlayIconPosition(iconWindow, job, ability, i, visible.Count, areaSize, iconSize, gap);
            var iconPos = areaOrigin + localPos;

            ImGui.SetCursorScreenPos(iconPos);
            this.DrawAbilityIcon(ability, iconSize, iconWindow);
            this.HandleOverlayIconInteraction(iconWindow, job, ability, localPos, iconPos, areaSize, iconSize);
        }

        for (var i = 0; i < auras.Count; i++)
        {
            var aura = auras[i];
            var localPos = this.GetAuraIconPosition(i, auras.Count, areaSize, iconSize, gap);
            ImGui.SetCursorScreenPos(areaOrigin + localPos);
            this.DrawAuraIcon(aura, iconSize, iconWindow);
        }

        ImGui.End();
        ImGui.PopStyleVar();
    }

    private Vector2 GetAuraIconPosition(int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
        => this.GetOverlayAutoPosition(index, visibleCount, areaSize, iconSize, gap);

    private void AutoAlignWhenVisibleSkillsChanged(IconWindowConfig iconWindow, string job, uint level, IReadOnlyList<AbilityDefinition> visible)
    {
        var key = $"{iconWindow.Id}:{job}";
        var visibleKey = $"{level}:{string.Join("|", visible.Select(ability => ability.Id))}";
        if (this.visibleAbilityKeys.TryGetValue(key, out var previous) && string.Equals(previous, visibleKey, StringComparison.Ordinal))
            return;

        this.visibleAbilityKeys[key] = visibleKey;
        this.AlignOverlayIcons(iconWindow, job, level);
        PluginInterface.SavePluginConfig(this.config);
    }

    private void DrawOverlayEditStage(ImDrawListPtr draw, Vector2 min, Vector2 max)
    {
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.52f)), 8f);
        var grid = 24f;
        for (var x = min.X; x <= max.X; x += grid)
            draw.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), ImGui.GetColorU32(new Vector4(0.4f, 0.91f, 0.98f, 0.16f)));

        for (var y = min.Y; y <= max.Y; y += grid)
            draw.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), ImGui.GetColorU32(new Vector4(0.4f, 0.91f, 0.98f, 0.16f)));

        draw.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.4f, 0.91f, 0.98f, 0.82f)), 8f, ImDrawFlags.None, 2f);
        draw.AddRect(min - new Vector2(2f, 2f), max + new Vector2(2f, 2f), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.82f)), 9f, ImDrawFlags.None, 2f);
    }

    private Vector2 GetOverlayIconPosition(IconWindowConfig iconWindow, string job, AbilityDefinition ability, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        if (iconWindow.IconPositionsByJob.TryGetValue(job, out var positions)
            && positions.TryGetValue(ability.Id, out var saved))
        {
            return this.ClampOverlayIconPosition(saved, areaSize, iconSize);
        }

        return this.GetOverlayAutoPosition(index, visibleCount, areaSize, iconSize, gap);
    }

    private Vector2 GetOverlayAutoPosition(int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        var cell = iconSize + gap;
        var columns = Math.Max(1, (int)Math.Floor((areaSize.X + gap) / Math.Max(1f, cell)));
        var count = Math.Max(1, visibleCount);
        var rows = Math.Max(1, (int)Math.Ceiling(count / (float)columns));
        var row = index / columns;
        var column = index % columns;
        var itemsInRow = row == rows - 1 ? count - row * columns : columns;
        var rowWidth = Math.Max(0f, itemsInRow * iconSize + Math.Max(0, itemsInRow - 1) * gap);
        var blockHeight = Math.Max(0f, rows * iconSize + Math.Max(0, rows - 1) * gap);
        var x = Math.Max(0f, MathF.Round((areaSize.X - rowWidth) * 0.5f)) + column * cell;
        var y = Math.Max(0f, MathF.Round((areaSize.Y - blockHeight) * 0.5f)) + row * cell;
        return this.ClampOverlayIconPosition(new Vector2(x, y), areaSize, iconSize);
    }

    private Vector2 ClampOverlayIconPosition(Vector2 position, Vector2 areaSize, float iconSize)
    {
        return new Vector2(
            Math.Clamp(position.X, 0f, Math.Max(0f, areaSize.X - iconSize)),
            Math.Clamp(position.Y, 0f, Math.Max(0f, areaSize.Y - iconSize)));
    }

    private void HandleOverlayIconInteraction(IconWindowConfig iconWindow, string job, AbilityDefinition ability, Vector2 localPos, Vector2 iconPos, Vector2 areaSize, float iconSize)
    {
        if (this.config.LockOverlay)
            return;

        ImGui.SetCursorScreenPos(iconPos);
        ImGui.InvisibleButton($"##overlay-drag-{ability.Id}", new Vector2(iconSize, iconSize));

        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            this.UntrackAbility(iconWindow, job, ability.Id);
            PluginInterface.SavePluginConfig(this.config);
            return;
        }

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 2f))
        {
            if (!string.Equals(this.draggedOverlayId, ability.Id, StringComparison.OrdinalIgnoreCase))
            {
                this.draggedOverlayId = ability.Id;
                this.draggedOverlayMouseStart = ImGui.GetMousePos();
                this.draggedOverlayPositionStart = localPos;
            }

            var next = this.ClampOverlayIconPosition(
                this.draggedOverlayPositionStart + ImGui.GetMousePos() - this.draggedOverlayMouseStart,
                areaSize,
                iconSize);
            this.SetOverlayIconPosition(iconWindow, job, ability.Id, next);
            PluginInterface.SavePluginConfig(this.config);
        }

        if (string.Equals(this.draggedOverlayId, ability.Id, StringComparison.OrdinalIgnoreCase))
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(new Vector4(0.45f, 0.72f, 1f, 0.95f)), 3f, ImDrawFlags.None, 2f);
        }
    }

    private void SetOverlayIconPosition(IconWindowConfig iconWindow, string job, string abilityId, Vector2 position)
    {
        if (!iconWindow.IconPositionsByJob.TryGetValue(job, out var positions))
        {
            positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            iconWindow.IconPositionsByJob[job] = positions;
        }

        positions[abilityId] = position;
    }

    private void AlignOverlayIcons(IconWindowConfig iconWindow, string job, uint level, bool preferTrackedOrder = false)
    {
        var visible = this.GetVisibleAbilities(job, level, iconWindow).ToList();
        if (visible.Count == 0)
            return;

        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        var rows = this.GetOverlayPositionRows(iconWindow, job, visible, areaSize);
        var positions = iconWindow.IconPositionsByJob.TryGetValue(job, out var existing)
            ? new Dictionary<string, Vector2>(existing, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);

        var orderedRows = rows
            .OrderBy(row => row.Average(item => item.Position.Y))
            .ToList();
        var verticalGap = Math.Max(0f, iconWindow.Gap);
        var rowStep = iconWindow.IconSize + verticalGap;
        var totalHeight = Math.Max(0f, orderedRows.Count * iconWindow.IconSize + Math.Max(0, orderedRows.Count - 1) * verticalGap);
        var startY = Math.Max(0f, MathF.Round((areaSize.Y - totalHeight) * 0.5f));

        for (var rowIndex = 0; rowIndex < orderedRows.Count; rowIndex++)
        {
            var row = orderedRows[rowIndex];
            var items = (preferTrackedOrder
                    ? row
                        .OrderBy(item => this.GetTrackedOrder(iconWindow, job, item.Ability.Id))
                        .ThenBy(item => item.Position.X)
                    : row
                        .OrderBy(item => item.Position.X)
                        .ThenBy(item => this.GetTrackedOrder(iconWindow, job, item.Ability.Id)))
                .ThenBy(item => item.Index)
                .ToList();
            var rowWidth = Math.Max(0f, items.Count * iconWindow.IconSize + Math.Max(0, items.Count - 1) * iconWindow.Gap);
            var startX = Math.Max(0f, MathF.Round((areaSize.X - rowWidth) * 0.5f));
            var y = Math.Clamp(MathF.Round(startY + rowIndex * rowStep), 0f, Math.Max(0f, areaSize.Y - iconWindow.IconSize));

            for (var column = 0; column < items.Count; column++)
            {
                positions[items[column].Ability.Id] = this.ClampOverlayIconPosition(
                    new Vector2(startX + column * (iconWindow.IconSize + iconWindow.Gap), y),
                    areaSize,
                    iconWindow.IconSize);
            }
        }

        iconWindow.IconPositionsByJob[job] = positions;
    }

    private List<List<(AbilityDefinition Ability, int Index, Vector2 Position)>> GetOverlayPositionRows(
        IconWindowConfig iconWindow,
        string job,
        List<AbilityDefinition> visible,
        Vector2 areaSize)
    {
        var rowThreshold = Math.Max(6f, MathF.Round(iconWindow.IconSize * 0.55f));
        var items = visible
            .Select((ability, index) => (
                Ability: ability,
                Index: index,
                Position: this.GetOverlayIconPosition(iconWindow, job, ability, index, visible.Count, areaSize, iconWindow.IconSize, iconWindow.Gap)))
            .OrderBy(item => item.Position.Y)
            .ThenBy(item => item.Position.X)
            .ThenBy(item => item.Index)
            .ToList();

        var rows = new List<List<(AbilityDefinition Ability, int Index, Vector2 Position)>>();
        foreach (var item in items)
        {
            var lastRow = rows.Count > 0 ? rows[^1] : null;
            if (lastRow is null || Math.Abs(item.Position.Y - lastRow.Average(rowItem => rowItem.Position.Y)) > rowThreshold)
            {
                rows.Add([item]);
                continue;
            }

            lastRow.Add(item);
        }

        return rows;
    }
}
