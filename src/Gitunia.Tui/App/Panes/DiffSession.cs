using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Panes;

/// <summary>The runtime side of one tab's diff pane: the loaded diff, what the list shows, the search and the pane layout choices. UI thread only.</summary>
public sealed class DiffSession(DiffStore store, GraphGlyphSet glyphs) : IDiffViewState, IDisposable
{
    public DiffStore Store { get; } = store;

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

    /// <summary>The loaded diff the pane last took its file from (the pane shows one file of it), null before the first.</summary>
    public DiffDocument? ShownFull { get; set; }

    /// <summary>Index in <see cref="ShownFull"/> of the file the pane shows, -1 for none.</summary>
    public int ShownFile { get; set; } = -1;

    /// <summary>The document of the shown file (<see cref="ShownFull"/> cut to that file), what the list source holds.</summary>
    public DiffDocument ShownDocument { get; set; } = DiffDocument.Empty;

    /// <summary>The path of the file the pane shows, kept to show the same file when the diff reloads.</summary>
    public string? ShownPath { get; set; }

    public void Dispose() => Store.Dispose();
}
