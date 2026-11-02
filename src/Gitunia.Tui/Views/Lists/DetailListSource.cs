using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Lists;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>List source over the rows of the detail pane; the whole content is replaced when another commit is selected or its details arrive.</summary>
public sealed class DetailListSource(GraphGlyphSet glyphs) : IListDataSource, IRangeSource, IJumpSource
{
    private readonly JumpLabelCache _jump = new();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public DetailContent Content { get; private set; } = DetailContent.Empty;

    public int Count => Content.Rows.Count;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    public RowRange? Range { get; set; }

    public event Action? RangeInvalidated;

    /// <summary>Shows other content and tells the list.</summary>
    public void Update(DetailContent content)
    {
        Content = content;
        _jump.Invalidate();
        RangeInvalidated?.Invoke();
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    /// <summary>The text of an open quick jump, whose targets in view get labels; null when none is open.</summary>
    public string? JumpText
    {
        get => _jump.Text;
        set => _jump.Text = value;
    }

    /// <summary>The quick-jump targets of <paramref name="text"/> among the rows in view of <paramref name="listView"/>, nearest the cursor first.</summary>
    /// <param name="listView">The list this source is shown in.</param>
    /// <param name="text">The typed text.</param>
    public IReadOnlyList<JumpLabel> JumpLabels(ListView listView, string text)
    {
        var width = PanePadding.TextWidth(listView.Viewport.Width, ScreenPadding.Columns(listView));
        var window = new JumpWindow(listView.Viewport.Y, listView.Viewport.Height, listView.SelectedItem ?? 0);

        return _jump.Labels(text, window, width, () => new DetailJumpRows(Content, width, glyphs.Ellipsis));
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        var columns = ScreenPadding.Columns(listView);
        width = PanePadding.TextWidth(width, columns);
        var isRange = Range?.Contains(item) == true;
        var isSelected = selected || isRange;
        var isRangeCursor = isRange && selected;
        var jump = JumpLabelOf(listView, item);
        var line = LineAt(item, isSelected, width, jump);
        LinePainter.Paint(listView, PanePadding.Wrap(line, columns), col, row, listView.HasFocus, isRange, isRangeCursor);
    }

    public void Dispose()
    {
    }

    private StyledLine LineAt(int item, bool isSelected, int width, JumpLabel? jump)
    {
        if (item < 0 || item >= Content.Rows.Count)
        {
            return DetailPaneRenderer.RenderEmpty(width, glyphs);
        }

        if (jump is { } label)
        {
            return DetailPaneRenderer.RenderRow(Content, item, isSelected, width, glyphs, label);
        }

        return DetailPaneRenderer.RenderRow(Content.Rows[item], isSelected, width, glyphs, Content.PathStyle);
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
