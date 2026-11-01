using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Core.DiffPane;

/// <summary>Work on the diff and file content stores of one tab, which read git off the UI thread and tell the
/// terminal layer when they changed.</summary>
public abstract record DiffPaneEffect : AppEffect
{
    private protected DiffPaneEffect(TabId tabId) => TabId = tabId;

    /// <summary>The tab whose stores to use.</summary>
    public TabId TabId { get; }
}

/// <summary>Load the diff of a commit, unless it is the one shown already.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">The diff to load.</param>
/// <param name="Prefetch">Read the diffs of the commits likely to be selected next once this one is shown.</param>
public sealed record LoadDiffEffect(TabId TabId, DiffTarget Target, bool Prefetch) : DiffPaneEffect(TabId);

/// <summary>Read the shown commit again with other diff options.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Options">The options.</param>
public sealed record SetDiffOptionsEffect(TabId TabId, DiffOptions Options) : DiffPaneEffect(TabId);

/// <summary>Read the shown commit again without the row and per-file limits.</summary>
/// <param name="TabId">The tab.</param>
public sealed record LoadWholeDiffEffect(TabId TabId) : DiffPaneEffect(TabId);

/// <summary>Forget the loaded diff and the file content (the pane was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record ClearDiffEffect(TabId TabId) : DiffPaneEffect(TabId);

/// <summary>Read the content of a file of a commit; the old content stays until the new one arrives.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Row">The file row.</param>
/// <param name="Commit">The commit to read from.</param>
/// <param name="Delay">How long the cursor must rest before the blob is read.</param>
public sealed record LoadFileContentEffect(TabId TabId, FileTreeRow Row, ObjectId Commit, TimeSpan Delay)
    : DiffPaneEffect(TabId);

/// <summary>Forget the file content.</summary>
/// <param name="TabId">The tab.</param>
public sealed record ClearFileContentEffect(TabId TabId) : DiffPaneEffect(TabId);
