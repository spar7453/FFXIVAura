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

        var windowPosition = ImGui.GetWindowPos();
        if (!this.config.LockOverlay && Vector2.DistanceSquared(iconWindow.Position, windowPosition) > 0.25f)
        {
            iconWindow.Position = windowPosition;
            this.QueueConfigSave();
        }
        else
        {
            iconWindow.Position = windowPosition;
        }
        ImGui.SetWindowFontScale(iconWindow.FontScale);

        var iconSize = iconWindow.IconSize;
        var gap = iconWindow.Gap;
        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        var areaOrigin = ImGui.GetCursorScreenPos();
        if (!this.config.LockOverlay)
            this.DrawOverlayEditStage(ImGui.GetWindowDrawList(), areaOrigin, areaOrigin + areaSize);
        if (!this.config.LockOverlay)
            this.HandleOverlayResize(iconWindow, areaOrigin, areaSize);

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
            var localPos = this.GetAuraIconPosition(iconWindow, aura, i, auras.Count, areaSize, iconSize, gap);
            var iconPos = areaOrigin + localPos;
            ImGui.SetCursorScreenPos(iconPos);
            this.DrawAuraIcon(aura, iconSize, iconWindow);
            this.HandleAuraIconInteraction(iconWindow, aura, localPos, iconPos, areaSize, iconSize);
        }

        ImGui.End();
        ImGui.PopStyleVar();

        if (!this.config.LockOverlay)
        {
            this.DrawOverlayRoleControls(iconWindow, areaOrigin, areaSize);
            this.DrawOverlayDisplayConditionControl(iconWindow, areaOrigin, areaSize);
            this.DrawOverlayAlignmentControls(iconWindow, job, level, areaOrigin, areaSize);
        }
    }

    private Vector2 GetAuraIconPosition(IconWindowConfig iconWindow, AuraState aura, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        if (iconWindow.AuraPositionsByRole.TryGetValue(GetAuraPositionGroupKey(iconWindow), out var positions)
            && positions.TryGetValue(GetAuraPositionKey(aura.StatusId), out var saved))
        {
            return this.ClampOverlayIconPosition(saved, areaSize, iconSize);
        }

        return this.GetOverlayAutoPosition(iconWindow, index, visibleCount, areaSize, iconSize, gap);
    }

    private void AutoAlignWhenVisibleSkillsChanged(IconWindowConfig iconWindow, string job, uint level, IReadOnlyList<AbilityDefinition> visible)
    {
        var key = $"{iconWindow.Id}:{job}";
        var visibleKey = $"{level}:{string.Join("|", visible.Select(ability => ability.Id))}";
        if (this.visibleAbilityKeys.TryGetValue(key, out var previous) && string.Equals(previous, visibleKey, StringComparison.Ordinal))
            return;

        this.visibleAbilityKeys[key] = visibleKey;
        if (iconWindow.IconPositionsByJob.TryGetValue(job, out var positions) && positions.Count > 0)
            return;

        this.AlignOverlayIcons(iconWindow, job, level);
        this.QueueConfigSave();
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

    private void HandleOverlayResize(IconWindowConfig iconWindow, Vector2 areaOrigin, Vector2 areaSize)
    {
        const float handleSize = 18f;
        const float minWidth = 120f;
        const float minHeight = 40f;
        const float maxWidth = 1200f;
        const float maxHeight = 400f;

        var handleMax = areaOrigin + areaSize;
        var handleMin = handleMax - new Vector2(handleSize, handleSize);
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(new Vector4(0.4f, 0.91f, 0.98f, 0.9f));
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.75f));

        draw.AddTriangleFilled(
            handleMax - new Vector2(handleSize, 0f),
            handleMax,
            handleMax - new Vector2(0f, handleSize),
            shadow);
        draw.AddLine(handleMax - new Vector2(13f, 3f), handleMax - new Vector2(3f, 13f), color, 2f);
        draw.AddLine(handleMax - new Vector2(8f, 3f), handleMax - new Vector2(3f, 8f), color, 2f);

        ImGui.SetCursorScreenPos(handleMin);
        ImGui.InvisibleButton($"##overlay-resize-{iconWindow.Id}", new Vector2(handleSize, handleSize));
        if (!ImGui.IsItemActive() || !ImGui.IsMouseDragging(ImGuiMouseButton.Left, 2f))
            return;

        var delta = ImGui.GetIO().MouseDelta;
        var nextWidth = Math.Clamp(iconWindow.Width + delta.X, minWidth, maxWidth);
        var nextHeight = Math.Clamp(iconWindow.Height + delta.Y, minHeight, maxHeight);
        if (Math.Abs(nextWidth - iconWindow.Width) <= 0.1f && Math.Abs(nextHeight - iconWindow.Height) <= 0.1f)
            return;

        iconWindow.Width = nextWidth;
        iconWindow.Height = nextHeight;
        this.QueueConfigSave();
    }

    private void DrawOverlayRoleControls(IconWindowConfig iconWindow, Vector2 areaOrigin, Vector2 areaSize)
    {
        const float padding = 6f;
        var framePadding = new Vector2(7f, 3f);
        var itemSpacing = new Vector2(4f, 0f);
        var options = WindowRoleOptions().ToList();
        var contentWidth = options.Sum(option => ImGui.CalcTextSize(option.Label).X + framePadding.X * 2f)
                           + Math.Max(0, options.Count - 1) * itemSpacing.X;
        var windowWidth = contentWidth + padding * 2f;
        var windowHeight = ImGui.GetTextLineHeight() + framePadding.Y * 2f + padding * 2f;
        var windowY = Math.Max(0f, areaOrigin.Y - windowHeight - 4f);

        ImGui.SetNextWindowPos(new Vector2(areaOrigin.X, windowY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);
        if (!ImGui.Begin($"FFXIVAuraOverlayRoleControls-{iconWindow.Id}", OverlayControlWindowFlags()))
        {
            ImGui.End();
            return;
        }

        ImGui.PushID($"overlay-role-controls-{iconWindow.Id}");
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, itemSpacing);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.05f, 0.08f, 0.1f, 0.82f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.08f, 0.32f, 0.38f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.12f, 0.48f, 0.56f, 1f));

        ImGui.SetCursorPos(new Vector2(padding, padding));
        for (var i = 0; i < options.Count; i++)
        {
            if (i > 0)
                ImGui.SameLine();

            var (role, label) = options[i];
            if (!this.DrawOverlayControlButton(iconWindow.Role == role, label))
                continue;

            iconWindow.Role = role;
            this.QueueConfigSave();
        }

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);
        ImGui.PopID();
        ImGui.End();
    }

    private void DrawOverlayDisplayConditionControl(IconWindowConfig iconWindow, Vector2 areaOrigin, Vector2 areaSize)
    {
        const float padding = 6f;
        var framePadding = new Vector2(7f, 3f);
        var options = DisplayConditionOptions().ToList();
        var currentLabel = options.FirstOrDefault(option => option.Condition == iconWindow.DisplayCondition).Label ?? "항상";
        var comboWidth = Math.Max(118f, options.Max(option => ImGui.CalcTextSize(option.Label).X) + framePadding.X * 2f + 26f);
        var windowWidth = comboWidth + padding * 2f;
        var windowHeight = ImGui.GetTextLineHeight() + framePadding.Y * 2f + padding * 2f;
        var windowX = Math.Max(areaOrigin.X, areaOrigin.X + areaSize.X - windowWidth);
        var windowY = Math.Max(0f, areaOrigin.Y - windowHeight - 4f);

        ImGui.SetNextWindowPos(new Vector2(windowX, windowY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);
        if (!ImGui.Begin($"FFXIVAuraOverlayConditionControl-{iconWindow.Id}", OverlayControlWindowFlags()))
        {
            ImGui.End();
            return;
        }

        ImGui.PushID($"overlay-condition-control-{iconWindow.Id}");
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
        ImGui.SetCursorPos(new Vector2(padding, padding));
        ImGui.SetNextItemWidth(comboWidth);
        if (ImGui.BeginCombo("##condition", currentLabel))
        {
            foreach (var (condition, label) in options)
            {
                var selected = iconWindow.DisplayCondition == condition;
                if (ImGui.Selectable(label, selected))
                {
                    iconWindow.DisplayCondition = condition;
                    this.QueueConfigSave();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.PopStyleVar();
        ImGui.PopID();
        ImGui.End();
    }

    private void DrawOverlayAlignmentControls(IconWindowConfig iconWindow, string job, uint level, Vector2 areaOrigin, Vector2 areaSize)
    {
        const float padding = 6f;
        const string leftLabel = "왼쪽";
        const string centerLabel = "가운데";
        const string rightLabel = "오른쪽";
        var framePadding = new Vector2(7f, 3f);
        var itemSpacing = new Vector2(4f, 0f);

        var leftWidth = ImGui.CalcTextSize(leftLabel).X + framePadding.X * 2f;
        var centerWidth = ImGui.CalcTextSize(centerLabel).X + framePadding.X * 2f;
        var rightWidth = ImGui.CalcTextSize(rightLabel).X + framePadding.X * 2f;
        var contentWidth = leftWidth + centerWidth + rightWidth + itemSpacing.X * 2f;
        var windowWidth = contentWidth + padding * 2f;
        var windowHeight = ImGui.GetTextLineHeight() + framePadding.Y * 2f + padding * 2f;
        var windowX = Math.Max(areaOrigin.X, areaOrigin.X + areaSize.X - windowWidth);
        var windowY = areaOrigin.Y + areaSize.Y + 4f;

        ImGui.SetNextWindowPos(new Vector2(windowX, windowY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);
        if (!ImGui.Begin($"FFXIVAuraOverlayControls-{iconWindow.Id}", OverlayControlWindowFlags()))
        {
            ImGui.End();
            return;
        }

        ImGui.PushID($"overlay-controls-{iconWindow.Id}");
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, itemSpacing);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.05f, 0.08f, 0.1f, 0.82f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.08f, 0.32f, 0.38f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.12f, 0.48f, 0.56f, 1f));

        ImGui.SetCursorPos(new Vector2(padding, padding));

        if (this.DrawOverlayControlButton(iconWindow.Alignment == IconAlignment.Left, leftLabel))
            this.ApplyOverlayAlignment(iconWindow, job, level, IconAlignment.Left);

        ImGui.SameLine();
        if (this.DrawOverlayControlButton(iconWindow.Alignment == IconAlignment.Center, centerLabel))
            this.ApplyOverlayAlignment(iconWindow, job, level, IconAlignment.Center);

        ImGui.SameLine();
        if (this.DrawOverlayControlButton(iconWindow.Alignment == IconAlignment.Right, rightLabel))
            this.ApplyOverlayAlignment(iconWindow, job, level, IconAlignment.Right);

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);
        ImGui.PopID();
        ImGui.End();
    }

    private static ImGuiWindowFlags OverlayControlWindowFlags()
        => ImGuiWindowFlags.NoTitleBar
           | ImGuiWindowFlags.NoScrollbar
           | ImGuiWindowFlags.NoSavedSettings
           | ImGuiWindowFlags.NoDecoration
           | ImGuiWindowFlags.NoMove;

    private bool DrawOverlayControlButton(bool selected, string label)
    {
        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.14f, 0.5f, 0.58f, 0.96f));

        var clicked = ImGui.Button(label);
        if (selected)
            ImGui.PopStyleColor();

        return clicked;
    }

    private void ApplyOverlayAlignment(IconWindowConfig iconWindow, string job, uint level, IconAlignment alignment)
    {
        iconWindow.Alignment = alignment;
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
            this.AlignOverlayIcons(iconWindow, job, level);
        else
            this.AlignAuraIcons(iconWindow);

        this.QueueConfigSave();
    }

    private Vector2 GetOverlayIconPosition(IconWindowConfig iconWindow, string job, AbilityDefinition ability, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        if (iconWindow.IconPositionsByJob.TryGetValue(job, out var positions)
            && positions.TryGetValue(ability.Id, out var saved))
        {
            return this.ClampOverlayIconPosition(saved, areaSize, iconSize);
        }

        return this.GetOverlayAutoPosition(iconWindow, index, visibleCount, areaSize, iconSize, gap);
    }

    private Vector2 GetOverlayAutoPosition(IconWindowConfig iconWindow, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
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
        var x = this.GetAlignedRowStartX(iconWindow, areaSize.X, rowWidth) + column * cell;
        var y = Math.Max(0f, MathF.Round((areaSize.Y - blockHeight) * 0.5f)) + row * cell;
        return this.ClampOverlayIconPosition(new Vector2(x, y), areaSize, iconSize);
    }

    private float GetAlignedRowStartX(IconWindowConfig iconWindow, float areaWidth, float rowWidth)
    {
        var remaining = Math.Max(0f, areaWidth - rowWidth);
        return iconWindow.Alignment switch
        {
            IconAlignment.Left => 0f,
            IconAlignment.Right => MathF.Round(remaining),
            _ => MathF.Round(remaining * 0.5f),
        };
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

        var dragId = $"{iconWindow.Id}:{job}:{ability.Id}";
        ImGui.SetCursorScreenPos(iconPos);
        ImGui.InvisibleButton($"##overlay-drag-{ability.Id}", new Vector2(iconSize, iconSize));

        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            this.UntrackAbility(iconWindow, job, ability.Id);
            this.QueueConfigSave();
            return;
        }

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 2f))
        {
            if (!string.Equals(this.draggedOverlayId, dragId, StringComparison.OrdinalIgnoreCase))
            {
                this.draggedOverlayId = dragId;
                this.draggedOverlayMouseStart = ImGui.GetMousePos();
                this.draggedOverlayPositionStart = localPos;
            }

            var next = this.ClampOverlayIconPosition(
                this.draggedOverlayPositionStart + ImGui.GetMousePos() - this.draggedOverlayMouseStart,
                areaSize,
                iconSize);
            this.SetOverlayIconPosition(iconWindow, job, ability.Id, next);
            this.QueueConfigSave();
        }

        if (string.Equals(this.draggedOverlayId, dragId, StringComparison.OrdinalIgnoreCase))
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

    private void HandleAuraIconInteraction(IconWindowConfig iconWindow, AuraState aura, Vector2 localPos, Vector2 iconPos, Vector2 areaSize, float iconSize)
    {
        if (this.config.LockOverlay)
            return;

        var id = GetAuraPositionKey(aura.StatusId);
        var dragId = $"{iconWindow.Id}:{id}";
        ImGui.SetCursorScreenPos(iconPos);
        ImGui.InvisibleButton($"##aura-drag-{iconWindow.Id}-{id}", new Vector2(iconSize, iconSize));

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 2f))
        {
            if (!string.Equals(this.draggedOverlayId, dragId, StringComparison.OrdinalIgnoreCase))
            {
                this.draggedOverlayId = dragId;
                this.draggedOverlayMouseStart = ImGui.GetMousePos();
                this.draggedOverlayPositionStart = localPos;
            }

            var next = this.ClampOverlayIconPosition(
                this.draggedOverlayPositionStart + ImGui.GetMousePos() - this.draggedOverlayMouseStart,
                areaSize,
                iconSize);
            this.SetAuraIconPosition(iconWindow, aura.StatusId, next);
            this.QueueConfigSave();
        }

        if (string.Equals(this.draggedOverlayId, dragId, StringComparison.OrdinalIgnoreCase))
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(new Vector4(0.45f, 0.72f, 1f, 0.95f)), 3f, ImDrawFlags.None, 2f);
        }
    }

    private void SetAuraIconPosition(IconWindowConfig iconWindow, uint statusId, Vector2 position)
    {
        var groupKey = GetAuraPositionGroupKey(iconWindow);
        if (!iconWindow.AuraPositionsByRole.TryGetValue(groupKey, out var positions))
        {
            positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            iconWindow.AuraPositionsByRole[groupKey] = positions;
        }

        positions[GetAuraPositionKey(statusId)] = position;
    }

    private static string GetAuraPositionGroupKey(IconWindowConfig iconWindow)
        => $"{iconWindow.Id}:{iconWindow.Role}";

    private static string GetAuraPositionKey(uint statusId)
        => $"status-{statusId}";

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
            var startX = this.GetAlignedRowStartX(iconWindow, areaSize.X, rowWidth);
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

    private void AlignAuraIcons(IconWindowConfig iconWindow)
    {
        var statusIds = iconWindow.TrackedStatusIds
            .Distinct()
            .ToList();
        if (statusIds.Count == 0)
            return;

        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        var positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < statusIds.Count; index++)
        {
            var position = this.GetOverlayAutoPosition(iconWindow, index, statusIds.Count, areaSize, iconWindow.IconSize, iconWindow.Gap);
            positions[GetAuraPositionKey(statusIds[index])] = position;
        }

        iconWindow.AuraPositionsByRole[GetAuraPositionGroupKey(iconWindow)] = positions;
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
