namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private bool TryReadStatusSnapshots(IEnumerable<IStatus>? statuses, string scope, uint ownerEntityId)
    {
        if (StatusSnapshotReader.ReadTo(statuses, this.statusSnapshotBuffer, out var error))
        {
            this.statusSnapshotFallbackCache.Remember(scope, ownerEntityId, this.statusSnapshotBuffer, DateTime.UtcNow);
            return true;
        }

        return this.TryUseStatusSnapshotFallback(scope, ownerEntityId, error);
    }

    private bool TryReadBattleCharaStatusSnapshots(IBattleChara character, string scope, out uint ownerEntityId)
    {
        ownerEntityId = 0;
        try
        {
            ownerEntityId = character.EntityId;
            return ownerEntityId != 0
                   && this.TryReadStatusSnapshots(character.StatusList, scope, ownerEntityId);
        }
        catch (Exception ex)
        {
            return this.TryUseStatusSnapshotFallback(scope, ownerEntityId, ex);
        }
    }

    private bool TryReadPartyMemberStatusSnapshots(IPartyMember member, string scope, out uint ownerEntityId)
    {
        ownerEntityId = 0;
        try
        {
            ownerEntityId = member.EntityId;
            return ownerEntityId != 0
                   && this.TryReadStatusSnapshots(member.Statuses, scope, ownerEntityId);
        }
        catch (Exception ex)
        {
            return this.TryUseStatusSnapshotFallback(scope, ownerEntityId, ex);
        }
    }

    private bool TryUseStatusSnapshotFallback(string scope, uint ownerEntityId, Exception? error)
    {
        if (this.statusSnapshotFallbackCache.TryCopyRecentTo(
                scope,
                ownerEntityId,
                DateTime.UtcNow,
                StatusSnapshotFailureRetention,
                this.statusSnapshotBuffer))
        {
            this.SetBugDiagnosticEvent($"statusReadFallback:{scope}:{ownerEntityId}:{error?.GetType().Name ?? "unknown"}");
            return true;
        }

        this.statusSnapshotBuffer.Clear();
        this.NoteStatusSnapshotReadFailure(scope, ownerEntityId, error);
        return false;
    }

    private uint GetGameObjectOwnerEntityId(uint entityId)
    {
        if (entityId is 0 or 0xE0000000)
            return 0;

        if (this.gameObjectOwnerFrameCache.TryGetValue(entityId, out var cached))
            return cached;

        var ownerId = 0u;
        try
        {
            ownerId = ObjectTable.SearchByEntityId(entityId)?.OwnerId ?? 0;
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"gameObjectOwnerReadFailed:{entityId}:{ex.GetType().Name}");
        }

        this.gameObjectOwnerFrameCache[entityId] = ownerId;
        return ownerId;
    }

    private void NoteStatusSnapshotReadFailure(string scope, uint ownerEntityId, Exception? error)
        => this.SetBugDiagnosticEvent($"statusReadFailed:{scope}:{ownerEntityId}:{error?.GetType().Name ?? "unknown"}");
}
