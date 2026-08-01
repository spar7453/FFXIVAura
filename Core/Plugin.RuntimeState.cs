namespace FFXIVAura;

internal enum WorldRuntimeResetReason
{
    Login,
    Logout,
    ZoneChange,
}

public sealed partial class Plugin
{
    private void ResetWorldRuntimeState(WorldRuntimeResetReason reason)
    {
        this.visibleAbilityKeys.Clear();
        this.transientSkillPositionsByGroup.Clear();
        this.overlayFrameBuffersByWindow.Clear();
        this.auraSearchService.ResetWorldRuntimeState();
        this.cooldownFrameService.BeginFrame();

        this.auraFrameService.ResetRuntimeState();
        this.statusSnapshotRuntime.ResetRuntimeState();

        this.partyCooldownRuntimeStore.Reset();
        this.partyCooldownActiveStatusIndex.Reset();
        this.partyCooldownFrameSnapshot = null;
        this.partyCooldownMemberEntityIdsBuffer.Clear();
        this.partyCooldownOwnedObjectMatcher.ResetRuntimeState();

        this.partyCooldownRosterService.ResetRuntimeState();
        this.partyCooldownLayoutModeTracker.Reset();
        if (reason is WorldRuntimeResetReason.Login or WorldRuntimeResetReason.Logout)
            this.performanceProfileIdentityAnonymizer.Reset();

        this.overlayWindowDebugSnapshots.Clear();
        this.InvalidateKeybindCache();
        this.SetBugDiagnosticEvent($"worldRuntimeReset:{reason}");
    }
}
