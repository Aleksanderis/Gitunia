using Gitunia.App.Picker;

namespace Gitunia.App.Tests.Fakes;

/// <summary>A file system whose listing a test scripts (to block it, slow it down or make it throw).</summary>
/// <param name="list">Answers the listing of a directory.</param>
public sealed class ScriptedDirectories(Func<string, IReadOnlyList<string>> list) : IDirectoryLister
{
    public IReadOnlyList<string> ListDirectories(string directory) => list(directory);

    public bool IsRepository(string directory) => false;

    public bool DirectoryExists(string directory) => false;
}
