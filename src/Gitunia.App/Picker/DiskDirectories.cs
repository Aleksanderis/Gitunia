namespace Gitunia.App.Picker;

/// <summary>The real file system.</summary>
public sealed class DiskDirectories : IDirectoryLister
{
    public static DiskDirectories Instance { get; } = new();

    public IReadOnlyList<string> ListDirectories(string directory)
    {
        try
        {
            return [.. Directory.EnumerateDirectories(directory)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public bool IsRepository(string directory)
    {
        var marker = Path.Combine(directory, ".git");

        return Directory.Exists(marker) || File.Exists(marker);
    }

    public bool DirectoryExists(string directory) => Directory.Exists(directory);
}
