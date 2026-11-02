using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

[Trait("Os", "Independent")]
public class WorkingTreeLayoutTests
{
    [Fact]
    public void GivenAWideScreen_WhenComputing_ThenListsAreStackedOnTheLeftAndTheDiffAndMessageOnTheRight()
    {
        var rects = WorkingTreeLayout.Compute(120, 36, PaneKind.WorkingUnstaged);

        Assert.Equal(new PaneRect(1, 2, 39, 16), rects.Unstaged);
        Assert.Equal(new PaneRect(1, 19, 39, 15), rects.Staged);
        Assert.Equal(new PaneRect(41, 2, 78, 26), rects.Diff);
        Assert.Equal(new PaneRect(41, 29, 78, 5), rects.Message);
    }

    [Fact]
    public void GivenAWideScreen_WhenComputing_ThenTheMessageBoxSplitsIntoEditorRowsAndOneOptionRow()
    {
        var rects = WorkingTreeLayout.Compute(120, 36, PaneKind.WorkingMessage);

        Assert.Equal(new PaneRect(41, 29, 78, 4), rects.MessageEditor);
        Assert.Equal(new PaneRect(41, 33, 78, 1), rects.MessageOptions);
    }

    [Fact]
    public void GivenANarrowScreenWithTheMessageBoxFocused_WhenComputing_ThenOnlyTheMessageBoxShows()
    {
        var rects = WorkingTreeLayout.Compute(70, 20, PaneKind.WorkingMessage);

        Assert.True(rects.Unstaged.IsEmpty);
        Assert.True(rects.Staged.IsEmpty);
        Assert.True(rects.Diff.IsEmpty);
        Assert.Equal(new PaneRect(1, 2, 68, 16), rects.Message);
    }

    [Fact]
    public void GivenAShortScreenWithTheMessageBoxFocused_WhenComputing_ThenItTakesThePlaceOfTheDiff()
    {
        var unfocused = WorkingTreeLayout.Compute(100, 9, PaneKind.WorkingUnstaged);
        var rects = WorkingTreeLayout.Compute(100, 9, PaneKind.WorkingMessage);

        Assert.True(unfocused.Message.IsEmpty);
        Assert.True(rects.Diff.IsEmpty);
        Assert.Equal(unfocused.Diff, rects.Message);
        Assert.Equal(unfocused.Unstaged, rects.Unstaged);
    }

    [Theory]
    [InlineData(120, 36)]
    [InlineData(80, 24)]
    [InlineData(80, 12)]
    [InlineData(100, 9)]
    public void GivenSizesFromNormalToShort_WhenComputing_ThenPanesFillTheFrameAndNeverOverlap(int width, int height)
    {
        var rects = WorkingTreeLayout.Compute(width, height, PaneKind.WorkingUnstaged);

        var area = ScreenLayout.GraphHeight(height);
        if (!rects.Unstaged.IsEmpty && !rects.Staged.IsEmpty)
        {
            Assert.Equal(area, rects.Unstaged.Height + 1 + rects.Staged.Height);
            Assert.Equal(rects.Unstaged.Y + rects.Unstaged.Height + 1, rects.Staged.Y);
        }

        Assert.Equal(width - 1, rects.Diff.X + rects.Diff.Width);
        Assert.Equal(Math.Max(rects.Unstaged.Width, rects.Staged.Width) + 2, rects.Diff.X);
        if (!rects.Message.IsEmpty)
        {
            Assert.Equal(area, rects.Diff.Height + 1 + rects.Message.Height);
        }
    }

    [Fact]
    public void GivenANarrowScreenWithUnstagedFocused_WhenComputing_ThenOnlyUnstagedShowsAtFullSize()
    {
        var rects = WorkingTreeLayout.Compute(64, 20, PaneKind.WorkingUnstaged);

        Assert.Equal(new PaneRect(1, 2, 62, 16), rects.Unstaged);
        Assert.True(rects.Staged.IsEmpty);
        Assert.True(rects.Diff.IsEmpty);
        Assert.True(rects.Message.IsEmpty);
    }

    [Fact]
    public void GivenANarrowScreenWithStagedFocused_WhenComputing_ThenOnlyStagedShows()
    {
        var rects = WorkingTreeLayout.Compute(79, 20, PaneKind.WorkingStaged);

        Assert.True(rects.Unstaged.IsEmpty);
        Assert.Equal(new PaneRect(1, 2, 77, 16), rects.Staged);
    }

    [Fact]
    public void GivenAShortWideScreen_WhenComputing_ThenOnlyTheFocusedListShowsBesideTheDiff()
    {
        var rects = WorkingTreeLayout.Compute(100, 9, PaneKind.WorkingStaged);

        Assert.True(rects.Unstaged.IsEmpty);
        Assert.Equal(new PaneRect(1, 2, 32, 5), rects.Staged);
        Assert.False(rects.Diff.IsEmpty);
        Assert.True(rects.Message.IsEmpty);
    }

    [Fact]
    public void GivenAScreenAtTheSideBySideLimit_WhenComputing_ThenTheListsKeepTheirMinimumWidth()
    {
        var rects = WorkingTreeLayout.Compute(WorkingTreeLayout.MinimumWidthForSideBySide, 30, PaneKind.WorkingUnstaged);

        Assert.True(rects.Unstaged.Width >= 24);
        Assert.True(rects.Diff.Width >= 40);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 4)]
    [InlineData(10, 3)]
    [InlineData(200, 80)]
    public void GivenTinyAndHugeScreens_WhenComputing_ThenItNeverThrowsAndNeverHasNegativeSizes(int width, int height)
    {
        var rects = WorkingTreeLayout.Compute(width, height, PaneKind.WorkingStaged);

        foreach (var rect in new[] { rects.Unstaged, rects.Staged, rects.Diff, rects.Message })
        {
            Assert.True(rect.Width >= 0 && rect.Height >= 0);
        }
    }
}
