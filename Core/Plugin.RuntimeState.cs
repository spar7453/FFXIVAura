namespace FFXIVAura;

internal enum WorldRuntimeResetReason
{
    Login,
    Logout,
    ZoneChange,
}

public sealed unsafe partial class Plugin
{
    private void ResetWorldRuntimeState(WorldRuntimeResetReason reason)
    {
        this.visibleAbilityKeys.Clear();
        this.transientSkillPositionsByGroup.Clear();
        this.visibleAurasByScope.Clear();
        this.auraFirstSeenByScope.Clear();
        this.cooldownFrameCache.Clear();

        this.playerAuraFrameCache.Clear();
        this.targetAuraFrameCache.Clear();
        this.partyAuraFrameAllCache.Clear();
        this.partyAuraFrameOwnCache.Clear();
        this.partyAuraGroupFrameAllCache.Clear();
        this.partyAuraGroupFrameOwnCache.Clear();
        this.partyMemberAuraFrameBuffer.Clear();
        this.partyMemberAuraOwnFrameBuffer.Clear();
        this.partyMemberAuraGroupFrameBuffer.Clear();
        this.partyMemberAuraGroupOwnFrameBuffer.Clear();
        this.partyAuraRuntimeStore.ResetRuntimeState();
        this.playerAuraFrameCacheValid = false;
        this.targetAuraFrameCacheValid = false;
        this.partyAuraFrameAllCacheValid = false;
        this.partyAuraFrameOwnCacheValid = false;

        this.statusSnapshotBuffer.Clear();
        this.statusSnapshotFallbackCache.Clear();
        this.gameObjectOwnerFrameCache.Clear();

        this.partyCooldownRuntimeStore.Reset();
        this.partyCooldownActiveStatusIndex.Reset();
        this.partyCooldownFrameSnapshot = null;
        this.partyCooldownMemberEntityIdsBuffer.Clear();
        this.partyCooldownOwnedObjectOwnerIdsBuffer.Clear();
        this.partyCooldownOwnedObjectPartyEntityIdsBuffer.Clear();

        this.partyCooldownRosterService.ResetRuntimeState();
        this.partyCooldownLayoutModeTracker.Reset();

        this.overlayWindowDebugSnapshots.Clear();
        this.InvalidateKeybindCache();
        this.SetBugDiagnosticEvent($"worldRuntimeReset:{reason}");
    }
}
