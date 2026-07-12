namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private IconWindowConfig AddIconWindowClone(IconWindowConfig activeWindow)
    {
        var number = this.GetNextIconWindowNumber();
        this.config.WindowCounter = Math.Max(this.config.WindowCounter, number);
        var id = $"win{number}";
        var windowSize = new Vector2(activeWindow.Width, activeWindow.Height);
        var window = IconWindowClone.CloneForNewWindow(
            activeWindow,
            id,
            $"\uCC3D {number}",
            ClampOverlayWindowPosition(this.GetNewIconWindowPosition(activeWindow), windowSize));

        if (window.FourPlayerLayout is not null && activeWindow.FourPlayerLayout is not null)
        {
            var positionOffset = window.Position - activeWindow.Position;
            var fourPlayerSize = new Vector2(window.FourPlayerLayout.Width, window.FourPlayerLayout.Height);
            window.FourPlayerLayout.Position = ClampOverlayWindowPosition(
                activeWindow.FourPlayerLayout.Position + positionOffset,
                fourPlayerSize);
        }

        if (window.AllianceLayout is not null && activeWindow.AllianceLayout is not null)
        {
            var positionOffset = window.Position - activeWindow.Position;
            var allianceSize = new Vector2(window.AllianceLayout.Width, window.AllianceLayout.Height);
            window.AllianceLayout.Position = ClampOverlayWindowPosition(
                activeWindow.AllianceLayout.Position + positionOffset,
                allianceSize);
        }

        this.config.IconWindows.Add(window);
        this.config.ActiveWindowId = id;
        return window;
    }

    private IconWindowConfig AddDefaultIconWindow(IconWindowConfig activeWindow)
    {
        var number = this.GetNextIconWindowNumber();
        this.config.WindowCounter = Math.Max(this.config.WindowCounter, number);
        var id = $"win{number}";
        var windowSize = new Vector2(activeWindow.Width, activeWindow.Height);
        var window = new IconWindowConfig
        {
            Id = id,
            Name = $"\uCC3D {number}",
            Position = ClampOverlayWindowPosition(this.GetNewIconWindowPosition(activeWindow), windowSize),
            Width = activeWindow.Width,
            Height = activeWindow.Height,
            IconSize = activeWindow.IconSize,
            Gap = activeWindow.Gap,
            FontScale = activeWindow.FontScale,
            OrderEditorHeight = activeWindow.OrderEditorHeight,
            Role = IconWindowRole.SkillCooldowns,
            DisplayCondition = IconDisplayCondition.Always,
            SkillDisplayCondition = IconDisplayCondition.Always,
            AuraDisplayCondition = IconDisplayCondition.Always,
            PartyCooldownDisplayCondition = IconDisplayCondition.Always,
            Alignment = IconAlignment.Center,
            HighlightAdjusted = true,
            ShowKeybindText = true,
            ShowMissingAuras = true,
            ShowPartyAuraCount = true,
        };

        this.config.IconWindows.Add(window);
        this.config.ActiveWindowId = id;
        return window;
    }

    private IconWindowConfig DeleteIconWindow(IconWindowConfig activeWindow)
    {
        var deleteId = activeWindow.Id;
        var deleteIndex = this.config.IconWindows.FindIndex(window => string.Equals(window.Id, deleteId, StringComparison.OrdinalIgnoreCase));
        this.config.IconWindows.RemoveAll(window => string.Equals(window.Id, deleteId, StringComparison.OrdinalIgnoreCase));
        this.RemoveIconWindowRuntimeState(deleteId);

        if (this.config.IconWindows.Count == 0)
        {
            this.EnsureIconWindows();
            return this.GetActiveIconWindow();
        }

        var nextIndex = Math.Clamp(deleteIndex < 0 ? 0 : deleteIndex, 0, this.config.IconWindows.Count - 1);
        var nextWindow = this.config.IconWindows[nextIndex];
        this.config.ActiveWindowId = nextWindow.Id;
        return nextWindow;
    }

    private Vector2 GetNewIconWindowPosition(IconWindowConfig activeWindow)
    {
        var offset = Math.Min(220f, this.config.IconWindows.Count * 28f);
        return activeWindow.Position + new Vector2(offset == 0 ? 28f : offset, 28f);
    }

    private void NormalizeActiveWindowIconPositions(IconWindowConfig activeWindow, string job, uint level)
    {
        var areaSize = new Vector2(activeWindow.Width, activeWindow.Height);
        var items = this.GetOverlayItems(activeWindow, job, level, OverlayItemVisibility.Layout);
        if (activeWindow.Role == IconWindowRole.SkillCooldowns)
        {
            var abilities = items.Abilities;
            this.NormalizeIconPositionsAfterResize(activeWindow, job, abilities, areaSize);
            if (abilities.Count > 0)
                this.AddMissingOverlayIconPositions(activeWindow, job, abilities, areaSize);

            return;
        }

        if (IconWindowRoles.IsPartyCooldownRole(activeWindow.Role))
            return;

        var auras = items.Auras;
        this.NormalizeAuraIconPositionsAfterResize(activeWindow, auras, areaSize);
        this.EnsureAuraIconPositions(activeWindow, auras, areaSize);
    }

    private static string GetIconWindowDisplayName(IconWindowConfig window)
        => string.IsNullOrWhiteSpace(window.Name) ? window.Id : window.Name;

    private int GetNextIconWindowNumber()
    {
        return IconWindowIdentity.GetNextAvailableNumber(
            this.config.IconWindows.Select(window => window.Id),
            this.config.WindowCounter);
    }
}
