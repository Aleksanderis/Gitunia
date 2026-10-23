using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Git;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Loading.Trees;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Panes;

/// <summary>The runtime side of one tab's diff pane: the loaded diff, what the list shows, the search and the pane layout choices. UI thread only.</summary>
public sealed class DiffSession(DiffStore store, GraphGlyphSet glyphs, FileContentStore? contentStore = null)
    : IDiffViewState, IDisposable
{
    public DiffStore Store { get; } = store;

    /// <summary>Reads the file the Files tab opened; null in sessions built without one (tests).</summary>
    public FileContentStore? ContentStore { get; } = contentStore;

    /// <summary>The path of the file whose content the pane shows instead of a diff (Files tab), null for a
    /// diff.</summary>
    public string? ShownContentPath { get; set; }

    /// <summary>The commit <see cref="ShownContentPath"/> is read from.</summary>
    public ObjectId? ShownContentCommit { get; set; }

    /// <summary>The pane shows the content of a file, not a diff.</summary>
    public bool IsShowingContent => ShownContentPath is not null;

    /// <summary>The list source of the diff pane.</summary>
    public DiffListSource Source { get; } = new(glyphs);

    /// <summary>The diff pane is open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>The pane that fills the screen, null for the normal layout.</summary>
    public PaneKind? ZoomedPane { get; set; }

    public DiffSearchState Search { get; set; } = DiffSearchState.Idle;

    /// <summary>The cursor row, remembered so switching tabs and back keeps the place.</summary>
    public int SelectedRow { get; set; }

    /// <summary>The first visible row, remembered with <see cref="SelectedRow"/>.</summary>
    public int TopRow { get; set; }

    /// <summary>The row the last search step revealed, so the next step continues from it (a side-by-side line holds two rows); null after any other cursor move.</summary>
    public int? SearchRow { get; set; }

    /// <summary>The file to put the cursor on once the diff has loaded, null for none.</summary>
    public string? PendingFilePath { get; set; }

    /// <summary>The folder whose combined changes to show once the diff has loaded (tree style of the Changes tab),
    /// null for none.</summary>
    public string? PendingFolderPath { get; set; }

    /// <summary>The folder the pane is asked to show; kept so the folder view survives a reload with other options.
    /// Null when the pane shows a single file.</summary>
    public string? ShownFolderPath { get; set; }

    /// <summary>The folder <see cref="ShownDocument"/> was cut for, null for a single file or the whole diff.</summary>
    public string? BuiltFolderPath { get; set; }

    /// <summary>The loaded diff the pane last took its file from, null before the first.</summary>
    public DiffDocument? ShownFull { get; set; }

    /// <summary>Index in <see cref="ShownFull"/> of the file the pane shows, -1 for none.</summary>
    public int ShownFile { get; set; } = -1;

    /// <summary>The shown file's document (<see cref="ShownFull"/> cut to it), held by the list source.</summary>
    public DiffDocument ShownDocument { get; set; } = DiffDocument.Empty;

    /// <summary>The path of the file the pane shows, kept to show the same file when the diff reloads.</summary>
    public string? ShownPath { get; set; }

    public void Dispose()
    {
        Store.Dispose();
        ContentStore?.Dispose();
    }
}
