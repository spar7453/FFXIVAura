using System.Reflection;
using System.Text;

namespace FFXIVAura;

internal enum PluginDataSource
{
    ExternalFile,
    EmbeddedResource,
    EmbeddedFallback,
}

internal readonly record struct PluginDataReadResult<T>(
    T Value,
    PluginDataSource Source,
    Exception? ExternalFailure);

internal static class PluginDataFiles
{
    public const string AbilitiesResourceName = "FFXIVAura.Data.abilities.json";
    public const string PartyCooldownsResourceName = "FFXIVAura.Data.party_cooldowns.json";

    public static string ReadText(string assemblyDirectory, string relativePath, string resourceName)
    {
        var path = Path.Combine(assemblyDirectory, relativePath);
        if (File.Exists(path))
            return File.ReadAllText(path, Encoding.UTF8);

        return ReadEmbeddedText(relativePath, resourceName, path);
    }

    public static PluginDataReadResult<T> ReadJson<T>(
        string assemblyDirectory,
        string relativePath,
        string resourceName,
        JsonSerializerOptions options,
        Func<T, bool> isValid)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(isValid);

        var path = Path.Combine(assemblyDirectory, relativePath);
        Exception? externalFailure = null;
        if (File.Exists(path))
        {
            try
            {
                var externalValue = DeserializeAndValidate<T>(
                    File.ReadAllText(path, Encoding.UTF8),
                    path,
                    options,
                    isValid);
                return new PluginDataReadResult<T>(
                    externalValue,
                    PluginDataSource.ExternalFile,
                    null);
            }
            catch (Exception ex)
            {
                externalFailure = ex;
            }
        }

        try
        {
            var embeddedValue = DeserializeAndValidate<T>(
                ReadEmbeddedText(relativePath, resourceName, path),
                resourceName,
                options,
                isValid);
            return new PluginDataReadResult<T>(
                embeddedValue,
                externalFailure is null
                    ? PluginDataSource.EmbeddedResource
                    : PluginDataSource.EmbeddedFallback,
                externalFailure);
        }
        catch (Exception embeddedFailure) when (externalFailure is not null)
        {
            throw new AggregateException(
                $"Both the external and embedded plugin data for '{relativePath}' are invalid.",
                externalFailure,
                embeddedFailure);
        }
    }

    private static T DeserializeAndValidate<T>(
        string json,
        string source,
        JsonSerializerOptions options,
        Func<T, bool> isValid)
        where T : class
    {
        var value = JsonSerializer.Deserialize<T>(json, options)
            ?? throw new InvalidDataException($"Plugin data '{source}' deserialized to null.");
        if (!isValid(value))
            throw new InvalidDataException($"Plugin data '{source}' failed schema validation.");

        return value;
    }

    private static string ReadEmbeddedText(
        string relativePath,
        string resourceName,
        string externalPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new FileNotFoundException(
                $"Could not find plugin data file '{relativePath}' or embedded resource '{resourceName}'.",
                externalPath);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
