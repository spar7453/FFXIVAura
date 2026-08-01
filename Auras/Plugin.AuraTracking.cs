namespace FFXIVAura;

public sealed partial class Plugin
{
    private void DrawAuraTrackerEditor(IconWindowConfig iconWindow)
    {
        ImGui.TextUnformatted("\uCD94\uC801 \uBC84\uD504/\uB514\uBC84\uD504");

        var search = iconWindow.AuraSearch ?? string.Empty;
        ImGui.SetNextItemWidth(Math.Min(360f, Math.Max(180f, GetConfigContentWidth() - 100f)));
        if (ImGui.InputTextWithHint("##FFXIVAuraAuraSearch", "\uBC84\uD504/\uB514\uBC84\uD504 \uC774\uB984 \uB610\uB294 ID \uAC80\uC0C9", ref search, 80))
        {
            iconWindow.AuraSearch = search;
            this.QueueConfigSave();
        }

        ImGui.SameLine();
        if (ImGui.Button("\uAC80\uC0C9"))
            this.auraSearchWindowSession.Open(iconWindow.Id);

        if (!string.IsNullOrWhiteSpace(iconWindow.AuraSearch))
        {
            ImGui.SameLine();
            if (ImGui.Button("\uAC80\uC0C9 \uC9C0\uC6B0\uAE30"))
            {
                iconWindow.AuraSearch = string.Empty;
                this.QueueConfigSave();
            }
        }

        var activeOnly = iconWindow.AuraSearchActiveOnly;
        if (ImGui.Checkbox("\uD604\uC7AC \uBCF4\uC774\uB294 \uBC84\uD504/\uB514\uBC84\uD504\uB9CC \uAC80\uC0C9", ref activeOnly))
        {
            iconWindow.AuraSearchActiveOnly = activeOnly;
            this.QueueConfigSave();
        }

        var showIndividualIds = iconWindow.AuraSearchShowIndividualIds;
        if (ImGui.Checkbox("ID\uBCC4 \uBCF4\uAE30", ref showIndividualIds))
        {
            iconWindow.AuraSearchShowIndividualIds = showIndividualIds;
            this.QueueConfigSave();
        }

        var pendingStatusId = this.auraSearchWindowSession.PendingStatusId;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputInt("\uC0C1\uD0DC ID", ref pendingStatusId))
            this.auraSearchWindowSession.PendingStatusId = pendingStatusId;

        ImGui.SameLine();
        if (ImGui.Button("\uC0C1\uD0DC \uCD94\uAC00") && pendingStatusId > 0)
        {
            var id = (uint)pendingStatusId;
            if (this.TrackAura(iconWindow, id, exact: true))
                this.QueueConfigSave();
        }

        using (ImRaii.Child("FFXIVAuraTrackedAuraList", new Vector2(GetConfigContentWidth(), 220f), true))
        {
            foreach (var group in this.auraSearchService.GetTrackedGroups(iconWindow))
            {
                using var idScope = ImRaii.PushId($"aura-{group.StatusId}");
                this.DrawStatusListIcon(group.IconId, 22f);
                ImGui.SameLine(0f, 8f);
                ImGui.TextUnformatted(AuraTrackingPresentation.GetTrackedLabel(group));
                ImGui.SameLine();
                if (ImGui.SmallButton(group.IsExact ? "그룹으로" : "이 ID만"))
                {
                    if (this.SetAuraTrackingMode(iconWindow, group.StatusId, exact: !group.IsExact))
                        this.QueueConfigSave();

                    break;
                }

                ImGui.SameLine();
                if (ImGui.SmallButton("\uC0AD\uC81C"))
                {
                    this.auraTrackingService.Untrack(iconWindow, group);
                    this.QueueConfigSave();
                    break;
                }
            }
        }
    }

    private void DrawAuraSearchWindow()
    {
        var iconWindow = this.auraSearchWindowSession.ResolveWindow(this.config.IconWindows);
        if (iconWindow is null)
            return;

        ImGui.SetNextWindowSize(new Vector2(620f, 460f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(420f, 320f), new Vector2(float.MaxValue, float.MaxValue));
        var searchWindowVisible = this.auraSearchWindowSession.IsVisible;
        try
        {
            if (ImGui.Begin("\uBC84\uD504/\uB514\uBC84\uD504 \uAC80\uC0C9", ref searchWindowVisible))
                this.DrawAuraSearchWindowContent(iconWindow);
        }
        finally
        {
            ImGui.End();
        }

        this.auraSearchWindowSession.SetVisible(searchWindowVisible);
    }

    private void DrawAuraSearchWindowContent(IconWindowConfig iconWindow)
    {
        var search = iconWindow.AuraSearch ?? string.Empty;
        var contentWidth = GetConfigContentWidth(360f);
        var clearWidth = ImGui.CalcTextSize("\uC9C0\uC6B0\uAE30").X + ImGui.GetStyle().FramePadding.X * 2f;
        ImGui.SetNextItemWidth(Math.Max(180f, contentWidth - clearWidth - ImGui.GetStyle().ItemSpacing.X));
        if (ImGui.InputTextWithHint("##FFXIVAuraAuraSearchWindowInput", "\uC774\uB984 \uB610\uB294 ID", ref search, 80))
        {
            iconWindow.AuraSearch = search;
            this.QueueConfigSave();
        }

        ImGui.SameLine();
        if (ImGui.Button("\uC9C0\uC6B0\uAE30"))
        {
            iconWindow.AuraSearch = string.Empty;
            this.QueueConfigSave();
        }

        var activeOnly = iconWindow.AuraSearchActiveOnly;
        if (ImGui.Checkbox("\uD604\uC7AC \uBCF4\uC774\uB294 \uBC84\uD504/\uB514\uBC84\uD504\uB9CC", ref activeOnly))
        {
            iconWindow.AuraSearchActiveOnly = activeOnly;
            this.QueueConfigSave();
        }

        ImGui.SameLine();
        var showIndividualIds = iconWindow.AuraSearchShowIndividualIds;
        if (ImGui.Checkbox("ID\uBCC4 \uBCF4\uAE30", ref showIndividualIds))
        {
            iconWindow.AuraSearchShowIndividualIds = showIndividualIds;
            this.QueueConfigSave();
        }

        ImGui.Separator();
        var resultSize = ImGui.GetContentRegionAvail();
        resultSize.Y = Math.Max(120f, resultSize.Y);
        this.DrawAuraSearchResults(iconWindow, resultSize);
    }

    private void DrawAuraSearchResults(IconWindowConfig iconWindow, Vector2 size)
    {
        var limit = string.IsNullOrWhiteSpace(iconWindow.AuraSearch) ? 200 : 80;
        var results = this.auraSearchService.Search(iconWindow.AuraSearch, iconWindow);
        var resultCount = Math.Min(limit, results.Count);

        using var resultsChild = ImRaii.Child("FFXIVAuraAuraSearchResults", size, true);
        if (resultCount == 0)
        {
            this.DrawEmptyAuraSearchResult(iconWindow);
        }

        using var resultsTable = ImRaii.Table("FFXIVAuraAuraSearchResultTable", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg);
        if (resultsTable)
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, 30f);
            ImGui.TableSetupColumn("##status", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(
                "##action",
                ImGuiTableColumnFlags.WidthFixed,
                iconWindow.AuraSearchShowIndividualIds ? 92f : 72f);
            for (var index = 0; index < resultCount; index++)
            {
                var result = results[index];
                var exact = iconWindow.AuraSearchShowIndividualIds;
                var coverage = this.auraTrackingService.GetCoverage(iconWindow, result.StatusId, exact);
                using var idScope = ImRaii.PushId($"status-search-{result.StatusId}");
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                this.DrawStatusListIcon(result.IconId, 22f);

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(AuraTrackingPresentation.GetSearchResultLabel(
                    result,
                    iconWindow.AuraSearchShowIndividualIds,
                    iconWindow.AuraSearch));
                var tags = AuraSearchDisplayResultFormatter.GetTagText(result);
                if (!string.IsNullOrEmpty(tags))
                    ImGui.TextWrapped(tags);

                ImGui.TableSetColumnIndex(2);
                var button = AuraTrackingPresentation.GetTrackingButton(exact, coverage);
                if (!button.Enabled)
                {
                    using var disabledScope = ImRaii.Disabled();
                    ImGui.SmallButton(button.Label);
                }
                else if (ImGui.SmallButton(button.Label))
                {
                    var changed = button.ChangesMode
                        ? this.SetAuraTrackingMode(iconWindow, result.StatusId, button.Exact)
                        : this.TrackAura(iconWindow, result.StatusId, button.Exact);
                    if (changed)
                        this.QueueConfigSave();
                }

            }
        }
    }

    private void DrawEmptyAuraSearchResult(IconWindowConfig iconWindow)
    {
        if (iconWindow.AuraSearchActiveOnly && !string.IsNullOrWhiteSpace(iconWindow.AuraSearch))
        {
            ImGui.TextWrapped("현재 보이는 버프/디버프만 검색 중입니다. 전체 상태에서 찾으려면 검색 범위를 전환하세요.");
            if (ImGui.Button("전체 검색으로 전환"))
            {
                iconWindow.AuraSearchActiveOnly = false;
                this.QueueConfigSave();
            }

            return;
        }

        ImGui.TextUnformatted(iconWindow.AuraSearchActiveOnly
            ? "\uD604\uC7AC \uBCF4\uC774\uB294 \uBC84\uD504/\uB514\uBC84\uD504\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4."
            : "\uCD5C\uADFC \uAC10\uC9C0\uB41C \uBC84\uD504/\uB514\uBC84\uD504\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
    }

    private bool TrackAura(IconWindowConfig iconWindow, uint statusId, bool exact = false)
    {
        if (!this.auraTrackingService.Track(iconWindow, statusId, exact))
            return false;

        var auras = this.GetOverlayAuras(iconWindow, OverlayItemVisibility.Layout).ToList();
        this.EnsureAuraIconPositions(iconWindow, auras, new Vector2(iconWindow.Width, iconWindow.Height));
        return true;
    }

    private bool SetAuraTrackingMode(IconWindowConfig iconWindow, uint statusId, bool exact)
    {
        if (!this.auraTrackingService.SetMode(iconWindow, statusId, exact))
            return false;

        var auras = this.GetOverlayAuras(iconWindow, OverlayItemVisibility.Layout).ToList();
        this.EnsureAuraIconPositions(iconWindow, auras, new Vector2(iconWindow.Width, iconWindow.Height));
        return true;
    }

}
