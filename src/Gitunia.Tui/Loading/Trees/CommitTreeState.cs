using Gitunia.Core.FileTree;
using Gitunia.Git;
using Gitunia.Tui.Loading.Commits;

namespace Gitunia.Tui.Loading.Trees;

/// <summary>The file tree of the selected commit and whether it has arrived.</summary>
/// <param name="Commit">The selected commit, null when none is.</param>
/// <param name="Status">Loading, loaded or failed (the same stages as the commit details).</param>
/// <param name="Model">The loaded tree, null unless <paramref name="Status"/> is <see
/// cref="CommitDetailsStatus.Loaded"/>.</param>
/// <param name="Error">Why loading failed, empty otherwise.</param>
public sealed record CommitTreeState(ObjectId? Commit, CommitDetailsStatus Status, FileTreeModel? Model, string Error)
{
    public static CommitTreeState Empty { get; } = new(null, CommitDetailsStatus.None, null, string.Empty);
}
