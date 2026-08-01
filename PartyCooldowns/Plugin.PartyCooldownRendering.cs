namespace FFXIVAura;

public sealed partial class Plugin
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

        var layoutMode = this.GetPartyCooldownLayoutMode(rows);
        var layoutBinding = IconWindowLayoutBinding.PartyCooldown(iconWindow, layoutMode, out var layoutCreated);
        if (layoutCreated)
            this.QueueConfigSave();

        var layoutEnvironment = new PartyCooldownBoardLayoutEnvironment(
            ImGui.GetIO().DisplaySize.Y,
            ImGui.GetTextLineHeight(),
            ImGui.CalcTextSize("C").X);
        var displayLayout = PartyCooldownBoardLayoutCalculator.Calculate(
            layoutBinding,
            rows,
            layoutEnvironment);
        this.RememberPartyCooldownWindowLayoutDiagnostics(iconWindow, displayLayout.Metrics);
        if (itemCount == 0 && this.config.LockOverlay)
            return;

        var areaSize = displayLayout.Size;
        var windowSize = areaSize;
        var clampedPosition = ClampOverlayWindowPosition(layoutBinding.Position, windowSize);
        var positionWasClamped = Vector2.DistanceSquared(layoutBinding.Position, clampedPosition) > 0.25f;
        if (positionWasClamped)
        {
            layoutBinding.Position = clampedPosition;
            this.SetBugDiagnosticEvent($"windowClamped:{iconWindow.Id}");
            this.QueueConfigSave();
        }

        ImGui.SetNextWindowPos(layoutBinding.Position, positionWasClamped ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(0f);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
        var flags = ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.NoDecoration;
        if (this.config.LockOverlay)
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        // ImGui.Begin/PushStyleVar must always be paired with End/PopStyleVar, even when the
        // body throws; otherwise the global ImGui stacks stay unbalanced for the rest of the frame.
        var windowExpanded = false;
        var areaOrigin = Vector2.Zero;
        try
        {
            windowExpanded = ImGui.Begin(this.GetPartyCooldownWindowTitle(iconWindow, layoutMode), flags);
            if (windowExpanded)
            {
                areaOrigin = this.DrawPartyCooldownWindowBody(
                    iconWindow,
                    job,
                    rows,
                    layoutBinding,
                    displayLayout.Metrics,
                    areaSize);
            }
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleVar();
        }

        if (!windowExpanded)
            return;

        if (!this.config.LockOverlay)
        {
            var controlLayout = this.GetOverlayControlLayout(iconWindow.Role, areaOrigin, areaSize);
            this.DrawOverlayRoleControls(iconWindow, job, level, areaOrigin, areaSize, controlLayout);
            this.DrawOverlayDisplayConditionControl(iconWindow, areaOrigin, areaSize, controlLayout);
            this.DrawOverlayNameControl(iconWindow, areaOrigin, areaSize, controlLayout);
            this.DrawOverlayAlignmentControls(iconWindow, job, level, areaOrigin, areaSize, controlLayout, layoutBinding);
        }
    }

    private Vector2 DrawPartyCooldownWindowBody(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        IconWindowLayoutBinding layoutBinding,
        PartyCooldownBoardRenderMetrics metrics,
        Vector2 areaSize)
    {
        this.SelectIconWindowFromOverlayClick(iconWindow);

        var windowPosition = ImGui.GetWindowPos();
        if (!this.config.LockOverlay && Vector2.DistanceSquared(layoutBinding.Position, windowPosition) > 0.25f)
        {
            layoutBinding.Position = windowPosition;
            this.QueueConfigSave();
        }
        else
        {
            layoutBinding.Position = windowPosition;
        }

        ImGui.SetWindowFontScale(metrics.FontScale);

        var areaOrigin = ImGui.GetCursorScreenPos();
        if (!this.config.LockOverlay)
            this.DrawOverlayEditStage(ImGui.GetWindowDrawList(), areaOrigin, areaOrigin + areaSize);

        this.DrawPartyCooldownRows(iconWindow, rows, areaOrigin, areaSize, metrics, layoutBinding.Alignment);

        if (!this.config.LockOverlay)
            this.HandleOverlayResize(iconWindow, job, Array.Empty<AbilityDefinition>(), Array.Empty<AuraState>(), areaOrigin, areaSize, layoutBinding);

        return areaOrigin;
    }

    private void DrawPartyCooldownRows(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        Vector2 areaOrigin,
        Vector2 areaSize,
        PartyCooldownBoardRenderMetrics metrics,
        IconAlignment alignment)
    {
        var draw = ImGui.GetWindowDrawList();

        draw.PushClipRect(areaOrigin, areaOrigin + areaSize, true);
        if (metrics.UseAllianceGrid)
            this.DrawPartyCooldownAllianceStack(iconWindow, rows, draw, areaOrigin, areaSize, metrics, alignment);
        else
            this.DrawPartyCooldownLinearRows(iconWindow, rows, draw, areaOrigin, areaSize, metrics, alignment);

        draw.PopClipRect();
    }

    private void DrawPartyCooldownLinearRows(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        ImDrawListPtr draw,
        Vector2 areaOrigin,
        Vector2 areaSize,
        PartyCooldownBoardRenderMetrics metrics,
        IconAlignment alignment)
    {
        var rowStartX = areaOrigin.X + metrics.Padding;
        var iconAreaStartX = rowStartX + metrics.LabelWidth;
        var rowY = areaOrigin.Y + metrics.Padding;
        var areaMaxX = areaOrigin.X + areaSize.X;
        var memberRowGap = PartyCooldownBoardLayout.GetMemberRowGap(metrics.IconSize, metrics.Gap, metrics.IsCompact);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (rowY > areaOrigin.Y + areaSize.Y)
                break;

            var contentHeight = this.DrawPartyCooldownRow(
                iconWindow,
                row,
                draw,
                rowStartX,
                iconAreaStartX,
                rowY,
                areaMaxX,
                showAllianceGroup: true,
                metrics,
                alignment);
            rowY += contentHeight + memberRowGap;
        }
    }

    private void DrawPartyCooldownAllianceStack(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        ImDrawListPtr draw,
        Vector2 areaOrigin,
        Vector2 areaSize,
        PartyCooldownBoardRenderMetrics metrics,
        IconAlignment alignment)
    {
        var headerHeight = PartyCooldownBoardLayoutCalculator.GetAllianceColumnHeaderHeight(metrics);
        var groupY = areaOrigin.Y + metrics.Padding;
        var groupX = areaOrigin.X + metrics.Padding;
        var groupGap = Math.Max(8f, metrics.Gap * 2f);
        var memberRowGap = PartyCooldownBoardLayout.GetMemberRowGap(metrics.IconSize, metrics.Gap, metrics.IsCompact);
        Span<int> groupRowIndices = stackalloc int[AllianceGroupMemberSlotCount];
        for (var group = 0; group < PartyCooldownBoardLayout.AllianceColumnCount; group++)
        {
            var groupLabel = PartyCooldownAllianceGroups.GroupLabel(group);
            if (!PartyCooldownBoardLayoutCalculator.ContainsAllianceGroup(rows, groupLabel))
                continue;

            if (groupY >= areaOrigin.Y + areaSize.Y)
                break;

            var headerPos = new Vector2(groupX, groupY);
            var headerMax = new Vector2(
                Math.Min(areaOrigin.X + areaSize.X - metrics.Padding, groupX + metrics.AllianceCellWidth),
                headerPos.Y + headerHeight - Math.Max(1f, metrics.Gap));
            draw.AddRectFilled(
                headerPos,
                headerMax,
                ImGui.GetColorU32(new Vector4(0.02f, 0.06f, 0.07f, 0.58f)),
                3f);
            this.DrawOutlinedText(
                draw,
                new Vector2(headerPos.X + metrics.LabelGap, headerPos.Y + Math.Max(0f, (headerHeight - ImGui.GetTextLineHeight()) * 0.5f)),
                groupLabel,
                new Vector4(0.72f, 0.95f, 1f, 0.98f),
                new Vector4(0f, 0f, 0f, 0.9f),
                1f);

            var groupRowCount = 0;
            for (var rowIndex = 0; rowIndex < rows.Count && groupRowCount < groupRowIndices.Length; rowIndex++)
            {
                if (string.Equals(rows[rowIndex].Member.AllianceGroup, groupLabel, StringComparison.Ordinal))
                    groupRowIndices[groupRowCount++] = rowIndex;
            }

            var rowY = groupY + headerHeight;
            for (var rowIndex = 0; rowIndex < groupRowCount; rowIndex++)
            {
                if (rowY > areaOrigin.Y + areaSize.Y)
                    break;

                var row = rows[groupRowIndices[rowIndex]];
                var contentHeight = this.DrawPartyCooldownRow(
                    iconWindow,
                    row,
                    draw,
                    groupX,
                    groupX + metrics.LabelWidth,
                    rowY,
                    groupX + metrics.AllianceCellWidth,
                    showAllianceGroup: false,
                    metrics,
                    alignment);
                rowY += contentHeight + memberRowGap;
            }

            groupY = rowY - memberRowGap + groupGap;
        }
    }

    private float DrawPartyCooldownRow(
        IconWindowConfig iconWindow,
        PartyCooldownMemberRow row,
        ImDrawListPtr draw,
        float rowStartX,
        float iconAreaStartX,
        float rowY,
        float areaMaxX,
        bool showAllianceGroup,
        PartyCooldownBoardRenderMetrics metrics,
        IconAlignment alignment)
    {
        var iconSize = metrics.IconSize;
        var gap = metrics.Gap;
        var contentHeight = PartyCooldownBoardLayoutCalculator.GetRowContentHeight(row.Items.Count, metrics);
        var firstLineHeight = Math.Max(iconSize, Math.Max(metrics.JobIconSize, metrics.TextLineHeight));
        var cursor = new Vector2(rowStartX, rowY + Math.Max(0f, (firstLineHeight - metrics.JobIconSize) * 0.5f));
        if (showAllianceGroup && metrics.AllianceGroupWidth > 0f)
        {
            var groupTextSize = ImGui.CalcTextSize(row.Member.AllianceGroup);
            var groupTextPos = new Vector2(
                cursor.X + Math.Max(0f, metrics.AllianceGroupWidth - metrics.LabelGap - groupTextSize.X) * 0.5f,
                rowY + Math.Max(0f, (firstLineHeight - ImGui.GetTextLineHeight()) * 0.5f));
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
        var namePos = new Vector2(cursor.X, rowY + Math.Max(0f, (firstLineHeight - ImGui.GetTextLineHeight()) * 0.5f));
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
            var lineStartX = iconAreaStartX + PartyCooldownBoardLayout.GetLineStartOffset(alignment, lineItemCount, iconSize, gap, metrics.IconsPerLine);
            var lineY = rowY + lineIndex * (iconSize + gap);

            for (var column = 0; column < lineItemCount; column++)
            {
                var itemIndex = PartyCooldownBoardLayout.GetLineStartIndex(lineIndex, metrics.IconsPerLine) + column;
                if (itemIndex >= row.Items.Count)
                    break;

                var iconPos = new Vector2(lineStartX + column * (iconSize + gap), lineY);
                if (iconPos.X + iconSize > areaMaxX)
                    break;

                ImGui.SetCursorScreenPos(iconPos);
                this.DrawPartyCooldownIcon(row.Items[itemIndex], iconSize);
                this.HandlePartyCooldownIconInteraction(iconWindow, row.Member.Key, row.Items[itemIndex], iconPos, iconSize, metrics.DrawnIconCount);
            }
        }

        return contentHeight;
    }
}
