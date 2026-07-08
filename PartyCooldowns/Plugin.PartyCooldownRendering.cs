namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private readonly record struct PartyCooldownBoardRenderMetrics(
        float IconSize,
        float Gap,
        float Padding,
        float LabelGap,
        float JobIconSize,
        float AllianceGroupWidth,
        float LabelWidth,
        int IconsPerLine,
        int DrawnIconCount);

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
        var metrics = GetPartyCooldownBoardRenderMetrics(iconWindow, rows);
        var nextHeight = GetPartyCooldownExpandedBoardHeight(iconWindow.Height, rows, metrics);

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
        var metrics = GetPartyCooldownBoardRenderMetrics(iconWindow, rows);
        var iconSize = metrics.IconSize;
        var gap = metrics.Gap;
        var rowStartX = areaOrigin.X + metrics.Padding;
        var iconAreaStartX = rowStartX + metrics.LabelWidth;
        var rowY = areaOrigin.Y + metrics.Padding;

        draw.PushClipRect(areaOrigin, areaOrigin + areaSize, true);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var contentHeight = PartyCooldownBoardLayout.GetRowContentHeight(row.Items.Count, iconSize, gap, metrics.IconsPerLine);
            if (rowY > areaOrigin.Y + areaSize.Y)
                break;

            var cursor = new Vector2(rowStartX, rowY + Math.Max(0f, (contentHeight - metrics.JobIconSize) * 0.5f));
            if (metrics.AllianceGroupWidth > 0f)
            {
                var groupTextSize = ImGui.CalcTextSize(row.Member.AllianceGroup);
                var groupTextPos = new Vector2(
                    cursor.X + Math.Max(0f, metrics.AllianceGroupWidth - metrics.LabelGap - groupTextSize.X) * 0.5f,
                    rowY + Math.Max(0f, (contentHeight - ImGui.GetTextLineHeight()) * 0.5f));
                this.DrawOutlinedText(
                    draw,
                    groupTextPos,
                    row.Member.AllianceGroup,
                    new Vector4(0.72f, 0.95f, 1f, 0.98f),
                    new Vector4(0f, 0f, 0f, 0.9f),
                    1f);
                cursor.X += metrics.AllianceGroupWidth;
            }

            this.DrawPartyCooldownJobBadge(draw, row.Member, cursor, metrics.JobIconSize);

            cursor.X += metrics.JobIconSize + metrics.LabelGap;
            var namePos = new Vector2(cursor.X, rowY + Math.Max(0f, (contentHeight - ImGui.GetTextLineHeight()) * 0.5f));
            this.DrawOutlinedText(
                draw,
                namePos,
                row.Member.ShortName,
                new Vector4(1f, 1f, 1f, 0.96f),
                new Vector4(0f, 0f, 0f, 0.9f),
                1f);

            var lineCount = PartyCooldownBoardLayout.GetLineCount(row.Items.Count, metrics.IconsPerLine);
            for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                var lineItemCount = PartyCooldownBoardLayout.GetLineItemCount(row.Items.Count, lineIndex, metrics.IconsPerLine);
                var lineStartX = iconAreaStartX + PartyCooldownBoardLayout.GetLineStartOffset(iconWindow.Alignment, lineItemCount, iconSize, gap, metrics.IconsPerLine);
                var lineY = rowY + lineIndex * (iconSize + gap);

                for (var column = 0; column < lineItemCount; column++)
                {
                    var itemIndex = PartyCooldownBoardLayout.GetLineStartIndex(lineIndex, metrics.IconsPerLine) + column;
                    if (itemIndex >= row.Items.Count)
                        break;

                    var iconPos = new Vector2(lineStartX + column * (iconSize + gap), lineY);
                    if (iconPos.X + iconSize > areaOrigin.X + areaSize.X)
                        break;

                    ImGui.SetCursorScreenPos(iconPos);
                    this.DrawPartyCooldownIcon(row.Items[itemIndex], iconSize);
                    this.HandlePartyCooldownIconInteraction(iconWindow, row.Member.Key, row.Items[itemIndex], iconPos, iconSize, metrics.DrawnIconCount);
                }
            }

            rowY += contentHeight + gap;
        }

        draw.PopClipRect();
    }

    private static float GetPartyCooldownAllianceGroupWidth(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        float labelGap)
        => rows.Any(row => !string.IsNullOrWhiteSpace(row.Member.AllianceGroup))
            ? Math.Max(16f, ImGui.CalcTextSize("C").X + labelGap)
            : 0f;

    private static PartyCooldownBoardRenderMetrics GetPartyCooldownBoardRenderMetrics(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows)
    {
        var iconSize = iconWindow.IconSize;
        var gap = Math.Max(2f, iconWindow.Gap);
        var padding = Math.Max(4f, gap);
        var labelGap = Math.Max(4f, gap);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var allianceGroupWidth = GetPartyCooldownAllianceGroupWidth(rows, labelGap);
        var labelWidth = allianceGroupWidth + jobIconSize + labelGap + nameWidth + labelGap;
        var iconAreaWidth = Math.Max(0f, iconWindow.Width - padding * 2f - labelWidth);
        var iconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(iconAreaWidth, iconSize, gap);
        var drawnIconCount = 0;
        foreach (var row in rows)
            drawnIconCount += row.Items.Count;

        return new PartyCooldownBoardRenderMetrics(
            iconSize,
            gap,
            padding,
            labelGap,
            jobIconSize,
            allianceGroupWidth,
            labelWidth,
            iconsPerLine,
            drawnIconCount);
    }

    private static float GetPartyCooldownExpandedBoardHeight(
        float currentHeight,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
    {
        var requiredHeight = GetPartyCooldownBoardContentHeight(rows, metrics);
        return Math.Clamp(Math.Max(currentHeight, requiredHeight), MinOverlayHeight, MaxOverlayHeight);
    }

    private static float GetPartyCooldownBoardContentHeight(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
    {
        var height = metrics.Padding * 2f;
        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                height += metrics.Gap;

            height += PartyCooldownBoardLayout.GetRowContentHeight(
                rows[i].Items.Count,
                metrics.IconSize,
                metrics.Gap,
                metrics.IconsPerLine);
        }

        return rows.Count == 0 ? 0f : height;
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

    private void HandlePartyCooldownIconInteraction(
        IconWindowConfig iconWindow,
        string memberKey,
        PartyCooldownDisplayItem item,
        Vector2 iconPos,
        float iconSize,
        int drawnIconCount)
    {
        var iconMax = iconPos + new Vector2(iconSize, iconSize);
        if (this.config.LockOverlay)
        {
            if (IsMouseInRect(iconPos, iconMax))
            {
                this.RecordTooltipHover(TooltipDiagnosticKind.PartyCooldown, iconWindow.Id, item.Definition.Id, item.Definition.ActionId, drawnIconCount, hitboxExpanded: false, iconPos, iconMax, imguiHovered: false);
                this.ShowPartyCooldownTooltip(item);
            }

            return;
        }

        ImGui.SetCursorScreenPos(iconPos);
        ImGui.InvisibleButton(
            PartyCooldownBoardLayout.BuildIconInteractionId(iconWindow.Id, memberKey, item.Definition.Id, item.Definition.ActionId),
            new Vector2(iconSize, iconSize));
        var hovered = ImGui.IsItemHovered() && !ImGui.IsItemActive();
        if (hovered)
        {
            this.RecordTooltipHover(TooltipDiagnosticKind.PartyCooldown, iconWindow.Id, item.Definition.Id, item.Definition.ActionId, drawnIconCount, hitboxExpanded: false, iconPos, iconMax, imguiHovered: true);
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
