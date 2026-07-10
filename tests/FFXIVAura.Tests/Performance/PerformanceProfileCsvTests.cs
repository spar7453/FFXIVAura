using System.Text;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PerformanceProfileCsvTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PerformanceProfileCsv escapes labels", EscapesLabels),
        ("PerformanceProfileCsv formats rows invariantly", FormatsRowsInvariantly),
    ];

    private static void EscapesLabels()
    {
        Equal("plain", PerformanceProfileCsv.Escape("plain"));
        Equal("\"Main, \"\"A\"\"\"", PerformanceProfileCsv.Escape("Main, \"A\""));
        Equal("\"line1\nline2\"", PerformanceProfileCsv.Escape("line1\nline2"));
    }

    private static void FormatsRowsInvariantly()
    {
        var builder = new StringBuilder();
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        PerformanceProfileCsv.AppendRow(builder, new PerformanceProfileCsvRow(
            timestamp,
            "frame",
            "plugin",
            "Plugin Frame",
            string.Empty,
            1.25,
            0.5,
            1.125,
            2.75,
            DateTime.MinValue,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14.5,
            15,
            16));

        var columns = builder.ToString().TrimEnd().Split(',');
        Equal(24, columns.Length);
        Equal("2026-01-02T03:04:05.0000000Z", columns[0]);
        Equal("frame", columns[1]);
        Equal("plugin", columns[2]);
        Equal("Plugin Frame", columns[3]);
        Equal(string.Empty, columns[4]);
        Equal("1.25", columns[5]);
        Equal("0.5", columns[6]);
        Equal("1.125", columns[7]);
        Equal("2.75", columns[8]);
        Equal(string.Empty, columns[9]);
        Equal("12", columns[19]);
        Equal("13", columns[20]);
        Equal("14.5", columns[21]);
        Equal("15", columns[22]);
        Equal("16", columns[23]);
    }
}
