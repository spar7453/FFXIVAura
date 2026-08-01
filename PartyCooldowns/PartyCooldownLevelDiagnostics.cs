namespace FFXIVAura;

internal readonly record struct PartyCooldownLevelDiagnostics(
    uint ReportedMinimum,
    uint ReportedMaximum,
    int MissingCount,
    uint ResolvedMinimum,
    uint ResolvedMaximum,
    int ReportedAboveFallbackCount);

internal static class PartyCooldownLevelDiagnosticCalculator
{
    public static PartyCooldownLevelDiagnostics Create(
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        uint missingLevelFallback)
    {
        var reportedMinimum = uint.MaxValue;
        var reportedMaximum = 0u;
        var missingCount = 0;
        var resolvedMinimum = uint.MaxValue;
        var resolvedMaximum = 0u;
        var reportedAboveFallbackCount = 0;

        foreach (var member in members)
        {
            if (member.Level == 0)
            {
                missingCount++;
            }
            else
            {
                reportedMinimum = Math.Min(reportedMinimum, member.Level);
                reportedMaximum = Math.Max(reportedMaximum, member.Level);
                if (missingLevelFallback > 0 && member.Level > missingLevelFallback)
                    reportedAboveFallbackCount++;
            }

            var resolvedLevel = member.ResolveEffectiveLevel(missingLevelFallback);
            if (resolvedLevel == 0)
                continue;

            resolvedMinimum = Math.Min(resolvedMinimum, resolvedLevel);
            resolvedMaximum = Math.Max(resolvedMaximum, resolvedLevel);
        }

        return new PartyCooldownLevelDiagnostics(
            reportedMinimum == uint.MaxValue ? 0 : reportedMinimum,
            reportedMaximum,
            missingCount,
            resolvedMinimum == uint.MaxValue ? 0 : resolvedMinimum,
            resolvedMaximum,
            reportedAboveFallbackCount);
    }
}
