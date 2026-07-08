namespace FFXIVAura.Tests;

internal static class TestFiles
{
    public static string FindRepoFile(params string[] pathParts)
    {
        var relativePath = Path.Combine(pathParts);
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {Directory.GetCurrentDirectory()}.");
    }
}
