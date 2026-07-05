namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawOverlay()
    {
        var job = JobInfo.Code(PlayerState.ClassJob.RowId);
        var level = (uint)(PlayerState.EffectiveLevel > 0 ? PlayerState.EffectiveLevel : PlayerState.Level);
        if (this.EnsureIconWindows())
            this.QueueConfigSave();

        foreach (var iconWindow in this.config.IconWindows)
            this.DrawIconWindow(iconWindow, job, level);
    }

    private void DrawIconWindow(IconWindowConfig iconWindow, string job, uint level)
    {
        var frameItems = this.GetOverlayFrameItems(iconWindow, job, level);
        var displayItems = frameItems.Display;
        var layoutItems = frameItems.Layout;
        var visible = displayItems.Abilities;
        var auras = displayItems.Auras;
        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        if (this.EnsureOverlayPositionsForItems(iconWindow, job, level, layoutItems, areaSize))
            this.QueueConfigSave();

        if (visible.Count == 0 && auras.Count == 0 && this.config.LockOverlay)
            return;

        var windowSize = new Vector2(iconWindow.Width, iconWindow.Height);
        var clampedPosition = ClampOverlayWindowPosition(iconWindow.Position, windowSize);
        var positionWasClamped = Vector2.DistanceSquared(iconWindow.Position, clampedPosition) > 0.25f;
        if (positionWasClamped)
        {
            iconWindow.Position = clampedPosition;
            this.QueueConfigSave();
        }

        ImGui.SetNextWindowPos(iconWindow.Position, positionWasClamped ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(0f);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
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
            this.HandleOverlayIconInteraction(iconWindow, job, level, ability, localPos, iconPos, areaSize, iconSize);
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

        if (!this.config.LockOverlay)
            this.HandleOverlayResize(iconWindow, job, layoutItems.Abilities, layoutItems.Auras, areaOrigin, areaSize);

        ImGui.End();
        ImGui.PopStyleVar();

        if (!this.config.LockOverlay)
        {
            var controlLayout = this.GetOverlayControlLayout(iconWindow.Role, areaOrigin, areaSize);
            this.DrawOverlayRoleControls(iconWindow, job, level, areaOrigin, areaSize, controlLayout);
            this.DrawOverlayDisplayConditionControl(iconWindow, areaOrigin, areaSize, controlLayout);
            this.DrawOverlayNameControl(iconWindow, areaOrigin, areaSize, controlLayout);
            this.DrawOverlayAlignmentControls(iconWindow, job, level, areaOrigin, areaSize, controlLayout);
        }
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

    private void HandleOverlayResize(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<AbilityDefinition> visible,
        IReadOnlyList<AuraState> auras,
        Vector2 areaOrigin,
        Vector2 areaSize)
    {
        const float handleSize = 18f;

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
        var nextWidth = Math.Clamp(iconWindow.Width + delta.X, MinOverlayWidth, MaxOverlayWidth);
        var nextHeight = Math.Clamp(iconWindow.Height + delta.Y, MinOverlayHeight, MaxOverlayHeight);
        if (Math.Abs(nextWidth - iconWindow.Width) <= 0.1f && Math.Abs(nextHeight - iconWindow.Height) <= 0.1f)
            return;

        iconWindow.Width = nextWidth;
        iconWindow.Height = nextHeight;
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
            this.NormalizeIconPositionsAfterResize(iconWindow, job, visible, new Vector2(nextWidth, nextHeight));
        else
            this.NormalizeAuraIconPositionsAfterResize(iconWindow, auras, new Vector2(nextWidth, nextHeight));

        this.QueueConfigSave();
    }


    private void HandleOverlayIconInteraction(IconWindowConfig iconWindow, string job, uint level, AbilityDefinition ability, Vector2 localPos, Vector2 iconPos, Vector2 areaSize, float iconSize)
    {
        if (this.config.LockOverlay)
            return;

        var dragId = RuntimeScopeKeys.AbilityDrag(iconWindow.Id, job, ability.Id);
        ImGui.SetCursorScreenPos(iconPos);
        ImGui.InvisibleButton($"##overlay-drag-{ability.Id}", new Vector2(iconSize, iconSize));

        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            this.UntrackAbilityFromOverlay(iconWindow, job, ability.Id);
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

    private void HandleAuraIconInteraction(IconWindowConfig iconWindow, AuraState aura, Vector2 localPos, Vector2 iconPos, Vector2 areaSize, float iconSize)
    {
        if (this.config.LockOverlay || UsesCompactAuraLayout(iconWindow))
            return;

        var id = OverlayPositionKeys.Aura(aura.StatusId);
        var dragId = RuntimeScopeKeys.AuraDrag(iconWindow.Id, aura.StatusId);
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

}
