namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawAuraIcon(AuraState aura, float size, IconWindowConfig iconWindow)
    {
        var lookup = new GameIconLookup(aura.IconId, false, true, null);
        var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
        var pos = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        var grayscaleTexture = !aura.Present ? this.GetGrayscaleIconTexture(aura.IconId) : null;
        ImGui.Image((grayscaleTexture ?? texture).Handle, new Vector2(size, size));
        var max = pos + new Vector2(size, size);

        if (!aura.Present)
        {
            if (grayscaleTexture is null)
                this.DrawUnavailableIconTint(draw, pos, max);

            return;
        }

        if (aura.Remaining > 0.05f)
            this.DrawTimerText(draw, pos, max, aura.Remaining);

        if (aura.Param > 1)
            this.DrawChargeText(draw, pos, max, aura.Param);

        if (iconWindow.Role == IconWindowRole.PartyBuffs && iconWindow.ShowPartyAuraCount && aura.Count > 0)
            this.DrawAuraCountText(draw, pos, max, aura.Count);

        if (aura.FromSelf)
            draw.AddRect(pos, max, ImGui.GetColorU32(new Vector4(0.45f, 0.75f, 1f, 0.95f)), 3f, ImDrawFlags.None, 1.5f);
    }

    private void DrawAuraCountText(ImDrawListPtr draw, Vector2 min, Vector2 max, int count)
    {
        var text = count.ToString();
        using (this.keybindFont.Push())
        {
            var size = ImGui.CalcTextSize(text);
            var pos = new Vector2(max.X - size.X - 2f, min.Y + 1f);
            this.DrawOutlinedText(draw, pos, text, new Vector4(0.82f, 1f, 0.82f, 1f), new Vector4(0f, 0f, 0f, 0.95f), 1f);
        }
    }

    private void DrawStatusListIcon(uint iconId, float size)
    {
        var lookup = new GameIconLookup(iconId, false, true, null);
        var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
        ImGui.Image(texture.Handle, new Vector2(size, size));
    }
}
