using System.Text.Json;
using System.Xml.Linq;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class BuildMetadataTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("build metadata versions stay synchronized", VersionsStaySynchronized),
    ];

    private static void VersionsStaySynchronized()
    {
        var project = XDocument.Load(TestFiles.FindRepoFile("FFXIVAura.csproj"));
        var version = project.Descendants("Version").Single().Value;
        var assemblyVersion = project.Descendants("AssemblyVersion").Single().Value;
        var fileVersion = project.Descendants("FileVersion").Single().Value;

        using var manifest = JsonDocument.Parse(File.ReadAllText(TestFiles.FindRepoFile("FFXIVAura.json")));
        var manifestVersion = manifest.RootElement.GetProperty("AssemblyVersion").GetString();

        Equal(version, assemblyVersion);
        Equal(version, fileVersion);
        Equal(version, manifestVersion);
    }
}
