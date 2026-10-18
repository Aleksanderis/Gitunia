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

    [Fact]
    public void GivenTheMainView_WhenBuilt_ThenOnlyTheGraphIgnoresDoubleClicksOnBlankSpace()
    {
        using var view = CreateView();

        Assert.True(view.Graph.IgnoresBlankDoubleClick);
        Assert.False(view.Detail.IgnoresBlankDoubleClick);
    }

    [Fact]
    public void GivenTheMainView_WhenBuilt_ThenTheHistoryPanesShowAndTheWorkingTreePanesAreHidden()
    {
        using var view = CreateView();

        Assert.True(view.Graph.Visible);
        Assert.True(view.Sidebar.Visible);
        Assert.False(view.WorkingUnstaged.Visible);
        Assert.False(view.WorkingStaged.Visible);
        Assert.False(view.IsChangingVisibility);
    }

    [Fact]
    public void GivenTheMainView_WhenShowingTheWorkingTreeAndHidingItAgain_ThenThePanesSwapBothWays()
    {
        using var view = CreateView();

        view.ShowWorkingTree(true);

        Assert.True(view.WorkingUnstaged.Visible);
        Assert.True(view.WorkingStaged.Visible);
        Assert.True(view.WorkingDiff.Visible);
        Assert.True(view.WorkingMessage.Visible);
        Assert.False(view.Graph.Visible);
        Assert.False(view.Sidebar.Visible);
        Assert.False(view.Detail.Visible);
        Assert.False(view.Diff.Visible);
        Assert.False(view.IsChangingVisibility);

        view.ShowWorkingTree(false);

        Assert.True(view.Graph.Visible);
        Assert.True(view.Sidebar.Visible);
        Assert.False(view.WorkingUnstaged.Visible);
        Assert.False(view.WorkingStaged.Visible);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenThePickerOpensAndCloses_ThenTheListsHideAndComeBackAndTheGraphStaysHidden()
    {
        using var view = CreateView();
        view.ShowWorkingTree(true);

        view.ShowPicker(true);

        Assert.False(view.WorkingUnstaged.Visible);
        Assert.False(view.Graph.Visible);

        view.ShowPicker(false);

        Assert.True(view.WorkingUnstaged.Visible);
        Assert.False(view.Graph.Visible);
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
