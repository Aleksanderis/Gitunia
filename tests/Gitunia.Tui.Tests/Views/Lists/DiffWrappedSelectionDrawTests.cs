using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Platform;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;
using StyledSegment = Gitunia.Tui.Rendering.Styling.StyledSegment;

namespace Gitunia.Tui.Tests.Views.Lists;

/// <summary>Which cells carry the selection tint when the selected rows wrap, sit side by side or are the lines of the Details tab (#1167).</summary>
public class DiffWrappedSelectionDrawTests
{
    private const int Columns = 90;
    private const int Rows = 8;
    private static readonly int Digits = DiffRowRenderer.GutterDigits(1);
    private static readonly int Prefix = (2 * Digits) + 3;

    private static readonly PaletteTable Table = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, false));

    [Fact]
    public void GivenASelectionAcrossAWrappedRowsBreak_WhenDrawn_ThenBothLinesAreTintedOnlyWhereTheCharactersAre()
    {
        using var application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        application.Driver!.SetScreenSize(Columns, Rows);
        using var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(Document(new DiffRow(DiffRowKind.Context, new string('x', 100), 1, 1, 0)));
        using var pane = Show(application, source, 5);
        var width = PanePadding.TextWidth(Columns, ScreenPadding.Columns(pane));
        source.Width = width;
        source.SetWrapped(true);
        var columns = width - Prefix;
        source.Range = new RowRange(0, 1);
        source.Text = new TextSelection(new TextPosition(0, columns - 2), new TextPosition(1, columns + 1));
        var start = ScreenPadding.Columns(pane) + Prefix;

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, start + columns - 3].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, start + columns - 2].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, start + columns - 1].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[1, start].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[1, start + 1].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[1, start + 2].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[1, 0].Attribute));
    }

    [Fact]
    public void GivenASelectionOnTheNewSideOfTheSideBySideView_WhenDrawn_ThenOnlyTheNewHalfIsTinted()
    {
        using var application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        application.Driver!.SetScreenSize(Columns, Rows);
        using var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(Document(
            new DiffRow(DiffRowKind.Removed, "same text", 1, null, 0),
            new DiffRow(DiffRowKind.Added, "same text", null, 1, 0)));
        using var pane = Show(application, source, 5);
        var width = PanePadding.TextWidth(Columns, ScreenPadding.Columns(pane));
        source.Width = width;
        source.SetSideBySide(true);
        source.Range = new RowRange(0, 0);
        source.Text = new TextSelection(new TextPosition(0, 0), new TextPosition(0, 2));
        var padding = ScreenPadding.Columns(pane);
        var newStart = padding + DiffSplitRenderer.LeftWidth(width) + 1 + DiffSplitRenderer.SidePrefixWidth(Digits);

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, newStart].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, newStart + 2].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, newStart + 3].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, padding + DiffSplitRenderer.SidePrefixWidth(Digits)].Attribute));
    }

    [Fact]
    public void GivenASelectionOnTheOldSideOfTheSideBySideView_WhenDrawn_ThenOnlyTheOldHalfIsTinted()
    {
        using var application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        application.Driver!.SetScreenSize(Columns, Rows);
        using var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(Document(
            new DiffRow(DiffRowKind.Removed, "same text", 1, null, 0),
            new DiffRow(DiffRowKind.Added, "same text", null, 1, 0)));
        using var pane = Show(application, source, 5);
        var width = PanePadding.TextWidth(Columns, ScreenPadding.Columns(pane));
        source.Width = width;
        source.SetSideBySide(true);
        source.Range = new RowRange(0, 0);
        source.IsOldSide = true;
        source.Text = new TextSelection(new TextPosition(0, 4), new TextPosition(0, 6));
        var padding = ScreenPadding.Columns(pane);
        var oldStart = padding + DiffSplitRenderer.SidePrefixWidth(Digits);

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, oldStart + 3].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, oldStart + 4].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, oldStart + 6].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, padding + DiffSplitRenderer.LeftWidth(width) + 1 + DiffSplitRenderer.SidePrefixWidth(Digits) + 4].Attribute));
    }

    [Fact]
    public void GivenASelectionInTheDetailsTab_WhenDrawn_ThenOnlyThoseCharactersHaveTheRangeTint()
    {
        using var application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        application.Driver!.SetScreenSize(Columns, Rows);
        using var source = new DetailListSource(GraphGlyphSet.Unicode);
        source.Update(new DetailContent(
            "abc",
            [new([new StyledSegment("Author: Ann Lee", SegmentRole.Plain)]), new([new StyledSegment("x", SegmentRole.Plain)])],
            2,
            [],
            BottomTab.Details));
        var pane = new PaneListView { X = 0, Y = 0, Width = Columns, Height = Rows, CanFocus = true, Source = source };
        pane.SetScheme(Palette.BaseScheme(Table));
        var window = new Window { Width = Dim.Fill(), Height = Dim.Fill(), BorderStyle = LineStyle.None };
        window.Add(pane);
        application.Begin(window);
        pane.SetFocus();
        pane.SelectedItem = 1;
        source.Range = new RowRange(0, 0);
        source.Text = new TextSelection(new TextPosition(0, 8), new TextPosition(0, 10));
        var start = ScreenPadding.Columns(pane);

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, start + 7].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, start + 8].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, start + 10].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, start + 11].Attribute));
        pane.Dispose();
    }

    // The rows, then filler rows the cursor can rest on, so the tinted rows are not the cursor row.
    private static DiffDocument Document(params DiffRow[] rows)
    {
        var all = rows.Concat(Enumerable.Range(0, 8).Select(index => new DiffRow(DiffRowKind.Context, "filler", 1, 1, 0)));

        return new DiffDocument([.. all], [], [], [["f"]], 1);
    }

    private static PaneListView Show(IApplication application, DiffListSource source, int cursor)
    {
        var pane = new PaneListView { X = 0, Y = 0, Width = Columns, Height = Rows, CanFocus = true, Source = source };
        pane.SetScheme(Palette.BaseScheme(Table));
        var window = new Window { Width = Dim.Fill(), Height = Dim.Fill(), BorderStyle = LineStyle.None };
        window.Add(pane);
        application.Begin(window);
        pane.SetFocus();
        pane.SelectedItem = cursor;

        return pane;
    }

    private static Color BackgroundOf(Attribute? attribute) => attribute!.Value.Background;
}
