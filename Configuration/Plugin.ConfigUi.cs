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
                ImGui.TextDisabled("전체 설정");
                changed |= this.DrawGeneralSettings();
                ImGui.Separator();
                ImGui.TextDisabled($"현재 창 표시 설정: {GetIconWindowDisplayName(activeWindow)}");
                changed |= this.DrawVisualSettings(activeWindow);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("오버레이/추적"))
            {
                ImGui.TextDisabled($"현재 창 크기 설정: {GetIconWindowDisplayName(activeWindow)}");
                changed |= this.DrawPartyCooldownLayoutEditMode();
                ImGui.Separator();
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
        var showDetailedPerformanceProfile = this.config.ShowDetailedPerformanceProfile;
        var recordPerformanceProfile = this.config.RecordPerformanceProfile;
        var performanceProfileRecordIntervalSeconds = this.config.PerformanceProfileRecordIntervalSeconds;
        var performanceProfileMaxFileMegabytes = this.config.PerformanceProfileMaxFileMegabytes;
        var showPartyCooldownLogObserver = this.config.ShowPartyCooldownLogObserver;

        changed |= ImGui.Checkbox("사용", ref enabled);
        changed |= ImGui.Checkbox("오버레이 이동 잠금", ref lockOverlay);
        changed |= ImGui.Checkbox("지역 이동 중 숨김", ref hideDuringZoneLoad);
        changed |= ImGui.Checkbox("툴팁 표시", ref showTooltips);
        changed |= ImGui.Checkbox("성능 계측 표시", ref showPerformanceOverlay);
        changed |= ImGui.Checkbox("상세 프로파일 표시 (성능 창 표시)", ref showDetailedPerformanceProfile);
        changed |= ImGui.Checkbox("프로파일 자동 기록", ref recordPerformanceProfile);
        ImGui.SetNextItemWidth(120f);
        changed |= ImGui.InputInt("기록 간격(초)", ref performanceProfileRecordIntervalSeconds);
        ImGui.SetNextItemWidth(120f);
        changed |= ImGui.InputInt("최대 파일(MB)", ref performanceProfileMaxFileMegabytes);
        ImGui.TextDisabled($"기록 파일: {this.GetPerformanceProfileFilePath()}");
        changed |= ImGui.Checkbox("파티 쿨다운 로그 관측", ref showPartyCooldownLogObserver);
        if (ImGui.Button("프로파일 초기화"))
            this.performanceProfiler.Reset();
        ImGui.SameLine();
        if (ImGui.Button("기록 파일 삭제"))
            this.ClearPerformanceProfileFiles();

        if (!changed)
            return false;

        this.config.Enabled = enabled;
        this.config.LockOverlay = lockOverlay;
        this.config.HideDuringZoneLoad = hideDuringZoneLoad;
        this.config.ShowTooltips = showTooltips;
        this.config.ShowPerformanceOverlay = showPerformanceOverlay;
        this.config.ShowDetailedPerformanceProfile = showDetailedPerformanceProfile;
        this.config.RecordPerformanceProfile = recordPerformanceProfile;
        this.config.PerformanceProfileRecordIntervalSeconds = Math.Clamp(
            performanceProfileRecordIntervalSeconds,
            MinPerformanceProfileRecordIntervalSeconds,
            MaxPerformanceProfileRecordIntervalSeconds);
        this.config.PerformanceProfileMaxFileMegabytes = Math.Clamp(
            performanceProfileMaxFileMegabytes,
            MinPerformanceProfileMaxFileMegabytes,
            MaxPerformanceProfileMaxFileMegabytes);
        this.config.ShowPartyCooldownLogObserver = showPartyCooldownLogObserver;
        return true;
    }

    private bool DrawOverlayWindowSettings(IconWindowConfig activeWindow, string job, uint level)
    {
        if (!IconWindowRoles.IsPartyCooldownRole(activeWindow.Role))
            return this.DrawOverlayWindowLayoutSettings(activeWindow, IconWindowLayoutBinding.Regular(activeWindow), job, level);

        var layoutBinding = IconWindowLayoutBinding.PartyCooldown(
            activeWindow,
            this.config.PartyCooldownLayoutEditMode,
            out var created);
        return created | this.DrawOverlayWindowLayoutSettings(activeWindow, layoutBinding, job, level);
    }

    private bool DrawPartyCooldownLayoutEditMode()
    {
        var mode = this.config.PartyCooldownLayoutEditMode;
        var changed = false;
        ImGui.TextUnformatted("\uD30C\uD2F0 \uBCF4\uB4DC \uD3B8\uC9D1 \uB808\uC774\uC544\uC6C3");

        changed |= DrawPartyCooldownLayoutModeButton(
            ref mode,
            PartyCooldownLayoutEditMode.FourPlayer,
            "4\uC778 \uB358\uC804");
        ImGui.SameLine();
        changed |= DrawPartyCooldownLayoutModeButton(
            ref mode,
            PartyCooldownLayoutEditMode.EightPlayer,
            "8\uC778 \uC77C\uBC18");
        ImGui.SameLine();
        changed |= DrawPartyCooldownLayoutModeButton(
            ref mode,
            PartyCooldownLayoutEditMode.Alliance,
            "24\uC778 \uC5F0\uD569");

        if (changed)
            this.config.PartyCooldownLayoutEditMode = mode;

        return changed;
    }

    private static bool DrawPartyCooldownLayoutModeButton(
        ref PartyCooldownLayoutEditMode mode,
        PartyCooldownLayoutEditMode candidate,
        string label)
    {
        var selected = mode == candidate;
        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.14f, 0.5f, 0.58f, 0.96f));

        var clicked = ImGui.Button(label, new Vector2(110f, 0f));
        if (selected)
            ImGui.PopStyleColor();
        if (!clicked || selected)
            return false;

        mode = candidate;
        return true;
    }

    private bool DrawOverlayWindowLayoutSettings(
        IconWindowConfig activeWindow,
        IconWindowLayoutBinding layoutBinding,
        string job,
        uint level)
    {
        var changed = false;

        var previousIconSize = layoutBinding.IconSize;
        var previousGap = layoutBinding.Gap;
        var previousWidth = layoutBinding.Width;
        var previousHeight = layoutBinding.Height;
        var iconSize = layoutBinding.IconSize;
        var gap = layoutBinding.Gap;
        var overlayWidth = layoutBinding.Width;
        var overlayHeight = layoutBinding.Height;
        var fontScale = layoutBinding.FontScale;

        changed |= ImGui.SliderFloat("아이콘 크기", ref iconSize, MinIconSize, MaxIconSize, "%.0f");
        changed |= ImGui.SliderFloat("간격", ref gap, MinGap, MaxGap, "%.0f");
        changed |= ImGui.SliderFloat("오버레이 너비", ref overlayWidth, MinOverlayWidth, MaxOverlayWidth, "%.0f");
        changed |= ImGui.SliderFloat("오버레이 높이", ref overlayHeight, MinOverlayHeight, MaxOverlayHeight, "%.0f");
        changed |= ImGui.SliderFloat("글자 크기", ref fontScale, MinFontScale, MaxFontScale, "%.2f");

        if (changed)
        {
            layoutBinding.IconSize = iconSize;
            layoutBinding.Gap = gap;
            layoutBinding.Width = overlayWidth;
            layoutBinding.Height = overlayHeight;
            layoutBinding.FontScale = fontScale;

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

        if (IconWindowRoles.IsStandardAuraRole(activeWindow.Role))
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
        if (auraVisibilityChanged && IconWindowRoles.IsStandardAuraRole(activeWindow.Role))
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
        else if (IconWindowRoles.IsPartyCooldownRole(activeWindow.Role))
            this.DrawPartyCooldownTrackingInfo(activeWindow, level);
        else
            this.DrawAuraTrackerEditor(activeWindow);
    }

    private void DrawPartyCooldownTrackingInfo(IconWindowConfig activeWindow, uint level)
    {
        var category = IconWindowRoles.GetPartyCooldownCategory(activeWindow.Role);
        var definitions = this.GetPartyCooldownDefinitionsForCategory(category);
        var presetDefinitions = this.GetPartyCooldownPresetDefinitionsForCategory(category);
        var effectiveDefinitions = this.GetPartyCooldownEffectiveDefinitionsForCategory(category, level);
        var availableAtLevel = effectiveDefinitions.Count;
        var visibleAtLevel = effectiveDefinitions.Count(definition => !this.IsPartyCooldownExcluded(activeWindow, definition));
        var excludedInCategory = presetDefinitions.Count(definition => this.IsPartyCooldownExcluded(activeWindow, definition));
        var statuslessAtLevel = effectiveDefinitions.Count(definition =>
            !this.IsPartyCooldownExcluded(activeWindow, definition)
            && !this.HasPartyCooldownStatusTracking(definition));

        ImGui.TextUnformatted("\uD30C\uD2F0 \uCFE8\uB2E4\uC6B4 \uBCF4\uB4DC");
        ImGui.TextWrapped("\uC774 \uCC3D\uC740 \uC218\uB3D9 \uCD94\uC801 \uBAA9\uB85D \uB300\uC2E0 \uAE30\uBCF8 \uC9C1\uC5C5 \uB370\uC774\uD130\uB85C \uD30C\uD2F0\uC6D0\uC758 \uC0DD\uC874\uAE30/\uD790\uCFE8/\uB51C \uC2DC\uB108\uC9C0\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
        ImGui.TextDisabled($"\uD604\uC7AC \uC720\uD6A8 \uB808\uBCA8 {level}: {visibleAtLevel}/{availableAtLevel}\uAC1C \uD45C\uC2DC, {excludedInCategory}\uAC1C \uC81C\uC678");
        if (statuslessAtLevel > 0)
            ImGui.TextDisabled($"\uC0C1\uD0DC \uCD94\uC801 \uC5C6\uC74C: {statuslessAtLevel}\uAC1C");

        var layoutMode = this.GetPartyCooldownLayoutMode();
        var activeLayout = IconWindowLayoutBinding.PartyCooldown(activeWindow, layoutMode, out var layoutCreated);
        if (layoutCreated)
            this.QueueConfigSave();

        var recommendedHeight = this.GetEstimatedPartyCooldownBoardHeight(activeWindow, activeLayout, level);
        if (recommendedHeight > activeLayout.Height + 0.5f)
        {
            ImGui.TextDisabled($"\uD604\uC7AC \uB192\uC774 {activeLayout.Height:0}\uC5D0\uC11C \uC544\uB798 \uD589\uC774 \uC798\uB9B4 \uC218 \uC788\uC2B5\uB2C8\uB2E4. \uAD8C\uC7A5 {recommendedHeight:0}");
            if (ImGui.Button("\uAD8C\uC7A5 \uB192\uC774 \uC801\uC6A9"))
            {
                activeLayout.Height = Math.Clamp(recommendedHeight, MinOverlayHeight, MaxOverlayHeight);
                this.QueueConfigSave();
            }
        }

        if (excludedInCategory > 0)
        {
            if (ImGui.Button("\uC774 \uCC3D\uC758 \uC81C\uC678 \uBAA9\uB85D \uCD08\uAE30\uD654"))
            {
                this.RemovePartyCooldownExclusions(activeWindow, definitions);
                this.QueueConfigSave();
            }
        }

        ImGui.BeginChild("FFXIVAuraPartyCooldownPresetList", new Vector2(GetConfigContentWidth(), 280f), true);
        foreach (var group in presetDefinitions.GroupBy(definition => definition.Job))
        {
            var groupDefinitions = group.ToList();
            var groupVisible = groupDefinitions.Count(definition => !this.IsPartyCooldownExcluded(activeWindow, definition));
            var label = $"{GetPartyCooldownJobLabel(group.Key)}  {groupVisible}/{groupDefinitions.Count}##party-cooldown-job-{group.Key}";
            if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen))
                continue;

            foreach (var definition in groupDefinitions)
                this.DrawPartyCooldownPresetRow(activeWindow, definition, level);
        }

        ImGui.EndChild();
    }

    private void DrawPartyCooldownPresetRow(IconWindowConfig activeWindow, PartyCooldownDefinition definition, uint level)
    {
        ImGui.PushID($"party-cooldown-preset-{definition.Id}");
        var included = !this.IsPartyCooldownExcluded(activeWindow, definition);
        if (ImGui.Checkbox("##include-party-cooldown", ref included))
        {
            this.SetPartyCooldownExcluded(activeWindow, definition, !included);
            this.QueueConfigSave();
        }

        ImGui.SameLine(0f, 8f);
        this.DrawStatusListIcon(definition.IconId, 22f);
        ImGui.SameLine(0f, 8f);
        ImGui.TextUnformatted($"{definition.Name}  Lv{definition.Level}");
        var effectiveDefinition = this.ResolveEffectivePartyCooldownDefinition(definition, definition.Job, level);
        if (definition.Level > level)
        {
            ImGui.SameLine();
            if (definition.ActionId != effectiveDefinition.ActionId
                && !string.Equals(definition.Id, effectiveDefinition.Id, StringComparison.OrdinalIgnoreCase))
                ImGui.TextDisabled($"\uD604\uC7AC \uB808\uBCA8\uC5D0\uC11C\uB294 {effectiveDefinition.Name}\uB85C \uCD94\uC801");
            else
                ImGui.TextDisabled("\uD604\uC7AC \uB808\uBCA8 \uC0AC\uC6A9 \uBD88\uAC00");
        }

        if (!this.HasPartyCooldownStatusTracking(effectiveDefinition))
        {
            ImGui.SameLine();
            ImGui.TextDisabled("\uC0C1\uD0DC \uCD94\uC801 \uC5C6\uC74C");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("\uC774 \uD56D\uBAA9\uC740 \uD655\uC778 \uAC00\uB2A5\uD55C \uBC84\uD504/\uB514\uBC84\uD504 \uC0C1\uD0DC\uAC00 \uC5C6\uC5B4 \uD604\uC7AC \uBC29\uC2DD\uC73C\uB85C\uB294 \uC0AC\uC6A9 \uC9C1\uD6C4 \uCFE8\uB2E4\uC6B4\uC744 \uCD94\uC815\uD558\uAE30 \uC5B4\uB824\uC6B8 \uC218 \uC788\uC2B5\uB2C8\uB2E4.");
        }

        ImGui.PopID();
    }

    private bool HasPartyCooldownStatusTracking(PartyCooldownDefinition definition)
        => this.ResolvePartyCooldownStatusIds(definition).Count > 0;

    private static string GetPartyCooldownJobLabel(string job)
        => string.Equals(job, "ROLE", StringComparison.OrdinalIgnoreCase) ? "\uACF5\uC6A9" : job;

    private static int GetPartyCooldownJobSortOrder(string job)
    {
        for (var index = 0; index < PartyCooldownJobOrder.Length; index++)
        {
            if (string.Equals(PartyCooldownJobOrder[index], job, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return int.MaxValue;
    }

    private static int ComparePartyCooldownDefinitionForUi(PartyCooldownDefinition left, PartyCooldownDefinition right)
    {
        var jobCompare = GetPartyCooldownJobSortOrder(left.Job).CompareTo(GetPartyCooldownJobSortOrder(right.Job));
        if (jobCompare != 0)
            return jobCompare;

        var levelCompare = left.Level.CompareTo(right.Level);
        if (levelCompare != 0)
            return levelCompare;

        var cooldownCompare = right.Cooldown.CompareTo(left.Cooldown);
        if (cooldownCompare != 0)
            return cooldownCompare;

        return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
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

        if (ImGui.Button("기본 창 추가"))
        {
            activeWindow = this.AddDefaultIconWindow(activeWindow);
            this.QueueConfigSave();
        }

        ImGui.SameLine();
        if (ImGui.Button("현재 창 복제"))
        {
            activeWindow = this.AddIconWindowClone(activeWindow);
            this.QueueConfigSave();
        }

        if (this.config.IconWindows.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.Button("창 삭제"))
                ImGui.OpenPopup("창 삭제 확인##delete-icon-window");

            if (ImGui.BeginPopupModal("창 삭제 확인##delete-icon-window", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped($"'{GetIconWindowDisplayName(activeWindow)}' 창을 삭제할까요?");
                ImGui.TextDisabled("추적 목록과 아이콘 위치 설정도 함께 제거됩니다.");
                ImGui.Spacing();

                if (ImGui.Button("삭제", new Vector2(84f, 0f)))
                {
                    activeWindow = this.DeleteIconWindow(activeWindow);
                    this.QueueConfigSave();
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button("취소", new Vector2(84f, 0f)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
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
        (IconWindowRole.PartyDefensives, "\uD30C\uD2F0 \uC0DD\uC874\uAE30"),
        (IconWindowRole.PartyHealingCooldowns, "\uD30C\uD2F0 \uD790\uCFE8"),
        (IconWindowRole.PartySynergies, "\uD30C\uD2F0 \uB51C \uC2DC\uB108\uC9C0"),
    ];

    private static readonly string[] PartyCooldownJobOrder =
    [
        "ROLE",
        "PLD", "WAR", "DRK", "GNB",
        "WHM", "SCH", "AST", "SGE",
        "MNK", "DRG", "NIN", "SAM", "RPR", "VPR",
        "BRD", "MCH", "DNC",
        "BLM", "SMN", "RDM", "PCT",
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

    private static float GetConfigContentWidth(float min = 260f)
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f)
            return min;

        return Math.Max(min, width);
    }
}
