namespace FFXIVAura;

internal static class StatusSnapshotReader
{
    public static bool ReadTo(IEnumerable<IStatus>? statuses, List<StatusSnapshot> output, out Exception? error)
    {
        output.Clear();
        error = null;
        if (statuses is null)
            return true;

        try
        {
            foreach (var status in statuses)
            {
                if (status is null)
                    continue;

                var snapshot = new StatusSnapshot(
                    status.StatusId,
                    status.SourceId,
                    status.Param,
                    status.RemainingTime);
                if (snapshot.StatusId > 0)
                    output.Add(snapshot);
            }

            return true;
        }
        catch (Exception ex)
        {
            output.Clear();
            error = ex;
            return false;
        }
    }
}
