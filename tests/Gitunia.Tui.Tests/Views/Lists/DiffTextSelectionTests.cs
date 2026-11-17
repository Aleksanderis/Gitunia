using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Platform;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Gitunia.Tui.Tests.Views.Lists;

/// <summary>A character-wise selection in the diff list: where the pointer lands and which cells are tinted (#1161).</summary>
public class DiffTextSelectionTests
{
    private const int Columns = 40;
    private const int Rows = 6;
    private const int CursorRow = 4;
    private const int RowPrefix = 9;
    private const int NotePrefix = 2;

    private static readonly PaletteTable Table = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, false));

    [Fact]
    public void GivenTextRows_WhenThePointerIsOverACharacter_ThenItsIndexIsReturnedWithoutTheGutter()
    {
        using var source = NewSource();
        using var pane = NewPane(source);
        var start = TextStart(pane, 0);

        Assert.Equal(new TextPosition(0, 6), source.HitAt(pane, start + 6, 0, null)?.Position);
        Assert.Equal(new TextPosition(0, 0), source.HitAt(pane, 0, 0, null)?.Position);
        Assert.Equal(new TextPosition(0, 0), source.HitAt(pane, start - 1, 0, null)?.Position);
        Assert.Equal(new TextPosition(0, 10), source.HitAt(pane, Columns - 1, 0, null)?.Position);
        Assert.Equal(new TextPosition(2, 3), source.HitAt(pane, start + 3, 2, null)?.Position);
    }

    [Fact]
    public void GivenATabInARow_WhenThePointerIsOverItsColumns_ThenTheTabIsReturnedAndTheNextCharacterAfterIt()
    {
        using var source = NewSource();
        using var pane = NewPane(source);
        var start = TextStart(pane, 1);

        Assert.Equal(new TextPosition(1, 0), source.HitAt(pane, start, 1, null)?.Position);
        Assert.Equal(new TextPosition(1, 1), source.HitAt(pane, start + 1, 1, null)?.Position);
        Assert.Equal(new TextPosition(1, 1), source.HitAt(pane, start + 3, 1, null)?.Position);
        Assert.Equal(new TextPosition(1, 2), source.HitAt(pane, start + 4, 1, null)?.Position);
    }

    [Fact]
    public void GivenAPointerAboveOrBelowTheRows_WhenMapping_ThenTheFirstOrLastRowIsUsed()
    {
        using var source = NewSource();
        using var pane = NewPane(source);

        Assert.Equal(0, source.HitAt(pane, 12, -3, null)!.Value.Position.Line);
        Assert.Equal(source.Count - 1, source.HitAt(pane, 12, 40, null)!.Value.Position.Line);
    }

    [Fact]
    public void GivenANoteRow_WhenCopyingASelection_ThenTheNoteIsLeftOut()
    {
        using var source = NewSource();

        var text = source.CopyText(new TextSelection(new TextPosition(2, 1), new TextPosition(4, 1)), false);

        Assert.Equal("ail\nen", text);
    }

    [Fact]
    public void GivenASelectionInsideOneRow_WhenDrawn_ThenOnlyThoseCharactersHaveTheRangeTint()
    {
        using var headless = new HeadlessApplication();
        var application = headless.Application;
        application.Driver!.SetScreenSize(Columns, Rows);
        using var source = NewSource();
        source.Range = new RowRange(0, 0);
        source.Text = new TextSelection(new TextPosition(0, 6), new TextPosition(0, 8));
        using var pane = Show(application, source);
        var start = TextStart(pane, 0);

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, start + 5].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, start + 6].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, start + 8].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, start + 9].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, 0].Attribute));
    }

    [Fact]
    public void GivenASelectionOverSeveralRows_WhenDrawn_ThenTheFirstRowStartsAtItsColumnAndTheLastEndsAtItsColumn()
    {
        using var headless = new HeadlessApplication();
        var application = headless.Application;
        application.Driver!.SetScreenSize(Columns, Rows);
        using var source = NewSource();
        source.Range = new RowRange(0, 2);
        source.Text = new TextSelection(new TextPosition(0, 6), new TextPosition(2, 1));
        using var pane = Show(application, source);
        var first = TextStart(pane, 0);
        var middle = TextStart(pane, 1);
        var last = TextStart(pane, 2);

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[0, first + 5].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[0, first + 10].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[1, middle].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[1, middle + 5].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[2, last + 1].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[2, last + 2].Attribute));
    }

    [Fact]
    public void GivenATabInTheSelection_WhenDrawn_ThenItsExpandedColumnsAreTinted()
    {
        using var headless = new HeadlessApplication();
        var application = headless.Application;
        application.Driver!.SetScreenSize(Columns, Rows);
        using var source = NewSource();
        source.Range = new RowRange(1, 1);
        source.Text = new TextSelection(new TextPosition(1, 1), new TextPosition(1, 1));
        using var pane = Show(application, source);
        var start = TextStart(pane, 1);

        application.LayoutAndDraw(true);

        var contents = application.Driver.Contents!;
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[1, start].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[1, start + 1].Attribute));
        Assert.Equal(Table.RangeBackground, BackgroundOf(contents[1, start + 3].Attribute));
        Assert.NotEqual(Table.RangeBackground, BackgroundOf(contents[1, start + 4].Attribute));
    }

    private static PaneListView NewPane(DiffListSource source) =>
        new() { Width = Columns, Height = Rows, Source = source };

    private static PaneListView Show(IApplication application, DiffListSource source)
    {
        var pane = new PaneListView { X = 0, Y = 0, Width = Columns, Height = Rows, CanFocus = true, Source = source };
        pane.SetScheme(Palette.BaseScheme(Table));
        var window = new Window { Width = Dim.Fill(), Height = Dim.Fill(), BorderStyle = LineStyle.None };
        window.Add(pane);
        application.Begin(window);
        pane.SetFocus();
        pane.SelectedItem = CursorRow;

        return pane;
    }

    // The column where the text of a row starts: the pane padding, then the line numbers and marker (3 digits, two numbers, a marker).
    private static int TextStart(ListView pane, int row)
    {
        var prefix = NewRows()[row].Kind == DiffRowKind.Note ? NotePrefix : RowPrefix;

        return ScreenPadding.Columns(pane) + prefix;
    }

    private static Color BackgroundOf(Attribute? attribute) => attribute!.Value.Background;

    private static DiffRow[] NewRows() =>
    [
        new(DiffRowKind.Context, "hello world", 1, 1, 0),
        new(DiffRowKind.Added, "a\tcd", null, 2, 0),
        new(DiffRowKind.Context, "tail", 3, 3, 0),
        new(DiffRowKind.Note, "no newline at end of file", null, null, 0),
        new(DiffRowKind.Context, "end", 4, 4, 0),
    ];

    private static DiffListSource NewSource()
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(new DiffDocument(NewRows(), [], [], [["f"]], 5));

        return source;
    }
}
