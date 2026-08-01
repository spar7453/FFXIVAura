namespace FFXIVAura;

public sealed partial class Plugin
{
    private void DrawConfig()
    {
        ImGui.SetNextWindowSize(new Vector2(640f, 760f), ImGuiCond.FirstUseEver);

        // ImGui.Begin must always be paired with ImGui.End, even when the body throws;
        // otherwise the global window stack stays unbalanced for the rest of the frame.
        try
        {
            if (ImGui.Begin("FFXIVAura 설정", ref this.configVisible))
                this.DrawConfigContent();
        }
        finally
        {
            ImGui.End();
        }

        this.DrawAuraSearchWindow();
    }

    private void DrawConfigContent()
    {
        var activeWindow = this.GetActiveIconWindow();
        var job = this.playerFrameContext.IsPlayerLoaded ? this.playerFrameContext.Job : "JOB";
        var level = this.playerFrameContext.EffectiveLevel;

        this.DrawConfigSummary(job, level);
        ImGui.Separator();
        this.DrawIconWindowControls(ref activeWindow);
        ImGui.Separator();

        var changed = false;
        using (var tabBar = ImRaii.TabBar("FFXIVAuraConfigTabs"))
        {
            if (tabBar)
            {
                using (var generalTab = ImRaii.TabItem("일반/표시"))
                {
                    if (generalTab)
                    {
                        ImGui.TextDisabled("전체 설정");
                        changed |= this.DrawGeneralSettings();
                        ImGui.Separator();
                        ImGui.TextDisabled($"현재 창 표시 설정: {IconWindowPresentation.GetDisplayName(activeWindow)}");
                        changed |= this.DrawVisualSettings(activeWindow);
                    }
                }

                using (var overlayTab = ImRaii.TabItem("오버레이/추적"))
                {
                    if (overlayTab)
                    {
                        ImGui.TextDisabled($"현재 창 크기 설정: {IconWindowPresentation.GetDisplayName(activeWindow)}");
                        changed |= this.DrawPartyCooldownLayoutEditMode();
                        ImGui.Separator();
                        changed |= this.DrawOverlayWindowSettings(activeWindow, job, level);
                        ImGui.Separator();
                        this.DrawTrackingSettings(activeWindow, job, level);
                    }
                }
            }
        }

        if (changed)
        {
            this.QueueConfigSave();
            this.InvalidateKeybindCache();
        }
    }

    private void DrawConfigSummary(string job, uint level)
    {
        ImGui.TextUnformatted($"현재 직업: {job} / 레벨: {level}");
        ImGui.TextUnformatted($"로드된 스킬: {this.abilityCatalog.Count}");
        ImGui.TextUnformatted("명령어: /fa");
    }

    private bool DrawGeneralSettings()
    {
        var settings = GeneralSettingsDraft.Create(this.config);
        var changed = false;
        changed |= ImGui.Checkbox("사용", ref settings.Enabled);
        changed |= ImGui.Checkbox("오버레이 이동 잠금", ref settings.LockOverlay);
        changed |= ImGui.Checkbox("지역 이동 중 숨김", ref settings.HideDuringZoneLoad);
        changed |= ImGui.Checkbox("툴팁 표시", ref settings.ShowTooltips);
        changed |= ImGui.Checkbox("성능 계측 표시", ref settings.ShowPerformanceOverlay);
        changed |= ImGui.Checkbox("상세 프로파일 표시 (성능 창 표시)", ref settings.ShowDetailedPerformanceProfile);
        changed |= ImGui.Checkbox("프로파일 자동 기록", ref settings.RecordPerformanceProfile);
        ImGui.SetNextItemWidth(120f);
        changed |= ImGui.InputInt("기록 간격(초)", ref settings.PerformanceProfileRecordIntervalSeconds);
        ImGui.SetNextItemWidth(120f);
        changed |= ImGui.InputInt("최대 파일(MB)", ref settings.PerformanceProfileMaxFileMegabytes);
        ImGui.TextDisabled($"기록 파일: {this.GetPerformanceProfileFilePath()}");
        changed |= ImGui.Checkbox("파티 쿨다운 로그 관측", ref settings.ShowPartyCooldownLogObserver);
        if (ImGui.Button("프로파일 초기화"))
            this.performanceProfiler.Reset();
        ImGui.SameLine();
        if (ImGui.Button("기록 파일 삭제"))
            this.ClearPerformanceProfileFiles();

        return changed && settings.ApplyTo(this.config);
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
        bool clicked;
        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.14f, 0.5f, 0.58f, 0.96f), selected))
        {
            clicked = ImGui.Button(label, new Vector2(110f, 0f));
        }

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
        var definitions = this.partyCooldownCatalog.GetDefinitionsForCategory(category);
        var presetDefinitions = this.partyCooldownCatalog.GetPresetDefinitionsForCategory(category);
        var effectiveDefinitions = this.partyCooldownCatalog.GetEffectiveDefinitionsForCategory(category, level);
        var summary = PartyCooldownTrackingSummaryBuilder.Build(
            presetDefinitions,
            effectiveDefinitions,
            definition => this.partyCooldownCatalog.IsExcluded(activeWindow, definition),
            this.HasPartyCooldownStatusTracking);

        ImGui.TextUnformatted("\uD30C\uD2F0 \uCFE8\uB2E4\uC6B4 \uBCF4\uB4DC");
        ImGui.TextWrapped("\uC774 \uCC3D\uC740 \uC218\uB3D9 \uCD94\uC801 \uBAA9\uB85D \uB300\uC2E0 \uAE30\uBCF8 \uC9C1\uC5C5 \uB370\uC774\uD130\uB85C \uD30C\uD2F0\uC6D0\uC758 \uC0DD\uC874\uAE30/\uD790\uCFE8/\uB51C \uC2DC\uB108\uC9C0\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
        ImGui.TextDisabled($"\uD604\uC7AC \uC720\uD6A8 \uB808\uBCA8 {level}: {summary.VisibleAtLevel}/{summary.AvailableAtLevel}\uAC1C \uD45C\uC2DC, {summary.ExcludedInCategory}\uAC1C \uC81C\uC678");
        if (summary.StatuslessAtLevel > 0)
            ImGui.TextDisabled($"\uC0C1\uD0DC \uCD94\uC801 \uC5C6\uC74C: {summary.StatuslessAtLevel}\uAC1C");

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

        if (summary.ExcludedInCategory > 0)
        {
            if (ImGui.Button("\uC774 \uCC3D\uC758 \uC81C\uC678 \uBAA9\uB85D \uCD08\uAE30\uD654"))
            {
                this.partyCooldownCatalog.RemoveExclusions(activeWindow, definitions);
                this.QueueConfigSave();
            }
        }

        using (ImRaii.Child("FFXIVAuraPartyCooldownPresetList", new Vector2(GetConfigContentWidth(), 280f), true))
        {
            foreach (var group in summary.PresetGroups)
            {
                var label = $"{GetPartyCooldownJobLabel(group.Job)}  {group.VisibleCount}/{group.Definitions.Count}##party-cooldown-job-{group.Job}";
                if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen))
                    continue;

                foreach (var definition in group.Definitions)
                    this.DrawPartyCooldownPresetRow(activeWindow, definition, level);
            }
        }
    }

    private void DrawPartyCooldownPresetRow(IconWindowConfig activeWindow, PartyCooldownDefinition definition, uint level)
    {
        using var idScope = ImRaii.PushId($"party-cooldown-preset-{definition.Id}");
        var included = !this.partyCooldownCatalog.IsExcluded(activeWindow, definition);
        if (ImGui.Checkbox("##include-party-cooldown", ref included))
        {
            this.partyCooldownCatalog.SetExcluded(activeWindow, definition, !included);
            this.QueueConfigSave();
        }

        ImGui.SameLine(0f, 8f);
        this.DrawStatusListIcon(definition.IconId, 22f);
        ImGui.SameLine(0f, 8f);
        ImGui.TextUnformatted($"{definition.Name}  Lv{definition.Level}");
        var effectiveDefinition = this.partyCooldownCatalog.ResolveEffectiveDefinition(
            definition,
            definition.Job,
            level);
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
    }

    private bool HasPartyCooldownStatusTracking(PartyCooldownDefinition definition)
        => this.partyCooldownCatalog.ResolveStatusIds(definition).Count > 0;

    private static string GetPartyCooldownJobLabel(string job)
        => string.Equals(job, "ROLE", StringComparison.OrdinalIgnoreCase) ? "\uACF5\uC6A9" : job;

    private void DrawIconWindowControls(ref IconWindowConfig activeWindow)
    {
        ImGui.TextUnformatted("오버레이 창");
        ImGui.SetNextItemWidth(180f);
        using (var combo = ImRaii.Combo("##overlay-window", IconWindowPresentation.GetDisplayName(activeWindow)))
        {
            if (combo)
            {
                foreach (var window in this.config.IconWindows)
                {
                    var selected = string.Equals(window.Id, this.config.ActiveWindowId, StringComparison.OrdinalIgnoreCase);
                    var displayName = IconWindowPresentation.GetDisplayName(window);
                    if (ImGui.Selectable($"{displayName}##{window.Id}", selected) && !selected)
                    {
                        this.config.ActiveWindowId = window.Id;
                        activeWindow = window;
                        this.QueueConfigSave();
                    }

                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
            }
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

            using (var popup = ImRaii.PopupModal("창 삭제 확인##delete-icon-window", ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (popup)
                {
                    ImGui.TextWrapped($"'{IconWindowPresentation.GetDisplayName(activeWindow)}' 창을 삭제할까요?");
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
                }
            }
        }

        ImGui.Spacing();
    }

    private static float GetConfigContentWidth(float min = 260f)
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f)
            return min;

        return Math.Max(min, width);
    }
}
