namespace FFXIVAura;

public sealed unsafe partial class Plugin
{

    private void DrawAbilityIcon(AbilityDefinition ability, float size, IconWindowConfig iconWindow)
    {
        var state = this.GetCooldown(ability);
        var iconId = state.DisplayIconId > 0 ? state.DisplayIconId : ability.IconId;
        var lookup = new GameIconLookup(iconId, false, true, null);
        var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
        var pos = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        var isChargeSkill = state.MaxCharges > 1;
        var chargeEmpty = isChargeSkill && state.CurrentCharges == 0;
        var unavailable = state.ConditionUnavailable || (isChargeSkill ? chargeEmpty : state.IsCooling);
        var ready = isChargeSkill ? state.CurrentCharges > 0 : !state.IsCooling;
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

        if (state.Highlighted || (ready && !state.ConditionUnavailable && iconWindow.HighlightReady))
            draw.AddRect(pos, max, ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.2f, 1f)), 3f, ImDrawFlags.None, 2f);

        if (!unavailable && state.Adjusted && iconWindow.HighlightAdjusted)
            draw.AddRect(pos + new Vector2(1f, 1f), max - new Vector2(1f, 1f), ImGui.GetColorU32(new Vector4(0.45f, 0.75f, 1f, 0.95f)), 3f, ImDrawFlags.None, 1.5f);

    }

    private CooldownState GetCooldown(AbilityDefinition ability)
    {
        var key = $"{ability.Id}:{ability.ActionId}";
        if (this.cooldownFrameCache.TryGetValue(key, out var cached))
            return cached;

        var state = this.ComputeCooldown(ability);
        this.cooldownFrameCache[key] = state;
        return state;
    }

    private CooldownState ComputeCooldown(AbilityDefinition ability)
    {
        var baseActionId = ability.ActionId;
        var actionId = baseActionId;
        var manager = ActionManager.Instance();
        var adjusted = manager->GetAdjustedActionId(actionId);
        if (adjusted != 0)
            actionId = adjusted;

        var total = 0f;
        var elapsed = 0f;
        var group = manager->GetRecastGroup((int)ActionType.Action, actionId);
        if (group >= 0)
        {
            var detail = manager->GetRecastGroupDetail(group);
            if (detail is not null && detail->IsActive)
            {
                total = detail->Total;
                elapsed = detail->Elapsed;
            }
        }

        if (total <= 0f)
        {
            var totalMs = ActionManager.GetAdjustedRecastTime(ActionType.Action, actionId, true);
            total = Math.Max(ability.Cooldown, totalMs / 1000f);
        }

        var charges = manager->GetCurrentCharges(actionId);
        var maxCharges = Math.Max((uint)ActionManager.GetMaxCharges(actionId, 0), Math.Max((uint)ability.Charges, 1u));
        var remaining = GetRemainingCooldown(total, elapsed, charges, maxCharges);
        var displayTotal = maxCharges > 1 ? total / maxCharges : total;
        var highlighted = manager->IsActionHighlighted(ActionType.Action, actionId);
        var iconId = this.GetActionIconId(actionId);
        var conditionUnavailable = this.IsGlobalActionUnavailable()
                                   || (!highlighted
                                    && (this.IsActionResourceUnavailable(manager, actionId)
                                        || this.IsActionStatusUnavailable(manager, actionId)));

        return new CooldownState(actionId, iconId, displayTotal, Math.Clamp(remaining, 0f, displayTotal), charges, maxCharges, highlighted, actionId != baseActionId, conditionUnavailable);
    }

    private bool IsGlobalActionUnavailable()
    {
        return Condition[ConditionFlag.Mounted];
    }

    private bool IsActionResourceUnavailable(ActionManager* manager, uint actionId)
    {
        try
        {
            return manager->CheckActionResources(ActionType.Action, actionId, null) != 0;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action resources for {actionId}.");
            return false;
        }
    }

    private bool IsActionStatusUnavailable(ActionManager* manager, uint actionId)
    {
        try
        {
            if (this.IsTargetOnlyUnavailable(manager, actionId))
                return false;

            var targetId = TargetManager.Target?.EntityId ?? 0xE0000000;
            return manager->GetActionStatus(ActionType.Action, actionId, targetId, false, true, null) != 0;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action status for {actionId}.");
            return false;
        }
    }

    private bool IsTargetOnlyUnavailable(ActionManager* manager, uint actionId)
    {
        var action = this.GetActionRow(actionId);
        if (action is null)
            return false;

        var requiresTarget = action.Value.CanTargetHostile
                             && !action.Value.CanTargetSelf
                             && !action.Value.CanTargetParty
                             && !action.Value.CanTargetAlly;
        if (!requiresTarget)
            return false;

        if (TargetManager.Target is null)
            return true;

        return !manager->IsActionTargetInRange(ActionType.Action, actionId);
    }

    private uint GetActionIconId(uint actionId)
    {
        try
        {
            var row = this.GetActionRow(actionId);
            if (row is not null)
                return row.Value.Icon;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action icon for {actionId}.");
        }

        return 0;
    }

    private GameAction? GetActionRow(uint actionId)
    {
        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            return null;

        var row = sheet.GetRow(actionId);
        return row.RowId == 0 ? null : row;
    }

    private static float GetRemainingCooldown(float total, float elapsed, uint charges, uint maxCharges)
    {
        if (total <= 0f || elapsed <= 0f)
            return 0f;

        if (maxCharges <= 1)
            return Math.Max(0f, total - elapsed);

        if (charges >= maxCharges)
            return 0f;

        var chargeTime = total / maxCharges;
        if (chargeTime <= 0f)
            return 0f;

        return Math.Abs(total - elapsed) % chargeTime;
    }

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

    private void DrawDesaturateOverlay(ImDrawListPtr draw, Vector2 min, Vector2 max)
    {
        draw.PushClipRect(min, max, true);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.43f, 0.43f, 0.43f, 0.38f)), 3f);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.10f)), 3f);
        draw.PopClipRect();
    }

    private void DrawUnavailableIconTint(ImDrawListPtr draw, Vector2 min, Vector2 max)
    {
        draw.PushClipRect(min, max, true);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.55f, 0.55f, 0.55f, 0.72f)), 3f);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.16f)), 3f);
        draw.PopClipRect();
    }

    private IDalamudTextureWrap? GetGrayscaleIconTexture(uint iconId)
    {
        lock (this.grayscaleIconLock)
        {
            if (this.grayscaleIconCache.TryGetValue(iconId, out var cached))
                return cached;

            if (!this.grayscaleIconPending.Add(iconId))
                return null;
        }

        this.CreateGrayscaleIconTexture(iconId);
        return null;
    }

    private void CreateGrayscaleIconTexture(uint iconId)
    {
        try
        {
            var lookup = new GameIconLookup(iconId, false, true, null);
            if (!TextureProvider.TryGetIconPath(in lookup, out var iconPath))
                throw new FileNotFoundException($"Icon path not found for {iconId}.");

            var tex = DataManager.GetFile<TexFile>(iconPath);
            if (tex is null)
                throw new FileNotFoundException($"Icon tex not found for {iconId}: {iconPath}");

            var data = tex.GetRgbaImageData();
            this.ConvertRgbaImageToGrayscale(data);
            var spec = RawImageSpecification.Rgba32(tex.Header.Width, tex.Header.Height);
            var texture = TextureProvider.CreateFromRaw(spec, data, $"FFXIVAura grayscale icon {iconId}");

            lock (this.grayscaleIconLock)
            {
                if (this.grayscaleIconCache.TryGetValue(iconId, out var oldTexture))
                    oldTexture.Dispose();

                this.grayscaleIconCache[iconId] = texture;
                this.grayscaleIconPending.Remove(iconId);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to create grayscale icon {iconId}.");
            lock (this.grayscaleIconLock)
                this.grayscaleIconPending.Remove(iconId);
        }
    }

    private void ConvertRgbaImageToGrayscale(byte[] data)
    {
        for (var offset = 0; offset + 3 < data.Length; offset += 4)
        {
            var gray = (byte)Math.Clamp((int)(data[offset] * 0.299f + data[offset + 1] * 0.587f + data[offset + 2] * 0.114f), 0, 255);
            data[offset] = gray;
            data[offset + 1] = gray;
            data[offset + 2] = gray;
        }
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

        var available = Math.Max(1f, max.X - min.X - 4f);
        if (ImGui.CalcTextSize(text).X > available)
            text = TrimKeybindText(text, available);

        this.DrawOutlinedText(draw, min + new Vector2(2f, 1f), text, new Vector4(1f, 1f, 1f, 0.98f), new Vector4(0f, 0f, 0f, 0.95f), 1f);
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
