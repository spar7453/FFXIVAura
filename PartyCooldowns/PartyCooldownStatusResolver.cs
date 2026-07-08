namespace FFXIVAura;

internal static class PartyCooldownStatusResolver
{
    public static uint[] Resolve(IEnumerable<uint> explicitStatusIds, uint statusGainSelfId)
    {
        var statusIds = new HashSet<uint>();
        foreach (var statusId in explicitStatusIds)
        {
            if (statusId > 0)
                statusIds.Add(statusId);
        }

        if (statusGainSelfId > 0)
            statusIds.Add(statusGainSelfId);

        return statusIds.Order().ToArray();
    }
}
