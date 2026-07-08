namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawAbilityIcon(AbilityDefinition ability, float size, IconWindowConfig iconWindow)
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AbilityIcon);
        try
        {
            var state = this.GetCooldown(ability);
            var iconId = state.DisplayIconId > 0 ? state.DisplayIconId : ability.IconId;
            var lookup = new GameIconLookup(iconId, false, true, null);
            var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
            var pos = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();

            var unavailable = state.IsUnavailable;
            var grayscaleTexture = unavailable ? this.GetGrayscaleIconTexture(iconId) : null;
            ImGui.Image((grayscaleTexture ?? texture).Handle, new Vector2(size, size));
            var max = pos + new Vector2(size, size);

            if (unavailable && grayscaleTexture is null)
                this.DrawUnavailableIconTint(draw, pos, max);

            if (state.IsCooling)
                this.DrawCooldownCover(draw, pos, max, 1f - state.Progress);

            if (iconWindow.ShowKeybindText)
                this.DrawKeybindText(draw, pos, max, ability.ActionId, state.DisplayActionId);

            if (state.CurrentCharges > 1 || state.MaxCharges > 1)
                this.DrawChargeText(draw, pos, max, state.CurrentCharges);

            if (state.IsCooling)
                this.DrawTimerText(draw, pos, max, state.Remaining);

            if (state.Highlighted || (state.IsReady && iconWindow.HighlightReady))
                draw.AddRect(pos, max, ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.2f, 1f)), 3f, ImDrawFlags.None, 2f);

            if (!unavailable && state.Adjusted && iconWindow.HighlightAdjusted)
                draw.AddRect(pos + new Vector2(1f, 1f), max - new Vector2(1f, 1f), ImGui.GetColorU32(new Vector4(0.45f, 0.75f, 1f, 0.95f)), 3f, ImDrawFlags.None, 1.5f);
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AbilityIcon, profileStart);
        }
    }
}
