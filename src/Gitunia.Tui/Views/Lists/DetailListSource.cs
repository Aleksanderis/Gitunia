using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>List source over the rows of the detail pane; the whole content is replaced when another commit is selected or its details arrive.</summary>
public sealed class DetailListSource(GraphGlyphSet glyphs)
    : IListDataSource, IRangeSource, IJumpSource, IRowMenuSource, ITextSource
{
    private readonly JumpLabelCache _jump = new();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public DetailContent Content { get; private set; } = DetailContent.Empty;

    public int Count => Content.Rows.Count;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    public RowRange? Range { get; set; }

    /// <summary>The characters of a character-wise selection, drawn instead of the whole rows of <see cref="Range"/>.</summary>
    public TextSelection? Text { get; set; }

    /// <summary>The detail pane has no sides; the flag is never set.</summary>
    public bool IsOldSide { get; set; }

    /// <summary>The Details tab shows plain text rows (the Changes and Files tabs list files, which are selected as rows).</summary>
    public bool SupportsText => Content.Tab == BottomTab.Details && Content.Rows.Count > 0;

    /// <summary>The text of row <paramref name="item"/> as drawn.</summary>
    /// <param name="item">The row.</param>
    /// <param name="isOldSide">Ignored.</param>
    public string TextOf(int item, bool isOldSide) => DetailTextLines.TextOf(Content, item);

    /// <summary>The character under a pointer position (the pane padding counts as the first character).</summary>
    /// <param name="listView">The list this source is shown in.</param>
    /// <param name="column">The pointer column inside the list view.</param>
    /// <param name="row">The pointer row inside the list view.</param>
    /// <param name="isOldSide">Ignored.</param>
    public TextHit? HitAt(ListView listView, int column, int row, bool? isOldSide)
    {
        if (!SupportsText)
        {
            return null;
        }

        var item = Math.Clamp(listView.Viewport.Y + row, 0, Content.Rows.Count - 1);
        var shown = Math.Max(column - ScreenPadding.Columns(listView), 0);
        var index = ShownColumns.IndexAt(TextOf(item, false), shown, DiffRowRenderer.TabWidth);

        return new TextHit(new TextPosition(item, index), false);
    }

    /// <summary>The start of row <paramref name="item"/>.</summary>
    /// <param name="item">The row.</param>
    /// <param name="isOldSide">Ignored.</param>
    public TextPosition StartAt(int item, bool isOldSide) => new(item, 0);

    /// <summary>The row itself: rows are not wrapped.</summary>
    /// <param name="position">The position.</param>
    /// <param name="isOldSide">Ignored.</param>
    public int ItemShowing(TextPosition position, bool isOldSide) => position.Line;

    /// <summary>The characters of a selection as they are copied: lines joined with a line feed.</summary>
    /// <param name="selection">The selection.</param>
    /// <param name="isOldSide">Ignored.</param>
    public string CopyText(TextSelection selection, bool isOldSide) => selection.TextIn(new DetailTextLines(Content));

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

    /// <inheritdoc />
    public bool HasRowMenu(int item) => Content.FilePathAt(item) is not null;

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        var columns = ScreenPadding.Columns(listView);
        var paneWidth = width;
        var reserved = RowMenuMarker.ReservedColumns(paneWidth, columns);
        width = PanePadding.TextWidth(width, columns) - reserved;
        var isRange = Range?.Contains(item) == true && Text is null;
        var isSelected = selected || isRange;
        var isRangeCursor = isRange && selected;
        var jump = JumpLabelOf(listView, item);
        var line = MarkText(LineAt(item, isSelected, width, jump), item);
        var shown = RowMenuMarker.Apply(
            PanePadding.Wrap(RowMenuMarker.WithReserve(line, reserved), columns),
            paneWidth,
            item == listView.SelectedItem && HasRowMenu(item));
        LinePainter.Paint(listView, shown, col, row, listView.HasFocus, isRange, isRangeCursor);
    }

    public void Dispose()
    {
    }

    // The line with the selected characters of a character-wise selection in the selection tint.
    private StyledLine MarkText(StyledLine line, int item)
    {
        if (Text is not { } selection || !SupportsText || item < 0 || item >= Content.Rows.Count)
        {
            return line;
        }

        var text = TextOf(item, false);
        if (selection.SpanOn(item, text) is not { } span)
        {
            return line;
        }

        var from = ShownColumns.OfIndex(text, span.First, DiffRowRenderer.TabWidth);
        var to = ShownColumns.OfIndex(text, span.Last, DiffRowRenderer.TabWidth)
            + ShownColumns.WidthAt(text, span.Last, DiffRowRenderer.TabWidth);

        return SelectionMarker.Mark(line, from, to);
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
