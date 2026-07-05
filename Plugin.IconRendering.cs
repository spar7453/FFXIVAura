namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawCooldownCover(ImDrawListPtr draw, Vector2 min, Vector2 max, float elapsedRatio)
    {
        elapsedRatio = Math.Clamp(elapsedRatio, 0f, 1f);
        if (elapsedRatio >= 0.995f)
            return;

        draw.PushClipRect(min, max, true);
        var center = (min + max) * 0.5f;
        var radius = (max.X - min.X) * 0.74f;
        var start = -MathF.PI * 0.5f + MathF.Tau * elapsedRatio;
        var end = -MathF.PI * 0.5f + MathF.Tau;
        var color = ImGui.GetColorU32(new Vector4(0.02f, 0.02f, 0.02f, 0.44f));

        draw.PathClear();
        draw.PathLineTo(center);
        var remainingRatio = 1f - elapsedRatio;
        var steps = Math.Max(10, (int)(64 * remainingRatio));
        for (var i = 0; i <= steps; i++)
        {
            var t = steps == 0 ? 0 : i / (float)steps;
            var a = start + (end - start) * t;
            draw.PathLineTo(center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius);
        }

        draw.PathFillConvex(color);
        draw.PopClipRect();
    }

    private void DrawUnavailableIconTint(ImDrawListPtr draw, Vector2 min, Vector2 max)
    {
        draw.PushClipRect(min, max, true);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.55f, 0.55f, 0.55f, 0.72f)), 3f);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.16f)), 3f);
        draw.PopClipRect();
    }

    private void DrawTimerText(ImDrawListPtr draw, Vector2 min, Vector2 max, float remaining)
    {
        var text = Math.Ceiling(remaining).ToString("0");
        using (this.cooldownFont.Push())
        {
            var size = ImGui.CalcTextSize(text);
            var pos = min + ((max - min) - size) * 0.5f + new Vector2(0f, -1f);
            this.DrawOutlinedText(draw, pos, text, new Vector4(1f, 1f, 1f, 1f), new Vector4(0f, 0f, 0f, 0.95f), 1.25f);
        }
    }

    private void DrawChargeText(ImDrawListPtr draw, Vector2 min, Vector2 max, uint charges)
    {
        var text = charges.ToString();
        using (this.chargeFont.Push())
        {
            var size = ImGui.CalcTextSize(text);
            var pos = new Vector2(max.X - size.X - 1f, max.Y - size.Y + 1f);
            this.DrawOutlinedText(draw, pos, text, new Vector4(1f, 1f, 1f, 1f), new Vector4(0.72f, 0.12f, 0.06f, 1f), 1.35f);
            draw.AddText(pos + new Vector2(0f, -1f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.45f)), text);
        }
    }

    private void DrawKeybindText(ImDrawListPtr draw, Vector2 min, Vector2 max, uint baseActionId, uint displayActionId)
    {
        var text = this.GetActionKeybindText(baseActionId, displayActionId);
        if (string.IsNullOrWhiteSpace(text))
            return;

        using (this.keybindFont.Push())
        {
            var available = Math.Max(1f, max.X - min.X - 4f);
            if (ImGui.CalcTextSize(text).X > available)
                text = TrimKeybindText(text, available);

            this.DrawOutlinedText(draw, min + new Vector2(2f, 1f), text, new Vector4(1f, 1f, 1f, 0.98f), new Vector4(0f, 0f, 0f, 0.95f), 1f);
        }
    }

    private static string TrimKeybindText(string text, float availableWidth)
    {
        const string suffix = ".";
        while (text.Length > 1 && ImGui.CalcTextSize(text + suffix).X > availableWidth)
            text = text[..^1];

        return text + suffix;
    }

    private void DrawOutlinedText(ImDrawListPtr draw, Vector2 pos, string text, Vector4 fill, Vector4 outline, float offset)
    {
        var outlineColor = ImGui.GetColorU32(outline);
        var fillColor = ImGui.GetColorU32(fill);
        draw.AddText(pos + new Vector2(-offset, 0f), outlineColor, text);
        draw.AddText(pos + new Vector2(offset, 0f), outlineColor, text);
        draw.AddText(pos + new Vector2(0f, -offset), outlineColor, text);
        draw.AddText(pos + new Vector2(0f, offset), outlineColor, text);
        draw.AddText(pos + new Vector2(-offset, -offset), outlineColor, text);
        draw.AddText(pos + new Vector2(offset, -offset), outlineColor, text);
        draw.AddText(pos + new Vector2(-offset, offset), outlineColor, text);
        draw.AddText(pos + new Vector2(offset, offset), outlineColor, text);
        draw.AddText(pos, fillColor, text);
    }
}
