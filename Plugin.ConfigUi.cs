namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawConfig()
    {
        ImGui.SetNextWindowSize(new Vector2(640f, 760f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("FFXIVAura 설정", ref this.configVisible))
        {
            ImGui.End();
            this.DrawAuraSearchWindow();
            return;
        }

        if (this.EnsureIconWindows())
            this.QueueConfigSave();

        var activeWindow = this.GetActiveIconWindow();
        var job = PlayerState.IsLoaded ? JobInfo.Code(PlayerState.ClassJob.RowId) : "JOB";
        var level = this.GetCurrentEffectiveLevel();

        this.DrawConfigSummary(job, level);
        ImGui.Separator();
        this.DrawIconWindowControls(ref activeWindow);
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
                changed |= this.DrawOverlayWindowSettings(activeWindow, job, level);
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
        this.DrawAuraSearchWindow();
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
        var showTooltips = this.config.ShowTooltips;
        var showPerformanceOverlay = this.config.ShowPerformanceOverlay;

        changed |= ImGui.Checkbox("사용", ref enabled);
        changed |= ImGui.Checkbox("오버레이 이동 잠금", ref lockOverlay);
        changed |= ImGui.Checkbox("지역 이동 중 숨김", ref hideDuringZoneLoad);
        changed |= ImGui.Checkbox("툴팁 표시", ref showTooltips);
        changed |= ImGui.Checkbox("성능 계측 표시", ref showPerformanceOverlay);

        if (!changed)
            return false;

        this.config.Enabled = enabled;
        this.config.LockOverlay = lockOverlay;
        this.config.HideDuringZoneLoad = hideDuringZoneLoad;
        this.config.ShowTooltips = showTooltips;
        this.config.ShowPerformanceOverlay = showPerformanceOverlay;
        return true;
    }

    private bool DrawOverlayWindowSettings(IconWindowConfig activeWindow, string job, uint level)
    {
        var changed = false;

        var previousIconSize = activeWindow.IconSize;
        var previousGap = activeWindow.Gap;
        var previousWidth = activeWindow.Width;
        var previousHeight = activeWindow.Height;
        var iconSize = activeWindow.IconSize;
        var gap = activeWindow.Gap;
        var overlayWidth = activeWindow.Width;
        var overlayHeight = activeWindow.Height;
        var fontScale = activeWindow.FontScale;

        changed |= ImGui.SliderFloat("아이콘 크기", ref iconSize, MinIconSize, MaxIconSize, "%.0f");
        changed |= ImGui.SliderFloat("간격", ref gap, MinGap, MaxGap, "%.0f");
        changed |= ImGui.SliderFloat("오버레이 너비", ref overlayWidth, MinOverlayWidth, MaxOverlayWidth, "%.0f");
        changed |= ImGui.SliderFloat("오버레이 높이", ref overlayHeight, MinOverlayHeight, MaxOverlayHeight, "%.0f");
        changed |= ImGui.SliderFloat("글자 크기", ref fontScale, MinFontScale, MaxFontScale, "%.2f");

        if (changed)
        {
            activeWindow.IconSize = iconSize;
            activeWindow.Gap = gap;
            activeWindow.Width = overlayWidth;
            activeWindow.Height = overlayHeight;
            activeWindow.FontScale = fontScale;

            var layoutChanged = Math.Abs(previousIconSize - iconSize) > 0.1f
                                || Math.Abs(previousGap - gap) > 0.1f
                                || Math.Abs(previousWidth - overlayWidth) > 0.1f
                                || Math.Abs(previousHeight - overlayHeight) > 0.1f;
            if (layoutChanged)
                this.NormalizeActiveWindowIconPositions(activeWindow, job, level);
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
        var auraVisibilityChanged = false;

        changed |= ImGui.Checkbox("사용 가능 강조", ref highlightReady);
        changed |= ImGui.Checkbox("변환 스킬 강조", ref highlightAdjusted);

        if (activeWindow.Role == IconWindowRole.SkillCooldowns)
            changed |= ImGui.Checkbox("단축키 표시", ref showKeybindText);

        if (activeWindow.Role != IconWindowRole.SkillCooldowns)
        {
            var showMissingChanged = ImGui.Checkbox("없는 버프/디버프 표시", ref showMissingAuras);
            changed |= showMissingChanged;
            auraVisibilityChanged |= showMissingChanged;
        }

        if (activeWindow.Role == IconWindowRole.PartyBuffs)
        {
            var ownOnlyChanged = ImGui.Checkbox("내가 건 파티 버프만 표시", ref partyAurasOwnOnly);
            changed |= ownOnlyChanged;
            auraVisibilityChanged |= ownOnlyChanged;
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
        if (auraVisibilityChanged && activeWindow.Role != IconWindowRole.SkillCooldowns)
        {
            var auras = this.GetOverlayAuras(activeWindow, OverlayItemVisibility.Layout).ToList();
            this.EnsureAuraIconPositions(activeWindow, auras, new Vector2(activeWindow.Width, activeWindow.Height));
        }

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
        ImGui.SetNextItemWidth(180f);
        if (ImGui.BeginCombo("##overlay-window", GetIconWindowDisplayName(activeWindow)))
        {
            foreach (var window in this.config.IconWindows)
            {
                var selected = string.Equals(window.Id, this.config.ActiveWindowId, StringComparison.OrdinalIgnoreCase);
                var displayName = GetIconWindowDisplayName(window);
                if (ImGui.Selectable($"{displayName}##{window.Id}", selected) && !selected)
                {
                    this.config.ActiveWindowId = window.Id;
                    activeWindow = window;
                    this.QueueConfigSave();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(180f);
        var windowName = activeWindow.Name;
        if (ImGui.InputText("창 이름", ref windowName, 40))
        {
            activeWindow.Name = windowName;
            this.QueueConfigSave();
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            var normalized = string.IsNullOrWhiteSpace(activeWindow.Name) ? activeWindow.Id : activeWindow.Name.Trim();
            if (!string.Equals(activeWindow.Name, normalized, StringComparison.Ordinal))
            {
                activeWindow.Name = normalized;
                this.QueueConfigSave();
            }
        }

        if (ImGui.Button("창 추가"))
        {
            activeWindow = this.AddIconWindowClone(activeWindow);
            this.QueueConfigSave();
        }

        if (this.config.IconWindows.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.Button("창 삭제"))
            {
                activeWindow = this.DeleteIconWindow(activeWindow);
                this.QueueConfigSave();
            }
        }

        ImGui.Spacing();
    }

    private static readonly (IconWindowRole Role, string Label)[] WindowRoleOptionItems =
    [
        (IconWindowRole.SkillCooldowns, "스킬"),
        (IconWindowRole.PlayerBuffs, "내 버프"),
        (IconWindowRole.TargetDebuffs, "대상 디버프"),
        (IconWindowRole.PartyBuffs, "파티 버프"),
    ];

    private static readonly (IconDisplayCondition Condition, string Label)[] SkillDisplayConditionOptionItems =
    [
        (IconDisplayCondition.Always, "항상"),
        (IconDisplayCondition.InCombat, "전투 중"),
        (IconDisplayCondition.OutOfCombat, "비전투"),
        (IconDisplayCondition.CoolingOnly, "쿨/불가"),
        (IconDisplayCondition.ReadyOnly, "사용 가능"),
    ];

    private static readonly (IconDisplayCondition Condition, string Label)[] AuraDisplayConditionOptionItems =
    [
        (IconDisplayCondition.Always, "항상"),
        (IconDisplayCondition.InCombat, "전투 중"),
        (IconDisplayCondition.OutOfCombat, "비전투"),
        (IconDisplayCondition.CoolingOnly, "활성"),
        (IconDisplayCondition.ReadyOnly, "없음"),
    ];

    private static IReadOnlyList<(IconWindowRole Role, string Label)> WindowRoleOptions()
        => WindowRoleOptionItems;

    private static IReadOnlyList<(IconDisplayCondition Condition, string Label)> DisplayConditionOptions(IconWindowRole role)
        => role == IconWindowRole.SkillCooldowns ? SkillDisplayConditionOptionItems : AuraDisplayConditionOptionItems;
}
