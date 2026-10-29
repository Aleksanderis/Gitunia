using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Platform;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Gitunia.Tui.Tests.Views.Lists;

/// <summary>Draws a diff list with a Visual range and reads the cells back (#814).</summary>
public class RangeRowBackgroundTests
{
    private const int Columns = 30;
    private const int RangeFirstRow = 1;
    private const int RangeLastRow = 2;
    private const int CursorRow = 4;
    private const int PlainRow = 6;

    private static readonly PaletteTable Table = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, false));

    [Fact]
    public void GivenAVisualRange_WhenDrawn_ThenItsRowsHaveTheRangeTintAndTheSelectedRowKeepsTheSelectionTint()
    {
        using var application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        application.Driver!.SetScreenSize(Columns, 8);
        var source = NewSource();
        source.Range = new RowRange(RangeFirstRow, RangeLastRow);
        var pane = new PaneListView { X = 0, Y = 0, Width = Columns, Height = 8, CanFocus = true, Source = source };
        pane.SetScheme(Palette.BaseScheme(Table));
        using var window = new Window();
        window.Width = Dim.Fill();
        window.Height = Dim.Fill();
        window.BorderStyle = LineStyle.None;
        window.Add(pane);
        application.Begin(window);
        pane.SetFocus();
        pane.SelectedItem = CursorRow;

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[RangeFirstRow, Columns - 1].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[RangeLastRow, 0].Attribute));
        Assert.Equal(Table.SelectionFocused, BackgroundOf(contents[CursorRow, Columns - 1].Attribute));
        Assert.NotEqual(Table.SelectionFocused, Table.RangeBackground);
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[PlainRow, Columns - 1].Attribute));
        Assert.NotEqual(Table.SelectionFocused, BackgroundOf(contents[PlainRow, Columns - 1].Attribute));
    }

    [Fact]
    public void GivenTheCursorInsideARange_WhenDrawn_ThenOnlyTheCursorRowIsUnderlined()
    {
        using var application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        application.Driver!.SetScreenSize(Columns, 8);
        var source = NewSource();
        source.Range = new RowRange(RangeFirstRow, RangeLastRow);
        var pane = new PaneListView { X = 0, Y = 0, Width = Columns, Height = 8, CanFocus = true, Source = source };
        pane.SetScheme(Palette.BaseScheme(Table));
        using var window = new Window();
        window.Width = Dim.Fill();
        window.Height = Dim.Fill();
        window.BorderStyle = LineStyle.None;
        window.Add(pane);
        application.Begin(window);
        pane.SetFocus();
        pane.SelectedItem = RangeLastRow;

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        var cursor = contents[RangeLastRow, Columns - 1].Attribute!.Value;
        var other = contents[RangeFirstRow, Columns - 1].Attribute!.Value;
        Assert.True(cursor.Style.HasFlag(TextStyle.Underline));
        Assert.False(other.Style.HasFlag(TextStyle.Underline));
        Assert.Equal(other.Background, cursor.Background);
    }

    private static Color BackgroundOf(Attribute? attribute) => attribute!.Value.Background;

    private static DiffListSource NewSource()
    {
        DiffRow[] rows =
        [
            new(DiffRowKind.Context, "a", 1, 1, 0),
            new(DiffRowKind.Removed, "b", 2, null, 0),
            new(DiffRowKind.Added, "B", null, 2, 0),
            new(DiffRowKind.Context, "c", 3, 3, 0),
            new(DiffRowKind.Context, "d", 4, 4, 0),
            new(DiffRowKind.Context, "e", 5, 5, 0),
            new(DiffRowKind.Context, "f", 6, 6, 0),
        ];
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(new DiffDocument(rows, [1], [], [["f"]], 5));

        return source;
    }
}
