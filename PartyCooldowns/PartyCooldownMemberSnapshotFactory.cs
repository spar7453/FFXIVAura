namespace FFXIVAura;

internal static class PartyCooldownMemberSnapshotFactory
{
    public static PartyCooldownMemberSnapshot Create(
        uint entityId,
        ulong contentId,
        ushort worldId,
        string name,
        uint classJobId)
    {
        var job = JobInfo.Code(classJobId);
        return new PartyCooldownMemberSnapshot(
            CreateKey(contentId, entityId, name, job),
            entityId,
            contentId,
            worldId,
            name,
            CreateShortName(name),
            job,
            JobInfo.IconId(classJobId),
            string.Empty);
    }

    public static string CreateKey(ulong contentId, uint entityId, string name, string job)
    {
        if (contentId != 0)
            return $"content-{contentId}";

        if (entityId != 0)
            return $"entity-{entityId}";

        return $"{job}:{name}";
    }

    public static string CreateShortName(string name)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? "??" : name.Trim();
        return normalized.Length <= 2 ? normalized : normalized[..2];
    }
}
