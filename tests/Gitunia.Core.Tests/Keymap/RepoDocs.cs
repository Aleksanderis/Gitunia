using System.Reflection;

namespace Gitunia.Core.Tests.Keymap;

/// <summary>Finds files of the repository (<c>docs/</c>, the keymap data file) for the documentation tests.</summary>
public static class RepoDocs
{
    public static string FindUxDocument(string fileName) => Find("docs", fileName);

    public static string Find(params string[] relativePath)
    {
        var root = typeof(RepoDocs).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepoRoot")?.Value ?? string.Empty;
        var candidate = Path.Combine([root, .. relativePath]);

        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException($"{Path.Combine(relativePath)} not found at {candidate}");
    }
}
