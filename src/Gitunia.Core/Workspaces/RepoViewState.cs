using Gitunia.Git;

namespace Gitunia.Core.Workspaces;

/// <summary>What a tab remembers about how the user looks at its repository.</summary>
/// <param name="SelectedCommit">The selected commit, tracked by id so it survives refreshes; null selects the newest.</param>
/// <param name="ScrollOffset">Index of the first visible graph row.</param>
/// <param name="FilterText">The active graph filter, empty when none.</param>
/// <param name="FocusedPane">The pane that has focus.</param>
public sealed record RepoViewState(
    ObjectId? SelectedCommit,
    int ScrollOffset,
    string FilterText,
    PaneKind FocusedPane)
{
    /// <summary>A fresh view: nothing selected, top of the graph, no filter, graph focused.</summary>
    public static RepoViewState Initial { get; } = new(null, 0, string.Empty, PaneKind.Graph);
}
