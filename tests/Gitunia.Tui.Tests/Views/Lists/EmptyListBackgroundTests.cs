using Gitunia.Core.Workspaces.Repositories;
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

namespace Gitunia.Tui.Tests.Views.Lists;

public class EmptyListBackgroundTests
{
    [Theory]
    [InlineData(ColorDepth.TrueColor, true)]
    [InlineData(ColorDepth.TrueColor, false)]
    [InlineData(ColorDepth.Ansi16, true)]
    [InlineData(ColorDepth.Ansi16, false)]
    [InlineData(ColorDepth.None, true)]
    [InlineData(ColorDepth.None, false)]
    public void GivenAnEmptyListPane_WhenDrawn_ThenEveryCellHasThePaletteBackground(ColorDepth depth, bool focused)
    {
        var table = PaletteTable.For(new PaletteSettings(depth, false, PaintsBase: true));
        using var application = Application.Create();
        application.Init(DriverRegistry.Names.ANSI);
        application.Driver!.SetScreenSize(20, 6);
        var pane = new PaneListView
        {
            X = 0,
            Y = 0,
            Width = 19,
            Height = 6,
            CanFocus = true,
            Source = new WorkingTreeListSource(WorkingTreeSection.Staged, GraphGlyphSet.Unicode),
        };
        pane.SetScheme(Palette.BaseScheme(table));
        var other = new PaneListView { X = 19, Y = 0, Width = 1, Height = 1, CanFocus = true };
        using var window = new Window();
        window.Width = Dim.Fill();
        window.Height = Dim.Fill();
        window.BorderStyle = LineStyle.None;
        // The window owns and disposes both panes.
        window.Add(pane, other);
        application.Begin(window);
        if (focused)
        {
            pane.SetFocus();
        }
        else
        {
            other.SetFocus();
        }

        application.LayoutAndDraw(true);

        Assert.Equal(focused, pane.HasFocus);
        var contents = application.Driver.Contents!;
        for (var row = 0; row < 6; row++)
        {
            for (var column = 0; column < 19; column++)
            {
                var attribute = contents[row, column].Attribute!.Value;
                Assert.True(table.BaseBackground == attribute.Background, $"{depth} focused={focused} cell {column},{row}: {attribute.Background}");
            }
        }
    }
}
