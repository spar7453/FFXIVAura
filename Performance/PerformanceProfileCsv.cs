using System.Globalization;
using System.Text;

namespace FFXIVAura;

internal static class PerformanceProfileCsv
{
    public const int PrivacySchemaVersion = 2;
    public const string FileName = "performance-profile.csv";
    public const string PreviousFileName = "performance-profile.previous.csv";
    public const string Header =
        "timestampUtc,scope,id,label,detail,currentMs,recentAvgMs,averageMs,maxMs,maxOccurredLocal,lastCallCount,totalCallCount,sampleFrameCount,windowCount,skillIconCount,auraIconCount,cooldownCalculationCount,keybindCount,tooltipRenderCount,grayscaleIconProcessCount,allocatedBytes,averageAllocatedBytes,maxAllocatedBytes,gen0CollectionCount,privacySchemaVersion";

    public static PerformanceProfileCsvRow CreateDiagnosticRow(
        DateTime timestampUtc,
        string id,
        string label,
        string detail,
        int windowCount,
        int skillIconCount,
        int auraIconCount,
        int cooldownCalculationCount,
        int keybindCount,
        int tooltipRenderCount,
        int grayscaleIconProcessCount)
        => new(
            timestampUtc,
            "diagnostic",
            id,
            label,
            detail,
            0,
            0,
            0,
            0,
            DateTime.MinValue,
            0,
            0,
            0,
            windowCount,
            skillIconCount,
            auraIconCount,
            cooldownCalculationCount,
            keybindCount,
            tooltipRenderCount,
            grayscaleIconProcessCount);

    public static void AppendRow(StringBuilder builder, PerformanceProfileCsvRow row)
    {
        builder
            .Append(FormatTimestamp(row.TimestampUtc)).Append(',')
            .Append(Escape(row.Scope)).Append(',')
            .Append(Escape(row.Id)).Append(',')
            .Append(Escape(row.Label)).Append(',')
            .Append(Escape(row.Detail)).Append(',')
            .Append(FormatNumber(row.CurrentMilliseconds)).Append(',')
            .Append(FormatNumber(row.RecentAverageMilliseconds)).Append(',')
            .Append(FormatNumber(row.AverageMilliseconds)).Append(',')
            .Append(FormatNumber(row.MaxMilliseconds)).Append(',')
            .Append(FormatTimestamp(row.MaxOccurredAtUtc == DateTime.MinValue ? DateTime.MinValue : row.MaxOccurredAtUtc.ToLocalTime())).Append(',')
            .Append(row.LastCallCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.TotalCallCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.SampleFrameCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.WindowCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.SkillIconCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.AuraIconCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.CooldownCalculationCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.KeybindCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.TooltipRenderCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.GrayscaleIconProcessCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.AllocatedBytes.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(FormatNumber(row.AverageAllocatedBytes)).Append(',')
            .Append(row.MaxAllocatedBytes.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(row.Gen0CollectionCount.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(PrivacySchemaVersion.ToString(CultureInfo.InvariantCulture))
            .AppendLine();
    }

    internal static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string FormatNumber(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTime value)
        => value == DateTime.MinValue
            ? string.Empty
            : value.ToString("O", CultureInfo.InvariantCulture);
}

internal readonly record struct PerformanceProfileCsvRow(
    DateTime TimestampUtc,
    string Scope,
    string Id,
    string Label,
    string Detail,
    double CurrentMilliseconds,
    double RecentAverageMilliseconds,
    double AverageMilliseconds,
    double MaxMilliseconds,
    DateTime MaxOccurredAtUtc,
    long LastCallCount,
    long TotalCallCount,
    int SampleFrameCount,
    int WindowCount,
    int SkillIconCount,
    int AuraIconCount,
    int CooldownCalculationCount,
    int KeybindCount,
    int TooltipRenderCount,
    int GrayscaleIconProcessCount,
    long AllocatedBytes = 0,
    double AverageAllocatedBytes = 0,
    long MaxAllocatedBytes = 0,
    int Gen0CollectionCount = 0);
