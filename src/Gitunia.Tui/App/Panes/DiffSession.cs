using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Trees;
using Gitunia.Core.Diff;
using Gitunia.Core.DiffPane;
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

    /// <summary>The list source of the diff pane.</summary>
    public DiffListSource Source { get; } = new(glyphs);

    /// <summary>The pane state the list was last brought in line with (the state itself is in the tab's view state).</summary>
    public DiffPaneState Applied { get; set; } = DiffPaneState.Empty;

    public DiffSearchState Search { get; set; } = DiffSearchState.Idle;

    /// <summary>The cursor row, remembered so switching tabs and back keeps the place.</summary>
    public int SelectedRow { get; set; }

    /// <summary>The first visible row, remembered with <see cref="SelectedRow"/>.</summary>
    public int TopRow { get; set; }

    /// <summary>The row the last search step revealed, so the next step continues from it (a side-by-side line holds two rows); null after any other cursor move.</summary>
    public int? SearchRow { get; set; }

    /// <summary>The loaded diff the pane last took its file from, null before the first.</summary>
    public DiffDocument? ShownFull { get; set; }

    /// <summary>Index in <see cref="ShownFull"/> of the file the pane shows, -1 for none.</summary>
    public int ShownFile { get; set; } = -1;

    /// <summary>The shown file's document (<see cref="ShownFull"/> cut to it), held by the list source.</summary>
    public DiffDocument ShownDocument { get; set; } = DiffDocument.Empty;

    /// <summary>The folder <see cref="ShownDocument"/> was cut for, null for a single file or the whole diff.</summary>
    public string? BuiltFolderPath { get; set; }

    public void Dispose()
    {
        Store.Dispose();
        ContentStore?.Dispose();
    }
}
