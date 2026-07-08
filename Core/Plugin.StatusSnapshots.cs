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

    private void NoteStatusSnapshotReadFailure(string scope, uint ownerEntityId, Exception? error)
        => this.SetBugDiagnosticEvent($"statusReadFailed:{scope}:{ownerEntityId}:{error?.GetType().Name ?? "unknown"}");
}
