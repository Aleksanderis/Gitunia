namespace Gitunia.App.Picker;

/// <summary>The file system questions of the repository picker, so a test can answer them without a disk.</summary>
public interface IDirectoryLister
{
    /// <summary>The full paths of the subdirectories of a directory; empty when it cannot be read.</summary>
    IReadOnlyList<string> ListDirectories(string directory);

    /// <summary>Whether the directory has a <c>.git</c> entry.</summary>
    bool IsRepository(string directory);

    /// <summary>Whether the directory exists.</summary>
    bool DirectoryExists(string directory);
}
