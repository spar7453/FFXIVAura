namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawOverlay()
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.Overlay);
        try
        {
            var job = JobInfo.Code(PlayerState.ClassJob.RowId);
            var level = (uint)(PlayerState.EffectiveLevel > 0 ? PlayerState.EffectiveLevel : PlayerState.Level);
            foreach (var iconWindow in this.config.IconWindows)
                this.DrawIconWindow(iconWindow, job, level);
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.Overlay, profileStart);
        }
    }

    private void DrawIconWindow(IconWindowConfig iconWindow, string job, uint level)
    {
        var profileLabel = GetIconWindowDisplayName(iconWindow);
        var profileStart = this.performanceProfiler.BeginWindow(iconWindow.Id, profileLabel);
        try
        {
            this.DrawIconWindowContent(iconWindow, job, level);
        }
        finally
        {
            this.performanceProfiler.EndWindow(iconWindow.Id, profileLabel, profileStart);
        }
    }

    private void DrawIconWindowContent(IconWindowConfig iconWindow, string job, uint level)
    {
        if (IconWindowRoles.IsPartyCooldownRole(iconWindow.Role))
        {
            this.DrawPartyCooldownWindowContent(iconWindow, job, level);
            return;
        }

        var frameModelProfileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.FrameModel);
        var frame = this.BuildOverlayFrameModel(iconWindow, job, level);
        this.performanceProfiler.EndSection(PerformanceProfileSection.FrameModel, frameModelProfileStart);
        this.performanceStats.CountOverlayWindow(frame.DisplayAbilities.Count, frame.DisplayAuras.Count);

        var positioningProfileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.Positioning);
        var positionsChanged = this.EnsureOverlayPositionsForItems(iconWindow, job, level, frame.Layout, frame.AreaSize);
        this.performanceProfiler.EndSection(PerformanceProfileSection.Positioning, positioningProfileStart);
        if (positionsChanged)
            this.QueueConfigSave();

        if (!frame.HasDisplayItems && this.config.LockOverlay)
        {
            this.RememberOverlayWindowDiagnostics(iconWindow, frame);
            return;
        }

        var visible = frame.DisplayAbilities;
        var auras = frame.DisplayAuras;
        var areaSize = frame.AreaSize;
        var windowSize = frame.AreaSize;
        var clampedPosition = ClampOverlayWindowPosition(iconWindow.Position, windowSize);
        var positionWasClamped = Vector2.DistanceSquared(iconWindow.Position, clampedPosition) > 0.25f;
        if (positionWasClamped)
        {
            iconWindow.Position = clampedPosition;
            this.SetBugDiagnosticEvent($"windowClamped:{iconWindow.Id}");
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

        this.SelectIconWindowFromOverlayClick(iconWindow);

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
            this.HandleOverlayIconInteraction(iconWindow, job, level, ability, localPos, iconPos, areaSize, iconSize, visible.Count);
        }

        for (var i = 0; i < auras.Count; i++)
        {
            var aura = auras[i];
            var localPos = this.GetAuraIconPosition(iconWindow, aura, i, auras.Count, areaSize, iconSize, gap);
            var iconPos = areaOrigin + localPos;
            ImGui.SetCursorScreenPos(iconPos);
            this.DrawAuraIcon(aura, iconSize, iconWindow);
            this.HandleAuraIconInteraction(iconWindow, aura, localPos, iconPos, areaSize, iconSize, auras.Count);
        }

        this.RememberOverlayWindowDiagnostics(iconWindow, frame);

        if (!this.config.LockOverlay)
            this.HandleOverlayResize(iconWindow, job, frame.LayoutAbilities, frame.LayoutAuras, areaOrigin, areaSize);

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

    private void SelectIconWindowFromOverlayClick(IconWindowConfig iconWindow)
    {
        if (!OverlayWindowSelection.ShouldSelect(
                this.config.LockOverlay,
                ImGui.IsWindowHovered(),
                ImGui.IsMouseClicked(ImGuiMouseButton.Left),
                this.config.ActiveWindowId,
                iconWindow.Id))
        {
            return;
        }

        this.config.ActiveWindowId = iconWindow.Id;
        this.SetBugDiagnosticEvent($"windowSelected:{iconWindow.Id}");
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

    private void HandleOverlayResize(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<AbilityDefinition> visible,
        IReadOnlyList<AuraState> auras,
        Vector2 areaOrigin,
        Vector2 areaSize,
        IconWindowLayoutBinding? layoutBinding = null)
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
        var currentWidth = layoutBinding?.Width ?? iconWindow.Width;
        var currentHeight = layoutBinding?.Height ?? iconWindow.Height;
        var nextWidth = Math.Clamp(Math.Max(currentWidth, areaSize.X) + delta.X, MinOverlayWidth, MaxOverlayWidth);
        var nextHeight = Math.Clamp(Math.Max(currentHeight, areaSize.Y) + delta.Y, MinOverlayHeight, MaxOverlayHeight);
        if (Math.Abs(nextWidth - currentWidth) <= 0.1f && Math.Abs(nextHeight - currentHeight) <= 0.1f)
            return;

        if (layoutBinding is null)
        {
            iconWindow.Width = nextWidth;
            iconWindow.Height = nextHeight;
        }
        else
        {
            var activeLayout = layoutBinding.Value;
            activeLayout.Width = nextWidth;
            activeLayout.Height = nextHeight;
        }
        this.SetBugDiagnosticEvent($"windowResized:{iconWindow.Id}:{nextWidth:0}x{nextHeight:0}");
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
            this.NormalizeIconPositionsAfterResize(iconWindow, job, visible, new Vector2(nextWidth, nextHeight));
        else if (IconWindowRoles.IsStandardAuraRole(iconWindow.Role))
            this.NormalizeAuraIconPositionsAfterResize(iconWindow, auras, new Vector2(nextWidth, nextHeight));

        this.QueueConfigSave();
    }


    private void HandleOverlayIconInteraction(IconWindowConfig iconWindow, string job, uint level, AbilityDefinition ability, Vector2 localPos, Vector2 iconPos, Vector2 areaSize, float iconSize, int drawnIconCount)
    {
        var iconMax = iconPos + new Vector2(iconSize, iconSize);
        if (this.config.LockOverlay)
        {
            if (IsMouseInRect(iconPos, iconMax))
            {
                this.RecordTooltipHover(TooltipDiagnosticKind.Ability, iconWindow.Id, ability.Id, ability.ActionId, drawnIconCount, hitboxExpanded: false, iconPos, iconMax, imguiHovered: false);
                this.RegisterAbilityTooltipCandidate(ability);
            }

            return;
        }

        var dragId = RuntimeScopeKeys.AbilityDrag(iconWindow.Id, job, ability.Id);
        ImGui.SetCursorScreenPos(iconPos);
        ImGui.InvisibleButton($"##overlay-drag-{ability.Id}", new Vector2(iconSize, iconSize));
        var hovered = ImGui.IsItemHovered() && !ImGui.IsItemActive();
        if (hovered)
        {
            this.RecordTooltipHover(TooltipDiagnosticKind.Ability, iconWindow.Id, ability.Id, ability.ActionId, drawnIconCount, hitboxExpanded: false, iconPos, iconMax, imguiHovered: true);
            this.RegisterAbilityTooltipCandidate(ability);
        }

        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyCtrl)
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

    private void ShowAbilityTooltip(AbilityDefinition ability)
    {
        if (!this.config.ShowTooltips)
        {
            this.tooltipDiagnostics.RecordDisabledSkip(TooltipDiagnosticKind.Ability, ability.Id, ability.ActionId);
            return;
        }

        var state = this.GetCooldown(ability);
        var actionId = state.DisplayActionId > 0 ? state.DisplayActionId : ability.ActionId;
        this.tooltipDiagnostics.RecordTooltipRequest(TooltipDiagnosticKind.Ability, ability.Id, actionId);
        this.SetBugDiagnosticEvent($"tooltipAction:{ability.Id}:{state.DisplayActionId}");
        this.ShowOverlayActionTooltip(actionId, ability.Name, ability.IconId, string.Empty);
    }

    private static bool IsMouseInRect(Vector2 min, Vector2 max)
    {
        var mouse = ImGui.GetMousePos();
        return mouse.X >= min.X
               && mouse.X <= max.X
               && mouse.Y >= min.Y
               && mouse.Y <= max.Y;
    }

    private void HandleAuraIconInteraction(IconWindowConfig iconWindow, AuraState aura, Vector2 localPos, Vector2 iconPos, Vector2 areaSize, float iconSize, int drawnIconCount)
    {
        var iconMax = iconPos + new Vector2(iconSize, iconSize);
        if (this.config.LockOverlay || UsesCompactAuraLayout(iconWindow))
        {
            if (IsMouseInRect(iconPos, iconMax))
            {
                this.RecordTooltipHover(TooltipDiagnosticKind.Aura, iconWindow.Id, aura.StatusId.ToString(), 0, drawnIconCount, hitboxExpanded: false, iconPos, iconMax, imguiHovered: false);
                this.RegisterAuraTooltipCandidate(aura);
            }

            return;
        }

        var id = OverlayPositionKeys.Aura(aura.StatusId);
        var dragId = RuntimeScopeKeys.AuraDrag(iconWindow.Id, aura.StatusId);
        ImGui.SetCursorScreenPos(iconPos);
        ImGui.InvisibleButton($"##aura-drag-{iconWindow.Id}-{id}", new Vector2(iconSize, iconSize));
        var hovered = ImGui.IsItemHovered() && !ImGui.IsItemActive();
        if (hovered)
        {
            this.RecordTooltipHover(TooltipDiagnosticKind.Aura, iconWindow.Id, aura.StatusId.ToString(), 0, drawnIconCount, hitboxExpanded: false, iconPos, iconMax, imguiHovered: true);
            this.RegisterAuraTooltipCandidate(aura);
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

    private void RegisterAbilityTooltipCandidate(AbilityDefinition ability)
    {
        this.overlayTooltipResolver.Register(OverlayTooltipCandidate.ForAbility(ability));
    }

    private void RegisterAuraTooltipCandidate(AuraState aura)
    {
        this.overlayTooltipResolver.Register(OverlayTooltipCandidate.ForAura(aura));
    }

    private void RegisterPartyCooldownTooltipCandidate(PartyCooldownDefinition definition)
    {
        this.overlayTooltipResolver.Register(OverlayTooltipCandidate.ForPartyCooldown(definition));
    }

    private void RecordTooltipHover(
        TooltipDiagnosticKind kind,
        string windowId,
        string id,
        uint actionId,
        int drawnIconCount,
        bool hitboxExpanded,
        Vector2 iconPos,
        Vector2 iconMax,
        bool imguiHovered)
    {
        this.tooltipDiagnostics.RecordHover(
            kind,
            windowId,
            id,
            actionId,
            drawnIconCount,
            hitboxExpanded,
            ImGui.GetMousePos(),
            iconPos,
            iconMax,
            IsMouseInRect(iconPos, iconMax),
            imguiHovered);
    }

    private void ShowDeferredOverlayTooltip()
    {
        if (!this.overlayTooltipResolver.TryConsume(out var candidate))
            return;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.TooltipRendering);
        try
        {
            switch (candidate.Kind)
            {
                case OverlayTooltipCandidateKind.Ability when candidate.Ability is not null:
                    this.ShowAbilityTooltip(candidate.Ability);
                    break;
                case OverlayTooltipCandidateKind.Aura:
                    this.ShowAuraTooltip(candidate.Aura);
                    break;
                case OverlayTooltipCandidateKind.PartyCooldown when candidate.PartyCooldown is not null:
                    this.ShowPartyCooldownTooltip(candidate.PartyCooldown);
                    break;
            }
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.TooltipRendering, profileStart);
        }
    }

}
