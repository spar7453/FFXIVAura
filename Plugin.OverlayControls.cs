namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawOverlayRoleControls(IconWindowConfig iconWindow, string job, uint level, Vector2 areaOrigin, Vector2 areaSize, OverlayControlLayout layout)
    {
        const float padding = 6f;
        var framePadding = new Vector2(7f, 3f);
        var options = WindowRoleOptions().ToList();
        var currentLabel = options.FirstOrDefault(option => option.Role == iconWindow.Role).Label ?? "\uC2A4\uD0AC";
        var comboWidth = Math.Max(132f, options.Max(option => ImGui.CalcTextSize(option.Label).X) + framePadding.X * 2f + 26f);
        var windowWidth = comboWidth + padding * 2f;
        var windowHeight = GetOverlayControlHeight(framePadding, padding);
        var windowY = OverlayControlGeometry.GetTopControlY(areaOrigin, areaSize, windowHeight, row: 0, layout);

        this.DrawOverlayFloatingWindow(
            $"FFXIVAuraOverlayRoleControls-{iconWindow.Id}",
            new Vector2(areaOrigin.X, windowY),
            new Vector2(windowWidth, windowHeight),
            0f,
            () =>
        {
            ImGui.PushID($"overlay-role-controls-{iconWindow.Id}");
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
            ImGui.SetCursorPos(new Vector2(padding, padding));
            ImGui.SetNextItemWidth(comboWidth);
            if (ImGui.BeginCombo("##role", currentLabel))
            {
                foreach (var (role, label) in options)
                {
                    var selected = iconWindow.Role == role;
                    if (ImGui.Selectable(label, selected))
                    {
                        if (iconWindow.Role != role)
                        {
                            this.ApplyOverlayRole(iconWindow, job, level, role);
                            this.QueueConfigSave();
                        }
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            ImGui.PopStyleVar();
            ImGui.PopID();
        });
    }

    private void DrawOverlayDisplayConditionControl(IconWindowConfig iconWindow, Vector2 areaOrigin, Vector2 areaSize, OverlayControlLayout layout)
    {
        const float padding = 6f;
        var framePadding = new Vector2(7f, 3f);
        var options = DisplayConditionOptions(iconWindow.Role).ToList();
        var currentLabel = options.FirstOrDefault(option => option.Condition == iconWindow.DisplayCondition).Label ?? "\uD56D\uC0C1";
        var comboWidth = GetOverlayConditionComboWidth(iconWindow.Role, framePadding);
        var windowWidth = comboWidth + padding * 2f;
        var windowHeight = GetOverlayControlHeight(framePadding, padding);
        var windowX = Math.Max(areaOrigin.X, areaOrigin.X + areaSize.X - windowWidth);
        var windowY = OverlayControlGeometry.GetTopControlY(areaOrigin, areaSize, windowHeight, row: layout.SplitTopControls ? 1 : 0, layout);

        this.DrawOverlayFloatingWindow(
            $"FFXIVAuraOverlayConditionControl-{iconWindow.Id}",
            new Vector2(windowX, windowY),
            new Vector2(windowWidth, windowHeight),
            0f,
            () =>
        {
            ImGui.PushID($"overlay-condition-control-{iconWindow.Id}");
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
            ImGui.SetCursorPos(new Vector2(padding, padding));
            ImGui.SetNextItemWidth(comboWidth);
            if (ImGui.BeginCombo("##condition", currentLabel))
            {
                foreach (var (condition, label) in options)
                {
                    var selected = iconWindow.DisplayCondition == condition;
                    if (ImGui.Selectable(label, selected) && !selected)
                    {
                        iconWindow.DisplayCondition = condition;
                        if (iconWindow.Role != IconWindowRole.SkillCooldowns)
                        {
                            var auras = this.GetOverlayAuras(iconWindow, OverlayItemVisibility.Layout).ToList();
                            if (auras.Count > 0)
                                this.AddMissingAuraIconPositions(iconWindow, auras, new Vector2(iconWindow.Width, iconWindow.Height));
                        }

                        this.QueueConfigSave();
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            ImGui.PopStyleVar();
            ImGui.PopID();
        });
    }

    private void DrawOverlayNameControl(IconWindowConfig iconWindow, Vector2 areaOrigin, Vector2 areaSize, OverlayControlLayout layout)
    {
        const float padding = 6f;
        var framePadding = new Vector2(7f, 3f);
        var inputWidth = GetOverlayNameInputWidth(areaSize);
        var windowWidth = inputWidth + padding * 2f;
        var windowHeight = GetOverlayControlHeight(framePadding, padding);
        var windowY = OverlayControlGeometry.GetBottomControlY(areaOrigin, areaSize, windowHeight, row: 0, layout);

        this.DrawOverlayFloatingWindow(
            $"FFXIVAuraOverlayNameControl-{iconWindow.Id}",
            new Vector2(areaOrigin.X, windowY),
            new Vector2(windowWidth, windowHeight),
            0.55f,
            () =>
        {
            ImGui.PushID($"overlay-name-control-{iconWindow.Id}");
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.04f, 0.06f, 0.08f, 0.82f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.06f, 0.1f, 0.12f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.08f, 0.16f, 0.18f, 0.95f));
            ImGui.SetCursorPos(new Vector2(padding, padding));
            ImGui.SetNextItemWidth(inputWidth);
            var name = iconWindow.Name;
            if (ImGui.InputText("##name", ref name, 40))
            {
                iconWindow.Name = name;
                this.QueueConfigSave();
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                var normalized = string.IsNullOrWhiteSpace(iconWindow.Name) ? iconWindow.Id : iconWindow.Name.Trim();
                if (!string.Equals(iconWindow.Name, normalized, StringComparison.Ordinal))
                {
                    iconWindow.Name = normalized;
                    this.QueueConfigSave();
                }
            }

            ImGui.PopStyleColor(3);
            ImGui.PopStyleVar();
            ImGui.PopID();
        });
    }

    private OverlayControlLayout GetOverlayControlLayout(IconWindowRole role, Vector2 areaOrigin, Vector2 areaSize)
    {
        const float padding = 6f;
        var framePadding = new Vector2(7f, 3f);
        var controlHeight = GetOverlayControlHeight(framePadding, padding);
        var roleWidth = GetOverlayRoleControlWidth(framePadding, padding);
        var conditionWidth = GetOverlayConditionComboWidth(role, framePadding) + padding * 2f;
        var nameWidth = GetOverlayNameControlWidth(areaSize, padding);
        var alignmentWidth = GetOverlayAlignmentControlWidth(framePadding, padding);

        return OverlayControlGeometry.CreateLayout(
            areaOrigin,
            areaSize,
            ImGui.GetIO().DisplaySize,
            controlHeight,
            roleWidth,
            conditionWidth,
            nameWidth,
            alignmentWidth);
    }

    private static float GetOverlayControlHeight(Vector2 framePadding, float padding)
        => ImGui.GetTextLineHeight() + framePadding.Y * 2f + padding * 2f;

    private static float GetOverlayRoleControlWidth(Vector2 framePadding, float padding)
    {
        var options = WindowRoleOptions().ToList();
        var comboWidth = Math.Max(132f, options.Max(option => ImGui.CalcTextSize(option.Label).X) + framePadding.X * 2f + 26f);
        return comboWidth + padding * 2f;
    }

    private static float GetOverlayConditionComboWidth(IconWindowRole role, Vector2 framePadding)
    {
        var options = DisplayConditionOptions(role).ToList();
        return Math.Max(118f, options.Max(option => ImGui.CalcTextSize(option.Label).X) + framePadding.X * 2f + 26f);
    }

    private static float GetOverlayNameInputWidth(Vector2 areaSize)
        => Math.Clamp(areaSize.X * 0.32f, 120f, 220f);

    private static float GetOverlayNameControlWidth(Vector2 areaSize, float padding)
        => GetOverlayNameInputWidth(areaSize) + padding * 2f;

    private static float GetOverlayAlignmentControlWidth(Vector2 framePadding, float padding)
    {
        const string leftLabel = "\uC67C\uCABD";
        const string centerLabel = "\uAC00\uC6B4\uB370";
        const string rightLabel = "\uC624\uB978\uCABD";
        var itemSpacing = 4f;
        return ImGui.CalcTextSize(leftLabel).X
               + ImGui.CalcTextSize(centerLabel).X
               + ImGui.CalcTextSize(rightLabel).X
               + framePadding.X * 6f
               + itemSpacing * 2f
               + padding * 2f;
    }

    private void DrawOverlayAlignmentControls(IconWindowConfig iconWindow, string job, uint level, Vector2 areaOrigin, Vector2 areaSize, OverlayControlLayout layout)
    {
        const float padding = 6f;
        const string leftLabel = "\uC67C\uCABD";
        const string centerLabel = "\uAC00\uC6B4\uB370";
        const string rightLabel = "\uC624\uB978\uCABD";
        var framePadding = new Vector2(7f, 3f);
        var itemSpacing = new Vector2(4f, 0f);

        var leftWidth = ImGui.CalcTextSize(leftLabel).X + framePadding.X * 2f;
        var centerWidth = ImGui.CalcTextSize(centerLabel).X + framePadding.X * 2f;
        var rightWidth = ImGui.CalcTextSize(rightLabel).X + framePadding.X * 2f;
        var contentWidth = leftWidth + centerWidth + rightWidth + itemSpacing.X * 2f;
        var windowWidth = contentWidth + padding * 2f;
        var windowHeight = GetOverlayControlHeight(framePadding, padding);
        var windowX = Math.Max(areaOrigin.X, areaOrigin.X + areaSize.X - windowWidth);
        var windowY = OverlayControlGeometry.GetBottomControlY(areaOrigin, areaSize, windowHeight, layout.SplitBottomControls ? 1 : 0, layout);

        this.DrawOverlayFloatingWindow(
            $"FFXIVAuraOverlayControls-{iconWindow.Id}",
            new Vector2(windowX, windowY),
            new Vector2(windowWidth, windowHeight),
            0f,
            () =>
        {
            ImGui.PushID($"overlay-controls-{iconWindow.Id}");
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, itemSpacing);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.05f, 0.08f, 0.1f, 0.82f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.08f, 0.32f, 0.38f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.12f, 0.48f, 0.56f, 1f));

            ImGui.SetCursorPos(new Vector2(padding, padding));

            if (this.DrawOverlayControlButton(iconWindow.Alignment == IconAlignment.Left, leftLabel))
                this.ApplyOverlayAlignment(iconWindow, job, level, IconAlignment.Left);

            ImGui.SameLine();
            if (this.DrawOverlayControlButton(iconWindow.Alignment == IconAlignment.Center, centerLabel))
                this.ApplyOverlayAlignment(iconWindow, job, level, IconAlignment.Center);

            ImGui.SameLine();
            if (this.DrawOverlayControlButton(iconWindow.Alignment == IconAlignment.Right, rightLabel))
                this.ApplyOverlayAlignment(iconWindow, job, level, IconAlignment.Right);

            ImGui.PopStyleColor(3);
            ImGui.PopStyleVar(2);
            ImGui.PopID();
        });
    }

    private void DrawOverlayFloatingWindow(string id, Vector2 position, Vector2 size, float backgroundAlpha, Action drawContents)
    {
        ImGui.SetNextWindowPos(ClampOverlayFloatingWindowPosition(position, size), ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(backgroundAlpha);
        if (!ImGui.Begin(id, OverlayControlWindowFlags()))
        {
            ImGui.End();
            return;
        }

        drawContents();
        ImGui.End();
    }

    private static Vector2 ClampOverlayWindowPosition(Vector2 position, Vector2 size)
        => OverlayControlGeometry.ClampWindowPositionToDisplay(
            position,
            size,
            new Vector2(OverlayWindowMargin, OverlayWindowMargin),
            ImGui.GetIO().DisplaySize,
            OverlayWindowMargin);

    private static Vector2 ClampOverlayFloatingWindowPosition(Vector2 position, Vector2 size)
        => OverlayControlGeometry.ClampWindowPositionToDisplay(
            position,
            size,
            new Vector2(OverlayWindowMargin, OverlayWindowMargin),
            ImGui.GetIO().DisplaySize,
            OverlayWindowMargin);

    private static ImGuiWindowFlags OverlayControlWindowFlags()
        => ImGuiWindowFlags.NoTitleBar
           | ImGuiWindowFlags.NoScrollbar
           | ImGuiWindowFlags.NoSavedSettings
           | ImGuiWindowFlags.NoDecoration
           | ImGuiWindowFlags.NoMove;

    private bool DrawOverlayControlButton(bool selected, string label)
    {
        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.14f, 0.5f, 0.58f, 0.96f));

        var clicked = ImGui.Button(label);
        if (selected)
            ImGui.PopStyleColor();

        return clicked;
    }

    private void ApplyOverlayAlignment(IconWindowConfig iconWindow, string job, uint level, IconAlignment alignment)
    {
        if (iconWindow.Alignment == alignment)
            return;

        iconWindow.Alignment = alignment;
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
            this.AlignOverlayIcons(iconWindow, job, level);
        else
            this.AlignAuraIcons(iconWindow);

        this.QueueConfigSave();
    }

    private void ApplyOverlayRole(IconWindowConfig iconWindow, string job, uint level, IconWindowRole role)
    {
        if (iconWindow.Role == role)
            return;

        iconWindow.Role = role;
        iconWindow.DisplayCondition = IconDisplayCondition.Always;
        this.NormalizeActiveWindowIconPositions(iconWindow, job, level);
    }
}
