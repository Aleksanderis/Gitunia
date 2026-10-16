using Gitunia.Core.Diff;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>The runtime side of one tab's working-tree diff: the loaded diff, what the list shows and the search. UI thread only.</summary>
public sealed class WorkingDiffSession(WorkingDiffStore store, GraphGlyphSet glyphs) : IDiffViewState, IDisposable
{
    public WorkingDiffStore Store { get; } = store;

    /// <summary>The list source of the diff pane.</summary>
    public DiffListSource Source { get; } = new(glyphs);

    public DiffSearchState Search { get; set; } = DiffSearchState.Idle;

    /// <summary>The diff fills the whole working-tree view (<c>&lt;space&gt;z</c>); ends when focus leaves the diff.</summary>
    public bool IsZoomed { get; set; }

    /// <summary>The cursor row, remembered so switching tabs and back keeps the place.</summary>
    public int SelectedRow { get; set; }

    /// <summary>The first visible row, remembered with <see cref="SelectedRow"/>.</summary>
    public int TopRow { get; set; }

    /// <summary>The row the last search step revealed, so the next step continues from it; null after any other cursor move.</summary>
    public int? SearchRow { get; set; }

    /// <summary>Changes whenever the status was read again, so the shown file is read again (see <see cref="WorkingDiffStore.Show"/>).</summary>
    public int Generation { get; private set; }

    /// <summary>The status the shown diff was asked for, to tell a new status from an old one.</summary>
    public StatusSnapshot? SeenSnapshot { get; private set; }

    /// <summary>The file the list source shows now, to keep the cursor when the same file loads again.</summary>
    public WorkingDiffRequest? ShownRequest { get; set; }

    /// <summary>Notes that the status was read again, so the next sync reloads the shown file.</summary>
    public void Invalidate() => Generation++;

    /// <summary>Notes the status a sync saw; a different one invalidates the diff.</summary>
    public void Observe(StatusSnapshot? snapshot)
    {
        if (!ReferenceEquals(snapshot, SeenSnapshot))
        {
            SeenSnapshot = snapshot;
            Generation++;
        }
    }

    public void Dispose() => Store.Dispose();
}
