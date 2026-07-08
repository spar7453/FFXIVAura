using System.Reflection;
using System.Text;

namespace FFXIVAura;

internal static class PluginDataFiles
{
    public const string AbilitiesResourceName = "FFXIVAura.Data.abilities.json";
    public const string PartyCooldownsResourceName = "FFXIVAura.Data.party_cooldowns.json";

    public static string ReadText(string assemblyDirectory, string relativePath, string resourceName)
    {
        var path = Path.Combine(assemblyDirectory, relativePath);
        if (File.Exists(path))
            return File.ReadAllText(path, Encoding.UTF8);

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new FileNotFoundException($"Could not find plugin data file '{relativePath}' or embedded resource '{resourceName}'.", path);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
