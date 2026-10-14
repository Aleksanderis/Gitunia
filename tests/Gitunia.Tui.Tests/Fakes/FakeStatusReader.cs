using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Answers status reads from memory, per repository root, so tests need no git repository.</summary>
public sealed class FakeStatusReader : IStatusReader
{
    private static readonly HeadState MainBranch = new(HeadKind.Branch, "main", null);

    private readonly Dictionary<string, bool> _dirtyByRoot = [];

    /// <summary>A location under the temp directory that is never touched on disk.</summary>
    public static RepositoryLocation LocationOf(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-fake-repositories", name);
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    /// <summary>Makes the next reads of this repository report a dirty or a clean working tree.</summary>
    public void SetDirty(RepositoryLocation location, bool isDirty) => _dirtyByRoot[location.RootPath] = isDirty;

    public Task<StatusSnapshot> ReadStatusAsync(RepositoryLocation location, bool includeIgnored, UntrackedFilesMode untrackedFiles, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = new List<StatusEntry>();
        if (_dirtyByRoot.GetValueOrDefault(location.RootPath))
        {
            entries.Add(new StatusEntry(StatusEntryKind.Ordinary, "a.txt", null, FileChangeKind.Unmodified, FileChangeKind.Modified, null, ConflictKind.None, null));
        }

        return Task.FromResult(new StatusSnapshot(new StatusBranch(MainBranch, null, null, null), entries, []));
    }
}
