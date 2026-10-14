using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Xunit;

namespace Gitunia.Tui.Tests.Views;

public class MainViewTests
{
    private static readonly ScreenModel EmptyModel = new("repository", null, 1, 1, "Normal", string.Empty, 0, 0, false, string.Empty);

    [Fact]
    public void GivenTheMainView_WhenBuilt_ThenTheDetailViewHasItsOwnPanelScheme()
    {
        using var view = CreateView();

        Assert.True(view.Detail.HasScheme, "Without its own scheme the toolkit paints the blank cells below the rows with the terminal default.");
        Assert.Equal(Palette.PanelScheme().Normal, view.Detail.GetScheme().Normal);
    }

    private static MainView CreateView() => new(
        () => EmptyModel,
        _ => new StyledLine([]),
        GraphGlyphSet.Unicode,
        new SidebarListSource(GraphGlyphSet.Unicode),
        () => new SidebarChrome(string.Empty, false, false),
        () => PaneArrangement.Normal,
        _ => new StyledLine([]),
        new DiffListSource(GraphGlyphSet.Unicode));
}
