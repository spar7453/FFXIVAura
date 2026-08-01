using System.Text.RegularExpressions;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class ArchitectureBoundaryTests
{
    private static readonly string[] FfxivClientStructsAdapters =
    [
        "Abilities/GameActionRuntime.cs",
        "Abilities/HotbarKeybindPolicy.cs",
        "Core/ActionKeybindService.cs",
        "GameIntegration/DalamudPartyRosterReader.cs",
    ];

    private static readonly string[] LuminaSheetAdapters =
    [
        "Abilities/GameActionRepository.cs",
        "Auras/AuraCatalog.cs",
        "Core/ActionKeybindService.cs",
        "Tooltips/TooltipContentService.cs",
    ];

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("architecture keeps PluginService injection in the composition root", KeepsPluginServicesInCompositionRoot),
        ("architecture keeps FFXIVClientStructs in runtime adapters", KeepsFfxivClientStructsInRuntimeAdapters),
        ("architecture keeps Lumina sheet types in data adapters", KeepsLuminaSheetsInDataAdapters),
        ("architecture keeps Plugin partials out of unsafe context", KeepsPluginPartialsOutOfUnsafeContext),
        ("architecture keeps static plugin logging in the composition root", KeepsStaticPluginLoggingInCompositionRoot),
    ];

    private static void KeepsPluginServicesInCompositionRoot()
    {
        var files = FindProductionFilesContaining("[PluginService]");

        Sequence(["Plugin.cs"], files);
    }

    private static void KeepsFfxivClientStructsInRuntimeAdapters()
    {
        var files = FindProductionFilesContaining("FFXIVClientStructs");

        Sequence(FfxivClientStructsAdapters, files);
    }

    private static void KeepsLuminaSheetsInDataAdapters()
    {
        var files = FindProductionFilesContaining("Lumina.Excel.Sheets");

        Sequence(LuminaSheetAdapters, files);
    }

    private static void KeepsPluginPartialsOutOfUnsafeContext()
    {
        var files = FindProductionFilesContaining("unsafe partial class Plugin");

        Equal(0, files.Count);
    }

    private static void KeepsStaticPluginLoggingInCompositionRoot()
    {
        var files = FindProductionFilesMatching(@"(?<![\w.])Log\.");

        Equal(0, files.Count);
    }

    private static IReadOnlyList<string> FindProductionFilesContaining(string value)
    {
        var root = GetRepositoryRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.StartsWith("obj/", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(Path.Combine(root, path)).Contains(value, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> FindProductionFilesMatching(string pattern)
    {
        var root = GetRepositoryRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.StartsWith("obj/", StringComparison.OrdinalIgnoreCase))
            .Where(path => Regex.IsMatch(File.ReadAllText(Path.Combine(root, path)), pattern))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetRepositoryRoot()
        => Path.GetDirectoryName(TestFiles.FindRepoFile("FFXIVAura.csproj"))!;
}
