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
        this.partyAuraTimerStates.Clear();
        this.partyAuraTimerLiveKeys.Clear();
        this.partyAuraTimerMemberOwnerIds.Clear();
        this.partyAuraTimerLiveOwnerIds.Clear();
        this.partyAuraTimerPruneBuffer.Clear();
        this.playerAuraFrameCacheValid = false;
        this.targetAuraFrameCacheValid = false;
        this.partyAuraFrameAllCacheValid = false;
        this.partyAuraFrameOwnCacheValid = false;

        this.statusSnapshotBuffer.Clear();
        this.statusSnapshotFallbackCache.Clear();
        this.gameObjectOwnerFrameCache.Clear();

        this.partyCooldownRuntimeStates.Clear();
        this.partyCooldownRuntimePruneBuffer.Clear();
        this.partyCooldownActiveStatusFrameCache.Clear();
        this.partyCooldownActiveStatusRosterHash = 0;
        this.partyCooldownActiveStatusIndexBuiltAtUtc = DateTime.MinValue;
        this.partyCooldownActiveStatusAbsenceConfirmed = false;
        this.partyCooldownFrameSnapshot = null;
        this.partyCooldownLiveRuntimeKeysFrameCache.Clear();
        this.partyCooldownLiveRuntimeKeysFrameCacheValid = false;
        this.partyCooldownMemberEntityIdsBuffer.Clear();
        this.partyCooldownLiveStatusOwnerIdsBuffer.Clear();
        this.partyCooldownOwnedObjectOwnerIdsBuffer.Clear();
        this.partyCooldownOwnedObjectPartyEntityIdsBuffer.Clear();
        this.partyCooldownRowBuffersByWindow.Clear();

        this.partyCooldownHudAllianceGroupRetention.Reset();
        this.partyCooldownLastCompleteHudAllianceOrder = [];
        this.partyCooldownHudAllianceOrderExpiresAtUtc = DateTime.MinValue;
        this.partyCooldownRosterCache = null;
        this.partyCooldownRosterCacheBuiltAtUtc = DateTime.MinValue;
        this.partyCooldownLayoutModeTracker.Reset();

        this.overlayWindowDebugSnapshots.Clear();
        this.InvalidateKeybindCache();
        this.SetBugDiagnosticEvent($"worldRuntimeReset:{reason}");
    }
}
