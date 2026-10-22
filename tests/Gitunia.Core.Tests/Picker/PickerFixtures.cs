using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Tests.Picker;

/// <summary>A fake file system and workspace for picker tests: paths under one root, no disk access.</summary>
public static class PickerFixtures
{
    // Under the drive root: a Windows temp path ("...\AppData\Local\Temp") would match short queries.
    public static string Home { get; } = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "picker-tests", "home");

    public static string PathOf(params string[] segments) => Path.Combine([Home, .. segments]);

    public static RepositoryLocation Location(params string[] segments)
    {
        var root = PathOf(segments);
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    public static Workspace Open(Workspace workspace, params string[] names)
    {
        foreach (var name in names)
        {
            workspace = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(Location("code", name))).Workspace;
        }

        return workspace;
    }

    /// <param name="workspace">The workspace; empty when null.</param>
    /// <param name="discovered">Root paths the scanner found.</param>
    /// <param name="directories">Maps a directory to the names of its subdirectories.</param>
    /// <param name="repositories">Directories that count as repositories.</param>
    public static PickerContext Context(
        Workspace? workspace = null,
        IEnumerable<string>? discovered = null,
        IReadOnlyDictionary<string, string[]>? directories = null,
        IEnumerable<string>? repositories = null)
    {
        var map = directories ?? new Dictionary<string, string[]>();
        var repositorySet = (repositories ?? []).ToHashSet();
        return new PickerContext(
            workspace ?? Workspace.Empty,
            [.. discovered ?? []],
            Home,
            PathOf("code", "current"),
            directory => map.TryGetValue(Path.TrimEndingDirectorySeparator(directory), out var names) ? [.. names.Select(name => Path.Combine(Path.TrimEndingDirectorySeparator(directory), name))] : [],
            repositorySet.Contains);
    }
}
