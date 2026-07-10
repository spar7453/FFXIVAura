namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private bool TryReadStatusSnapshots(IEnumerable<IStatus>? statuses, string scope, uint ownerEntityId)
    {
        if (StatusSnapshotReader.ReadTo(statuses, this.statusSnapshotBuffer, out var error))
            return true;

        this.NoteStatusSnapshotReadFailure(scope, ownerEntityId, error);
        return false;
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
            this.statusSnapshotBuffer.Clear();
            this.NoteStatusSnapshotReadFailure(scope, ownerEntityId, ex);
            return false;
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
            this.statusSnapshotBuffer.Clear();
            this.NoteStatusSnapshotReadFailure(scope, ownerEntityId, ex);
            return false;
        }
    }

    private void NoteStatusSnapshotReadFailure(string scope, uint ownerEntityId, Exception? error)
        => this.SetBugDiagnosticEvent($"statusReadFailed:{scope}:{ownerEntityId}:{error?.GetType().Name ?? "unknown"}");
}
