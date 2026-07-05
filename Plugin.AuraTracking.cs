namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawAuraTrackerEditor(IconWindowConfig iconWindow)
    {
        ImGui.TextUnformatted("\uCD94\uC801 \uBC84\uD504/\uB514\uBC84\uD504");

        var search = iconWindow.AuraSearch ?? string.Empty;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputTextWithHint("##FFXIVAuraAuraSearch", "\uBC84\uD504/\uB514\uBC84\uD504 \uC774\uB984 \uB610\uB294 ID \uAC80\uC0C9", ref search, 80))
        {
            iconWindow.AuraSearch = search;
            this.QueueConfigSave();
        }

        ImGui.SameLine();
        if (ImGui.Button("\uAC80\uC0C9"))
        {
            this.auraSearchWindowId = iconWindow.Id;
            this.auraSearchWindowVisible = true;
        }

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

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("\uC0C1\uD0DC ID", ref this.pendingStatusId);
        ImGui.SameLine();
        if (ImGui.Button("\uC0C1\uD0DC \uCD94\uAC00") && this.pendingStatusId > 0)
        {
            var id = (uint)this.pendingStatusId;
            if (this.TrackAura(iconWindow, id))
                this.QueueConfigSave();
        }

        ImGui.BeginChild("FFXIVAuraTrackedAuraList", new Vector2(560f, 220f), true);
        for (var i = 0; i < iconWindow.TrackedStatusIds.Count; i++)
        {
            var statusId = iconWindow.TrackedStatusIds[i];
            var definition = this.GetStatusDefinition(statusId);
            ImGui.PushID($"aura-{statusId}");
            this.DrawStatusListIcon(definition.IconId, 22f);
            ImGui.SameLine(0f, 8f);
            ImGui.TextUnformatted($"{definition.Name}  \uC0C1\uD0DC {statusId}");
            ImGui.SameLine();
            if (ImGui.SmallButton("\uC0AD\uC81C"))
            {
                this.UntrackAura(iconWindow, statusId);
                this.QueueConfigSave();
                ImGui.PopID();
                break;
            }

            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private void DrawAuraSearchWindow()
    {
        var iconWindow = this.GetAuraSearchWindow();
        if (iconWindow is null)
            return;

        ImGui.SetNextWindowSize(new Vector2(620f, 460f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("\uBC84\uD504/\uB514\uBC84\uD504 \uAC80\uC0C9", ref this.auraSearchWindowVisible))
        {
            if (!this.auraSearchWindowVisible)
                this.auraSearchWindowId = null;

            ImGui.End();
            return;
        }

        var search = iconWindow.AuraSearch ?? string.Empty;
        ImGui.SetNextItemWidth(360f);
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

        ImGui.Separator();
        this.DrawAuraSearchResults(iconWindow, new Vector2(590f, 340f));
        ImGui.End();
        if (!this.auraSearchWindowVisible)
            this.auraSearchWindowId = null;
    }

    private IconWindowConfig? GetAuraSearchWindow()
    {
        if (!this.auraSearchWindowVisible)
            return null;

        var iconWindow = string.IsNullOrWhiteSpace(this.auraSearchWindowId)
            ? this.config.IconWindows.FirstOrDefault(window => window.Role != IconWindowRole.SkillCooldowns)
            : this.config.IconWindows.FirstOrDefault(window => string.Equals(window.Id, this.auraSearchWindowId, StringComparison.OrdinalIgnoreCase));
        if (iconWindow is not null && iconWindow.Role != IconWindowRole.SkillCooldowns)
            return iconWindow;

        this.auraSearchWindowVisible = false;
        this.auraSearchWindowId = null;
        return null;
    }

    private void CloseAuraSearchWindow()
    {
        this.auraSearchWindowVisible = false;
        this.auraSearchWindowId = null;
    }

    private void DrawAuraSearchResults(IconWindowConfig iconWindow, Vector2 size)
    {
        var limit = string.IsNullOrWhiteSpace(iconWindow.AuraSearch) ? 200 : 80;
        var results = this.SearchStatuses(iconWindow.AuraSearch, iconWindow)
            .Take(limit)
            .ToList();

        ImGui.BeginChild("FFXIVAuraAuraSearchResults", size, true);
        if (results.Count == 0)
        {
            ImGui.TextUnformatted(iconWindow.AuraSearchActiveOnly
                ? "\uD604\uC7AC \uBCF4\uC774\uB294 \uBC84\uD504/\uB514\uBC84\uD504\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4."
                : "\uCD5C\uADFC \uAC10\uC9C0\uB41C \uBC84\uD504/\uB514\uBC84\uD504\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
        }

        foreach (var result in results)
        {
            var alreadyTracked = iconWindow.TrackedStatusIds.Contains(result.StatusId);
            ImGui.PushID($"status-search-{result.StatusId}");
            this.DrawStatusListIcon(result.IconId, 22f);
            ImGui.SameLine(0f, 8f);
            ImGui.TextUnformatted($"{result.Name}  \uC0C1\uD0DC {result.StatusId}");
            ImGui.SameLine();
            if (alreadyTracked)
            {
                ImGui.BeginDisabled();
                ImGui.SmallButton("\uCD94\uAC00\uB428");
                ImGui.EndDisabled();
            }
            else if (ImGui.SmallButton("\uCD94\uAC00"))
            {
                if (this.TrackAura(iconWindow, result.StatusId))
                    this.QueueConfigSave();
            }

            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private bool TrackAura(IconWindowConfig iconWindow, uint statusId)
    {
        if (iconWindow.TrackedStatusIds.Contains(statusId))
            return false;

        iconWindow.TrackedStatusIds.Add(statusId);
        var auras = this.GetOverlayAuras(iconWindow, OverlayItemVisibility.Layout).ToList();
        this.EnsureAuraIconPositions(iconWindow, auras, new Vector2(iconWindow.Width, iconWindow.Height));
        return true;
    }

    private void UntrackAura(IconWindowConfig iconWindow, uint statusId)
    {
        iconWindow.TrackedStatusIds.RemoveAll(id => id == statusId);
        var positionKey = OverlayPositionKeys.Aura(statusId);
        var groupPrefix = OverlayPositionKeys.WindowPrefix(iconWindow.Id);
        foreach (var groupKey in iconWindow.AuraPositionsByRole.Keys.ToList())
        {
            if (!groupKey.StartsWith(groupPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!iconWindow.AuraPositionsByRole.TryGetValue(groupKey, out var positions))
                continue;

            positions.Remove(positionKey);
            if (positions.Count == 0)
                iconWindow.AuraPositionsByRole.Remove(groupKey);
        }
    }
}
