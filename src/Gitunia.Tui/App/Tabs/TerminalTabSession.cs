using Gitunia.App.Tabs;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Tabs;

/// <summary>
/// One tab's session with its terminal side added to the runtime half: the list sources drawing the graph and the
/// detail pane, and the diff views. The runtime half (rows, load state, stores) is <see cref="TabSession"/> in
/// <c>Gitunia.App</c>. Touched from the UI thread only.
/// </summary>
public sealed class TerminalTabSession : TabSession
{
    public TerminalTabSession(TabSessionParts parts, GraphGlyphSet glyphs, IGroupButtonSource? workingButtons = null)
        : base(parts)
    {
        Diff = parts.DiffStore is null ? null : new DiffSession(parts.DiffStore, glyphs, parts.ContentStore);
        WorkingDiff = parts.WorkingDiffStore is null ? null : new WorkingDiffSession(parts.WorkingDiffStore, glyphs, workingButtons);
        DetailSource = new DetailListSource(glyphs);
        Source = new GraphListSource(Graph, glyphs);
    }

    public GraphListSource Source { get; }

    /// <summary>The list source of the detail pane.</summary>
    public DetailListSource DetailSource { get; }

    /// <summary>The tab's diff pane; null in sessions built without a diff store (tests).</summary>
    public DiffSession? Diff { get; }

    /// <summary>The tab's Changes view diff; null in sessions built without a working diff store (tests).</summary>
    public WorkingDiffSession? WorkingDiff { get; }

    protected override void OnGraphRowsChanged()
    {
        Source.ShowsCutMarker = ShowsCutMarker;
        Source.NotifyAppended();
    }

    protected override void OnStatusRead() => WorkingDiff?.Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Diff?.Dispose();
            WorkingDiff?.Dispose();
        }

        base.Dispose(disposing);
    }
}
