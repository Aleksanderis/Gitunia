using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>List source over one file list (Unstaged or Staged) of the working-tree view of the active tab; it can show a Visual range of files.</summary>
public sealed class WorkingTreeListSource(WorkingTreeSection section, GraphGlyphSet glyphs) : IListDataSource, IRangeSource
{
    private readonly JumpLabelCache _jump = new();
    private WorkingTreeState? _tree;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>The working tree the rows come from, null while the status is not loaded.</summary>
    public WorkingTreeState? Tree => _tree;

    /// <summary>The list this source shows.</summary>
    public WorkingTreeSection Section => section;

    /// <summary>Rows of the list, 0 while the status is not loaded.</summary>
    public int Count => WorkingTreePaneRenderer.Count(_tree, section);

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    /// <summary>The text of an open quick jump, whose targets in view get labels; null when none is open.</summary>
    public string? JumpText
    {
        get => _jump.Text;
        set => _jump.Text = value;
    }

    public RowRange? Range { get; set; }

    public event Action? RangeInvalidated;

    /// <summary>Shows the rows of another working-tree state. Returns true when the rows are new.</summary>
    /// <param name="tree">The active tab's working tree, null when it has none yet.</param>
    public bool Update(WorkingTreeState? tree)
    {
        if (ReferenceEquals(_tree?.Snapshot, tree?.Snapshot))
        {
            return false;
        }

        _tree = tree;
        _jump.Invalidate();
        RangeInvalidated?.Invoke();
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        return true;
    }

    /// <summary>The quick-jump targets of <paramref name="text"/> among the files in view of <paramref name="listView"/>, nearest the cursor first.</summary>
    /// <param name="listView">The list this source is shown in.</param>
    /// <param name="text">The typed text.</param>
    public IReadOnlyList<JumpLabel> JumpLabels(ListView listView, string text)
    {
        var width = PanePadding.TextWidth(listView.Viewport.Width, ScreenPadding.Columns(listView));
        var window = new JumpWindow(listView.Viewport.Y, listView.Viewport.Height, listView.SelectedItem ?? 0);

        return _jump.Labels(text, window, width, () => new WorkingTreeJumpRows(_tree, section, width, glyphs.Ellipsis));
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        var padding = ScreenPadding.Columns(listView);
        var textWidth = PanePadding.TextWidth(width, padding);
        var line = WorkingTreePaneRenderer.RenderRow(_tree, section, item, textWidth, selected || Range?.Contains(item) == true, glyphs, JumpLabelOf(listView, item));
        LinePainter.Paint(listView, PanePadding.Wrap(line, padding), col, row, listView.HasFocus);
    }

    public void Dispose()
    {
    }

    // The labels are worked out once per frame (the first row asks), not once per row.
    private JumpLabel? JumpLabelOf(ListView listView, int item)
    {
        if (string.IsNullOrEmpty(JumpText) || item < 0 || item >= Count)
        {
            return null;
        }

        JumpLabels(listView, JumpText);

        return _jump.LabelOf(item);
    }
}
