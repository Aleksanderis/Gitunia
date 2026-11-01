using Gitunia.App.Loading.Commits;
using Gitunia.Core.Diff;
using Gitunia.Git;

namespace Gitunia.App.Loading.Trees;

/// <summary>The content of the file the Files tab opened and whether it has arrived.</summary>
/// <param name="Commit">The commit the file belongs to, null when no file is open.</param>
/// <param name="Path">The path of the file, null when no file is open.</param>
/// <param name="Status">Loading, loaded or failed.</param>
/// <param name="Document">What the diff pane shows: the lines, or one note row (loading, failed, binary, too large,
/// empty).</param>
public sealed record FileContentState(ObjectId? Commit, string? Path, CommitDetailsStatus Status, DiffDocument Document)
{
    public static FileContentState Empty { get; } = new(null, null, CommitDetailsStatus.None, DiffDocument.Empty);
}
