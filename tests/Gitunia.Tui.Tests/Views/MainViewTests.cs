using Xunit;
using Gitunia.Tui.Views.Theme;


namespace Gitunia.Tui.Tests.Views;

public class MainViewTests
{
    [Fact]
    public void GivenTheMainView_WhenBuilt_ThenTheDetailViewHasItsOwnScheme()
    {
        using var screen = new MainViewFixture();

        Assert.True(screen.Panes.Detail.HasScheme, "Without its own scheme the toolkit paints the blank cells below the rows with the terminal default.");
        Assert.Equal(Palette.BaseScheme().Normal, screen.Panes.Detail.GetScheme().Normal);
    }

    [Fact]
    public void GivenTheMainView_WhenBuilt_ThenEveryListPaneHasThePaletteScheme()
    {
        using var screen = new MainViewFixture();

        foreach (var pane in new[] { screen.Panes.Graph, screen.Panes.Sidebar, screen.Panes.Detail, screen.Panes.Diff, screen.Panes.WorkingUnstaged, screen.Panes.WorkingStaged, screen.Panes.WorkingDiff, screen.Panes.WorkingMessage })
        {
            Assert.True(pane.HasScheme, "An empty pane would paint the toolkit default background.");
            Assert.Equal(Palette.BaseScheme().Normal, pane.GetScheme().Normal);
            Assert.Equal(Palette.BaseScheme().Focus, pane.GetScheme().Focus);
        }
    }

    [Fact]
    public void GivenTheMainView_WhenBuilt_ThenOnlyTheGraphIgnoresDoubleClicksOnBlankSpace()
    {
        using var screen = new MainViewFixture();

        Assert.True(screen.Panes.Graph.IgnoresBlankDoubleClick);
        Assert.False(screen.Panes.Detail.IgnoresBlankDoubleClick);
    }

    [Fact]
    public void GivenTheMainView_WhenBuilt_ThenTheHistoryPanesShowAndTheWorkingTreePanesAreHidden()
    {
        using var screen = new MainViewFixture();

        Assert.True(screen.Panes.Graph.Visible);
        Assert.True(screen.Panes.Sidebar.Visible);
        Assert.False(screen.Panes.WorkingUnstaged.Visible);
        Assert.False(screen.Panes.WorkingStaged.Visible);
        Assert.False(screen.View.IsChangingVisibility);
    }

    [Fact]
    public void GivenTheMainView_WhenShowingTheWorkingTreeAndHidingItAgain_ThenThePanesSwapBothWays()
    {
        using var screen = new MainViewFixture();

        screen.View.ShowWorkingTree(true);

        Assert.True(screen.Panes.WorkingUnstaged.Visible);
        Assert.True(screen.Panes.WorkingStaged.Visible);
        Assert.True(screen.Panes.WorkingDiff.Visible);
        Assert.True(screen.Panes.WorkingMessage.Visible);
        Assert.False(screen.Panes.Graph.Visible);
        Assert.False(screen.Panes.Sidebar.Visible);
        Assert.False(screen.Panes.Detail.Visible);
        Assert.False(screen.Panes.Diff.Visible);
        Assert.False(screen.View.IsChangingVisibility);

        screen.View.ShowWorkingTree(false);

        Assert.True(screen.Panes.Graph.Visible);
        Assert.True(screen.Panes.Sidebar.Visible);
        Assert.False(screen.Panes.WorkingUnstaged.Visible);
        Assert.False(screen.Panes.WorkingStaged.Visible);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenThePickerOpensAndCloses_ThenTheListsHideAndComeBackAndTheGraphStaysHidden()
    {
        using var screen = new MainViewFixture();
        screen.View.ShowWorkingTree(true);

        screen.View.ShowPicker(true);

        Assert.False(screen.Panes.WorkingUnstaged.Visible);
        Assert.False(screen.Panes.Graph.Visible);

        screen.View.ShowPicker(false);

        Assert.True(screen.Panes.WorkingUnstaged.Visible);
        Assert.False(screen.Panes.Graph.Visible);
    }
}
