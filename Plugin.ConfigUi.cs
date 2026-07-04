namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawConfig()
    {
        ImGui.SetNextWindowSize(new Vector2(640f, 760f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("FFXIVAura 설정", ref this.configVisible))
        {
            ImGui.End();
            return;
        }

        this.EnsureIconWindows();
        var activeWindow = this.GetActiveIconWindow();
        var job = PlayerState.IsLoaded ? JobInfo.Code(PlayerState.ClassJob.RowId) : "JOB";
        var level = this.GetCurrentEffectiveLevel();

        this.DrawConfigSummary(job, level);
        ImGui.Separator();

        var changed = false;
        if (ImGui.BeginTabBar("FFXIVAuraConfigTabs"))
        {
            if (ImGui.BeginTabItem("일반/표시"))
            {
                changed |= this.DrawGeneralSettings();
                ImGui.Separator();
                changed |= this.DrawVisualSettings(activeWindow);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("오버레이/추적"))
            {
                this.DrawIconWindowControls(ref activeWindow);
                changed |= this.DrawOverlayWindowSettings(activeWindow);
                ImGui.Separator();
                this.DrawTrackingSettings(activeWindow, job, level);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        if (changed)
        {
            this.QueueConfigSave();
            this.InvalidateKeybindCache();
        }

        ImGui.End();
    }

    private uint GetCurrentEffectiveLevel()
    {
        return (uint)(PlayerState.EffectiveLevel > 0 ? PlayerState.EffectiveLevel : PlayerState.Level);
    }

    private void DrawConfigSummary(string job, uint level)
    {
        ImGui.TextUnformatted($"현재 직업: {job} / 레벨: {level}");
        ImGui.TextUnformatted($"로드된 스킬: {this.abilities.Count}");
        ImGui.TextUnformatted("명령어: /fa");
    }

    private bool DrawGeneralSettings()
    {
        var changed = false;
        var enabled = this.config.Enabled;
        var lockOverlay = this.config.LockOverlay;
        var hideDuringZoneLoad = this.config.HideDuringZoneLoad;

        changed |= ImGui.Checkbox("사용", ref enabled);
        changed |= ImGui.Checkbox("오버레이 이동 잠금", ref lockOverlay);
        changed |= ImGui.Checkbox("지역 이동 중 숨김", ref hideDuringZoneLoad);

        if (!changed)
            return false;

        this.config.Enabled = enabled;
        this.config.LockOverlay = lockOverlay;
        this.config.HideDuringZoneLoad = hideDuringZoneLoad;
        return true;
    }

    private bool DrawOverlayWindowSettings(IconWindowConfig activeWindow)
    {
        var changed = false;
        changed |= this.DrawWindowRoleSelector(activeWindow);
        changed |= this.DrawDisplayConditionSelector(activeWindow);

        ImGui.Spacing();
        var iconSize = activeWindow.IconSize;
        var gap = activeWindow.Gap;
        var overlayWidth = activeWindow.Width;
        var overlayHeight = activeWindow.Height;
        var fontScale = activeWindow.FontScale;

        changed |= ImGui.SliderFloat("아이콘 크기", ref iconSize, 24f, 72f, "%.0f");
        changed |= ImGui.SliderFloat("간격", ref gap, 0f, 16f, "%.0f");
        changed |= ImGui.SliderFloat("오버레이 너비", ref overlayWidth, 120f, 1200f, "%.0f");
        changed |= ImGui.SliderFloat("오버레이 높이", ref overlayHeight, 40f, 400f, "%.0f");
        changed |= ImGui.SliderFloat("글자 크기", ref fontScale, 0.75f, 1.5f, "%.2f");

        if (changed)
        {
            activeWindow.IconSize = iconSize;
            activeWindow.Gap = gap;
            activeWindow.Width = overlayWidth;
            activeWindow.Height = overlayHeight;
            activeWindow.FontScale = fontScale;
        }

        return changed;
    }

    private bool DrawVisualSettings(IconWindowConfig activeWindow)
    {
        var changed = false;
        var highlightReady = activeWindow.HighlightReady;
        var highlightAdjusted = activeWindow.HighlightAdjusted;
        var showKeybindText = activeWindow.ShowKeybindText;
        var showMissingAuras = activeWindow.ShowMissingAuras;
        var partyAurasOwnOnly = activeWindow.PartyAurasOwnOnly;
        var showPartyAuraCount = activeWindow.ShowPartyAuraCount;

        changed |= ImGui.Checkbox("사용 가능 강조", ref highlightReady);
        changed |= ImGui.Checkbox("변환 스킬 강조", ref highlightAdjusted);

        if (activeWindow.Role == IconWindowRole.SkillCooldowns)
            changed |= ImGui.Checkbox("단축키 표시", ref showKeybindText);

        if (activeWindow.Role != IconWindowRole.SkillCooldowns)
            changed |= ImGui.Checkbox("없는 버프/디버프 표시", ref showMissingAuras);

        if (activeWindow.Role == IconWindowRole.PartyBuffs)
        {
            changed |= ImGui.Checkbox("내가 건 파티 버프만 표시", ref partyAurasOwnOnly);
            changed |= ImGui.Checkbox("파티 적용 인원 표시", ref showPartyAuraCount);
        }

        if (!changed)
            return false;

        activeWindow.HighlightReady = highlightReady;
        activeWindow.HighlightAdjusted = highlightAdjusted;
        activeWindow.ShowKeybindText = showKeybindText;
        activeWindow.ShowMissingAuras = showMissingAuras;
        activeWindow.PartyAurasOwnOnly = partyAurasOwnOnly;
        activeWindow.ShowPartyAuraCount = showPartyAuraCount;
        return true;
    }

    private void DrawTrackingSettings(IconWindowConfig activeWindow, string job, uint level)
    {
        if (activeWindow.Role == IconWindowRole.SkillCooldowns)
            this.DrawTrackedSkillEditor(activeWindow, job, level);
        else
            this.DrawAuraTrackerEditor(activeWindow);
    }

    private void DrawIconWindowControls(ref IconWindowConfig activeWindow)
    {
        ImGui.TextUnformatted("오버레이 창");
        foreach (var window in this.config.IconWindows)
        {
            if (window != this.config.IconWindows[0])
                ImGui.SameLine();

            if (ImGui.Selectable(window.Name, string.Equals(window.Id, this.config.ActiveWindowId, StringComparison.OrdinalIgnoreCase), ImGuiSelectableFlags.DontClosePopups, new Vector2(72f, 0f)))
            {
                this.config.ActiveWindowId = window.Id;
                activeWindow = window;
                this.QueueConfigSave();
            }
        }

        ImGui.SetNextItemWidth(180f);
        var windowName = activeWindow.Name;
        if (ImGui.InputText("창 이름", ref windowName, 40))
        {
            activeWindow.Name = string.IsNullOrWhiteSpace(windowName) ? activeWindow.Id : windowName.Trim();
            this.QueueConfigSave();
        }

        if (ImGui.Button("창 추가"))
        {
            var number = this.GetNextIconWindowNumber();
            this.config.WindowCounter = Math.Max(this.config.WindowCounter, number);
            var id = $"win{number}";
            var offset = Math.Min(220f, this.config.IconWindows.Count * 28f);
            var window = new IconWindowConfig
            {
                Id = id,
                Name = $"창 {number}",
                Position = activeWindow.Position + new Vector2(offset == 0 ? 28f : offset, 28f),
                Width = activeWindow.Width,
                Height = activeWindow.Height,
                IconSize = activeWindow.IconSize,
                Gap = activeWindow.Gap,
                FontScale = activeWindow.FontScale,
                OrderEditorHeight = activeWindow.OrderEditorHeight,
                Role = activeWindow.Role,
                DisplayCondition = activeWindow.DisplayCondition,
                Alignment = activeWindow.Alignment,
                HighlightReady = activeWindow.HighlightReady,
                HighlightAdjusted = activeWindow.HighlightAdjusted,
                ShowKeybindText = activeWindow.ShowKeybindText,
                ShowMissingAuras = activeWindow.ShowMissingAuras,
                PartyAurasOwnOnly = activeWindow.PartyAurasOwnOnly,
                ShowPartyAuraCount = activeWindow.ShowPartyAuraCount,
            };
            this.config.IconWindows.Add(window);
            this.config.ActiveWindowId = id;
            activeWindow = window;
            this.QueueConfigSave();
        }

        if (this.config.IconWindows.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.Button("창 삭제"))
            {
                var deleteId = activeWindow.Id;
                this.config.IconWindows.RemoveAll(window => string.Equals(window.Id, deleteId, StringComparison.OrdinalIgnoreCase));
                this.config.ActiveWindowId = this.config.IconWindows[0].Id;
                activeWindow = this.config.IconWindows[0];
                this.QueueConfigSave();
            }
        }

        ImGui.Spacing();
    }

    private int GetNextIconWindowNumber()
    {
        var used = this.config.IconWindows
            .Select(window => window.Id)
            .Where(id => id.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            .Select(id => int.TryParse(id[3..], out var number) ? number : 0)
            .Where(number => number > 0)
            .ToHashSet();

        for (var number = 1; number < 1000; number++)
        {
            if (!used.Contains(number))
                return number;
        }

        return used.Count + 1;
    }

    private bool DrawWindowRoleSelector(IconWindowConfig activeWindow)
    {
        var changed = false;
        ImGui.TextUnformatted("창 역할");
        foreach (var (role, label) in WindowRoleOptions())
        {
            if (role != IconWindowRole.SkillCooldowns)
                ImGui.SameLine();

            if (ImGui.Selectable(label, activeWindow.Role == role, ImGuiSelectableFlags.DontClosePopups, new Vector2(108f, 0f)))
            {
                activeWindow.Role = role;
                changed = true;
            }
        }

        return changed;
    }

    private bool DrawDisplayConditionSelector(IconWindowConfig activeWindow)
    {
        var changed = false;
        ImGui.TextUnformatted("표시 조건");
        foreach (var (condition, label) in DisplayConditionOptions())
        {
            if (condition != IconDisplayCondition.Always)
                ImGui.SameLine();

            if (ImGui.Selectable(label, activeWindow.DisplayCondition == condition, ImGuiSelectableFlags.DontClosePopups, new Vector2(96f, 0f)))
            {
                activeWindow.DisplayCondition = condition;
                changed = true;
            }
        }

        return changed;
    }

    private static IEnumerable<(IconWindowRole Role, string Label)> WindowRoleOptions()
    {
        yield return (IconWindowRole.SkillCooldowns, "스킬");
        yield return (IconWindowRole.PlayerBuffs, "내 버프");
        yield return (IconWindowRole.TargetDebuffs, "대상 디버프");
        yield return (IconWindowRole.PartyBuffs, "파티 버프");
    }

    private static IEnumerable<(IconDisplayCondition Condition, string Label)> DisplayConditionOptions()
    {
        yield return (IconDisplayCondition.Always, "항상");
        yield return (IconDisplayCondition.InCombat, "전투 중");
        yield return (IconDisplayCondition.OutOfCombat, "비전투");
        yield return (IconDisplayCondition.CoolingOnly, "활성/쿨중");
        yield return (IconDisplayCondition.ReadyOnly, "준비/없음");
    }
}
