namespace FFXIVAura;

public sealed partial class Plugin
{
    private IReadOnlyList<PartyCooldownMemberRow> BuildPartyCooldownRows(
        IconWindowConfig iconWindow,
        uint level)
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.PartyRows);
        try
        {
            var category = IconWindowRoles.GetPartyCooldownCategory(iconWindow.Role);
            var frameSnapshot = this.GetPartyCooldownFrameSnapshot();
            var members = frameSnapshot.DisplayMembers;

            var rowBuffer = this.partyCooldownRuntimeStore.GetOrCreateRowBuffer(iconWindow.Id);
            rowBuffer.BeginFrame(members.Count);
            var rows = rowBuffer.Rows;
            var candidateItemCount = 0;
            var hiddenByDisplayConditionCount = 0;
            var statuslessCandidateCount = 0;

            foreach (var member in members)
            {
                var memberLevel = member.ResolveEffectiveLevel(level);
                var definitions = this.partyCooldownCatalog.GetEffectiveDefinitionsForMember(
                    category,
                    member.Job,
                    memberLevel);
                var items = rowBuffer.GetMemberItems(member.Key, definitions.Count);
                foreach (var definition in definitions)
                {
                    if (this.partyCooldownCatalog.IsExcluded(iconWindow, definition))
                        continue;

                    candidateItemCount++;
                    if (!this.HasPartyCooldownStatusTracking(definition))
                        statuslessCandidateCount++;

                    var item = this.BuildPartyCooldownDisplayItem(
                        member,
                        definition,
                        memberLevel,
                        frameSnapshot.TimestampUtc);
                    var visibleItem = this.FilterPartyCooldownDisplayCondition(iconWindow, item);
                    if (visibleItem is { } visibleValue)
                        items.Add(visibleValue);
                    else
                        hiddenByDisplayConditionCount++;
                }

                if (items.Count > 0 || !PartyCooldownBoardLayout.HideEmptyRows(category))
                    rows.Add(new PartyCooldownMemberRow(member, items));
            }

            this.RememberPartyCooldownWindowDiagnostics(
                iconWindow,
                rows,
                candidateItemCount,
                hiddenByDisplayConditionCount,
                statuslessCandidateCount);
            rowBuffer.EndFrame();
            this.PrunePartyCooldownRuntime(members, level);
            return rows;
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.PartyRows, profileStart);
        }
    }

    private PartyCooldownFrameSnapshot GetPartyCooldownFrameSnapshot()
    {
        if (this.partyCooldownFrameSnapshot is { } cached)
            return cached;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.PartyRoster);
        try
        {
            var roster = this.partyCooldownRosterService.GetRoster();
            var members = roster.Members;
            var displayMembers = roster.DisplayMembers;
            this.RebuildPartyCooldownActiveStatusIndex(displayMembers);
            var snapshot = new PartyCooldownFrameSnapshot(
                members,
                displayMembers,
                roster.Diagnostics,
                DateTime.UtcNow);
            this.partyCooldownFrameSnapshot = snapshot;
            return snapshot;
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.PartyRoster, profileStart);
        }
    }

    private void PrunePartyCooldownRuntime(
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        uint level)
    {
        if (this.partyCooldownRuntimeStore.BeginLiveKeyCollection())
        {
            foreach (var window in this.config.IconWindows)
            {
                if (!IconWindowRoles.IsPartyCooldownRole(window.Role))
                    continue;

                var category = IconWindowRoles.GetPartyCooldownCategory(window.Role);
                foreach (var member in members)
                {
                    var memberLevel = member.ResolveEffectiveLevel(level);
                    foreach (var definition in this.partyCooldownCatalog.GetDefinitionsForMember(
                                 category,
                                 member.Job,
                                 memberLevel,
                                 window))
                    {
                        this.partyCooldownRuntimeStore.MarkLiveKey(
                            this.partyCooldownCatalog.CreateRuntimeKey(member.Key, definition));
                    }
                }
            }

            this.partyCooldownRuntimeStore.CompleteLiveKeyCollection();
        }

        this.partyCooldownRuntimeStore.PruneStates();
    }

    private float GetEstimatedPartyCooldownBoardHeight(
        IconWindowConfig iconWindow,
        IconWindowLayoutBinding layoutBinding,
        uint level)
    {
        var category = IconWindowRoles.GetPartyCooldownCategory(iconWindow.Role);
        var members = this.GetPartyCooldownFrameSnapshot().DisplayMembers;
        var rows = new List<PartyCooldownBoardLayoutRow>(members.Count);
        foreach (var member in members)
        {
            var memberLevel = member.ResolveEffectiveLevel(level);
            var itemCount = this.partyCooldownCatalog
                .GetDefinitionsForMember(category, member.Job, memberLevel, iconWindow)
                .Count();
            if (itemCount > 0 || !PartyCooldownBoardLayout.HideEmptyRows(category))
                rows.Add(new PartyCooldownBoardLayoutRow(member.AllianceGroup, itemCount));
        }

        var environment = new PartyCooldownBoardLayoutEnvironment(
            ImGui.GetIO().DisplaySize.Y,
            ImGui.GetTextLineHeight(),
            ImGui.CalcTextSize("C").X);
        return PartyCooldownBoardLayoutCalculator.Calculate(
            layoutBinding,
            rows,
            environment).RequiredHeight;
    }

    private bool IsPartyCooldownTrackedByAnyWindow(
        PartyCooldownDefinition definition,
        string job,
        uint level)
    {
        if (!this.partyCooldownCatalog.IsForJob(definition, job))
            return false;

        foreach (var window in this.config.IconWindows)
        {
            if (!IconWindowRoles.IsPartyCooldownRole(window.Role))
                continue;

            var category = IconWindowRoles.GetPartyCooldownCategory(window.Role);
            if (!PartyCooldownCatalog.IsCategory(definition, category))
                continue;

            foreach (var visibleDefinition in this.partyCooldownCatalog.GetDefinitionsForMember(
                         category,
                         job,
                         level,
                         window))
            {
                if (this.partyCooldownCatalog.DefinitionsMatch(definition, visibleDefinition))
                    return true;
            }
        }

        return false;
    }

    private static bool TryFindPartyCooldownMemberByEntityId(
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        uint entityId,
        out PartyCooldownMemberSnapshot member)
    {
        foreach (var candidate in displayMembers)
        {
            if (candidate.EntityId == entityId)
            {
                member = candidate;
                return true;
            }
        }

        member = default;
        return false;
    }

    private static bool IsValidPartyCooldownEntityId(uint entityId)
        => PartyCooldownOwnerResolver.IsValidEntityId(entityId);
}
