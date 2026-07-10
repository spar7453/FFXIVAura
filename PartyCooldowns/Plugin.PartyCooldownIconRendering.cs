namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
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
            var unavailable = item.State == PartyCooldownDisplayState.Cooldown;
            var grayscaleTexture = unavailable ? this.GetGrayscaleIconTexture(iconId) : null;

            ImGui.Image((grayscaleTexture ?? texture).Handle, new Vector2(size, size));
            if (unavailable && grayscaleTexture is null)
                this.DrawUnavailableIconTint(draw, pos, max);

            if (item.State != PartyCooldownDisplayState.Active && item.CooldownRemaining > 0.05f)
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

            if (item.MaxCharges > 1)
                this.DrawChargeText(draw, pos, max, item.CurrentCharges);

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
                this.RegisterPartyCooldownTooltipCandidate(item.Definition);
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
            this.RegisterPartyCooldownTooltipCandidate(item.Definition);
        }
    }

    private void ShowPartyCooldownTooltip(PartyCooldownDefinition definition)
    {
        if (!this.config.ShowTooltips)
        {
            this.tooltipDiagnostics.RecordDisabledSkip(TooltipDiagnosticKind.PartyCooldown, definition.Id, definition.ActionId);
            return;
        }

        this.tooltipDiagnostics.RecordTooltipRequest(TooltipDiagnosticKind.PartyCooldown, definition.Id, definition.ActionId);
        this.SetBugDiagnosticEvent($"tooltipPartyCooldown:{definition.Id}:{definition.ActionId}");
        this.ShowOverlayActionTooltip(definition.ActionId, definition.Name, definition.IconId, definition.Category);
    }
}
