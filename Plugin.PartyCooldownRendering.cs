namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawPartyCooldownWindowContent(IconWindowConfig iconWindow, string job, uint level)
    {
        var frameModelProfileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.FrameModel);
        var rows = this.BuildPartyCooldownRows(iconWindow, level);
        this.performanceProfiler.EndSection(PerformanceProfileSection.FrameModel, frameModelProfileStart);
        var itemCount = 0;
        foreach (var row in rows)
            itemCount += row.Items.Count;
        this.performanceStats.CountOverlayWindow(itemCount, 0);

        if (itemCount == 0 && this.config.LockOverlay)
            return;

        if (this.EnsurePartyCooldownBoardHeight(iconWindow, rows))
            this.QueueConfigSave();

        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        var windowSize = areaSize;
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

        var areaOrigin = ImGui.GetCursorScreenPos();
        if (!this.config.LockOverlay)
            this.DrawOverlayEditStage(ImGui.GetWindowDrawList(), areaOrigin, areaOrigin + areaSize);

        this.DrawPartyCooldownRows(iconWindow, rows, areaOrigin, areaSize);

        if (!this.config.LockOverlay)
            this.HandleOverlayResize(iconWindow, job, Array.Empty<AbilityDefinition>(), Array.Empty<AuraState>(), areaOrigin, areaSize);

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

    private bool EnsurePartyCooldownBoardHeight(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows)
    {
        var iconSize = iconWindow.IconSize;
        var gap = Math.Max(2f, iconWindow.Gap);
        var padding = Math.Max(4f, gap);
        var labelGap = Math.Max(4f, gap);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var labelWidth = jobIconSize + labelGap + nameWidth + labelGap;
        var iconAreaWidth = Math.Max(0f, iconWindow.Width - padding * 2f - labelWidth);
        var iconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(iconAreaWidth, iconSize, gap);
        var nextHeight = PartyCooldownBoardLayout.GetExpandedBoardHeight(
            iconWindow.Height,
            rows.Select(row => row.Items.Count),
            iconSize,
            gap,
            padding,
            iconsPerLine,
            MinOverlayHeight,
            MaxOverlayHeight);

        if (nextHeight <= iconWindow.Height + 0.5f)
            return false;

        iconWindow.Height = nextHeight;
        this.SetBugDiagnosticEvent($"partyCooldownHeightExpanded:{iconWindow.Id}:{nextHeight:0}");
        return true;
    }

    private void DrawPartyCooldownRows(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        Vector2 areaOrigin,
        Vector2 areaSize)
    {
        var draw = ImGui.GetWindowDrawList();
        var iconSize = iconWindow.IconSize;
        var gap = Math.Max(2f, iconWindow.Gap);
        var padding = Math.Max(4f, gap);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var labelGap = Math.Max(4f, gap);
        var labelWidth = jobIconSize + labelGap + nameWidth + labelGap;
        var rowStartX = areaOrigin.X + padding;
        var iconAreaStartX = rowStartX + labelWidth;
        var iconAreaWidth = Math.Max(0f, areaSize.X - padding * 2f - labelWidth);
        var iconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(iconAreaWidth, iconSize, gap);
        var rowY = areaOrigin.Y + padding;

        draw.PushClipRect(areaOrigin, areaOrigin + areaSize, true);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var contentHeight = PartyCooldownBoardLayout.GetRowContentHeight(row.Items.Count, iconSize, gap, iconsPerLine);
            if (rowY > areaOrigin.Y + areaSize.Y)
                break;

            var cursor = new Vector2(rowStartX, rowY + Math.Max(0f, (contentHeight - jobIconSize) * 0.5f));
            this.DrawPartyCooldownJobBadge(draw, row.Member, cursor, jobIconSize);

            cursor.X += jobIconSize + labelGap;
            var namePos = new Vector2(cursor.X, rowY + Math.Max(0f, (contentHeight - ImGui.GetTextLineHeight()) * 0.5f));
            this.DrawOutlinedText(
                draw,
                namePos,
                row.Member.ShortName,
                new Vector4(1f, 1f, 1f, 0.96f),
                new Vector4(0f, 0f, 0f, 0.9f),
                1f);

            var lineCount = PartyCooldownBoardLayout.GetLineCount(row.Items.Count, iconsPerLine);
            for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                var lineItemCount = PartyCooldownBoardLayout.GetLineItemCount(row.Items.Count, lineIndex, iconsPerLine);
                var lineStartX = iconAreaStartX + PartyCooldownBoardLayout.GetLineStartOffset(iconWindow.Alignment, lineItemCount, iconSize, gap, iconsPerLine);
                var lineY = rowY + lineIndex * (iconSize + gap);

                for (var column = 0; column < lineItemCount; column++)
                {
                    var itemIndex = PartyCooldownBoardLayout.GetLineStartIndex(lineIndex, iconsPerLine) + column;
                    if (itemIndex >= row.Items.Count)
                        break;

                    var iconPos = new Vector2(lineStartX + column * (iconSize + gap), lineY);
                    if (iconPos.X + iconSize > areaOrigin.X + areaSize.X)
                        break;

                    ImGui.SetCursorScreenPos(iconPos);
                    this.DrawPartyCooldownIcon(row.Items[itemIndex], iconSize);
                    this.HandlePartyCooldownIconInteraction(row.Items[itemIndex], iconPos, iconSize);
                }
            }

            rowY += contentHeight + gap;
        }

        draw.PopClipRect();
    }

    private void DrawPartyCooldownJobBadge(ImDrawListPtr draw, PartyCooldownMemberSnapshot member, Vector2 pos, float size)
    {
        var max = pos + new Vector2(size, size);
        draw.AddRectFilled(pos, max, ImGui.GetColorU32(new Vector4(0.02f, 0.04f, 0.05f, 0.82f)), 4f);
        if (member.JobIconId > 0)
        {
            var lookup = new GameIconLookup(member.JobIconId, false, true, null);
            var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
            ImGui.SetCursorScreenPos(pos);
            ImGui.Image(texture.Handle, new Vector2(size, size));
        }

        draw.AddRect(pos, max, ImGui.GetColorU32(new Vector4(0.45f, 0.9f, 0.98f, 0.7f)), 4f, ImDrawFlags.None, 1f);
    }

    private void DrawPartyCooldownIcon(PartyCooldownDisplayItem item, float size)
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AbilityIcon);
        try
        {
            var iconId = item.Definition.IconId;
            var lookup = new GameIconLookup(iconId, false, true, null);
            var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
            var pos = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            var max = pos + new Vector2(size, size);
            var grayscaleTexture = item.State == PartyCooldownDisplayState.Cooldown ? this.GetGrayscaleIconTexture(iconId) : null;

            ImGui.Image((grayscaleTexture ?? texture).Handle, new Vector2(size, size));
            if (item.State == PartyCooldownDisplayState.Cooldown && grayscaleTexture is null)
                this.DrawUnavailableIconTint(draw, pos, max);

            if (item.State == PartyCooldownDisplayState.Cooldown)
            {
                var elapsedRatio = item.CooldownTotal <= 0f ? 1f : 1f - (item.CooldownRemaining / item.CooldownTotal);
                this.DrawCooldownCover(draw, pos, max, elapsedRatio);
                this.DrawTimerText(draw, pos, max, item.CooldownRemaining);
            }
            else if (item.State == PartyCooldownDisplayState.Active)
            {
                draw.AddRect(pos - new Vector2(1f, 1f), max + new Vector2(1f, 1f), ImGui.GetColorU32(new Vector4(0.3f, 0.95f, 1f, 1f)), 4f, ImDrawFlags.None, 2.4f);
                this.DrawTimerText(draw, pos, max, item.ActiveRemaining);
            }

            draw.AddRect(pos, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.62f)), 3f, ImDrawFlags.None, 1f);
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AbilityIcon, profileStart);
        }
    }

    private void HandlePartyCooldownIconInteraction(PartyCooldownDisplayItem item, Vector2 iconPos, float iconSize)
    {
        var iconMax = iconPos + new Vector2(iconSize, iconSize);
        if (this.config.LockOverlay)
        {
            if (IsMouseInRect(iconPos, iconMax))
            {
                this.RecordTooltipHover(TooltipDiagnosticKind.PartyCooldown, item.Definition.Id, item.Definition.ActionId, iconPos, iconMax, imguiHovered: false);
                this.ShowPartyCooldownTooltip(item);
            }

            return;
        }

        ImGui.SetCursorScreenPos(iconPos);
        ImGui.InvisibleButton($"##party-cooldown-{item.Definition.Id}-{item.Definition.ActionId}", new Vector2(iconSize, iconSize));
        var hovered = ImGui.IsItemHovered() && !ImGui.IsItemActive();
        if (hovered)
        {
            this.RecordTooltipHover(TooltipDiagnosticKind.PartyCooldown, item.Definition.Id, item.Definition.ActionId, iconPos, iconMax, imguiHovered: true);
            this.ShowPartyCooldownTooltip(item);
        }
    }

    private void ShowPartyCooldownTooltip(PartyCooldownDisplayItem item)
    {
        if (!this.config.ShowTooltips)
        {
            this.tooltipDiagnostics.RecordDisabledSkip(TooltipDiagnosticKind.PartyCooldown, item.Definition.Id, item.Definition.ActionId);
            return;
        }

        this.tooltipDiagnostics.RecordTooltipRequest(TooltipDiagnosticKind.PartyCooldown, item.Definition.Id, item.Definition.ActionId);
        this.SetBugDiagnosticEvent($"tooltipPartyCooldown:{item.Definition.Id}:{item.Definition.ActionId}");
        this.ShowNativeActionTooltip(item.Definition.ActionId);
    }
}
