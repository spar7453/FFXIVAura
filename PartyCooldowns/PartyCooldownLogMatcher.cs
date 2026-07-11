namespace FFXIVAura;

internal static class PartyCooldownLogMatcher
{
    public static string NormalizeActorName(string name)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        var worldSeparator = normalized.IndexOf('@');
        return worldSeparator > 0 ? normalized[..worldSeparator].TrimEnd() : normalized;
    }

    public static string NormalizeActionName(string name)
        => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

    public static bool IsSameActor(string sourceName, ushort sourceWorldId, string memberName, ushort memberWorldId)
    {
        var normalizedSourceName = NormalizeActorName(sourceName);
        var normalizedMemberName = NormalizeActorName(memberName);
        if (normalizedSourceName.Length == 0 || normalizedMemberName.Length == 0)
            return false;

        if (!string.Equals(normalizedSourceName, normalizedMemberName, StringComparison.Ordinal))
            return false;

        return sourceWorldId == 0 || memberWorldId == 0 || sourceWorldId == memberWorldId;
    }

    public static bool IsCompletedActionUseTemplate(string templateText)
        => templateText.Contains("시전했습니다", StringComparison.Ordinal)
           || templateText.Contains("사용했습니다", StringComparison.Ordinal)
           || templateText.Contains("uses", StringComparison.OrdinalIgnoreCase)
           || templateText.Contains("used", StringComparison.OrdinalIgnoreCase);

    public static bool IsAmbiguousActorFallback(ushort sourceWorldId, int matchCount)
        => sourceWorldId == 0 && matchCount > 1;

    public static bool IsDuplicateUse(DateTime lastTrackedAtUtc, DateTime nowUtc, TimeSpan dedupeWindow)
        => lastTrackedAtUtc != DateTime.MinValue
           && nowUtc >= lastTrackedAtUtc
           && nowUtc - lastTrackedAtUtc < dedupeWindow;
}
