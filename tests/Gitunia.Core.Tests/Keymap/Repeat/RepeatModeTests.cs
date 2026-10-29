using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.Core.Tests.Keymap.Repeat;

public class RepeatModeTests
{
    private static readonly TabId Tab = new(3);

    [Theory]
    [InlineData(PaneKind.Diff)]
    [InlineData(PaneKind.WorkingUnstagedDiff)]
    [InlineData(PaneKind.WorkingStagedDiff)]
    public void GivenAContextStepInADiff_WhenStarting_ThenTheContextModeStartsOnThatPane(PaneKind pane)
    {
        var mode = RepeatMode.Start(ActionIds.DiffLessContext, Tab, pane);

        Assert.Equal(new RepeatMode(RepeatKind.Context, Tab, pane), mode);
        Assert.Equal(RepeatMode.ContextView, mode!.View);
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Detail)]
    [InlineData(PaneKind.WorkingMessage)]
    public void GivenAContextStepOutsideADiff_WhenStarting_ThenNoModeStarts(PaneKind pane)
    {
        Assert.Null(RepeatMode.Start(ActionIds.DiffMoreContext, Tab, pane));
    }

    [Theory]
    [InlineData(PaneKind.Sidebar)]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Detail)]
    [InlineData(PaneKind.Diff)]
    public void GivenAResizeStep_WhenStarting_ThenTheResizeModeStarts(PaneKind pane)
    {
        var mode = RepeatMode.Start(ActionIds.PaneShorter, Tab, pane);

        Assert.Equal(RepeatKind.Resize, mode?.Kind);
        Assert.Equal(RepeatMode.ResizeView, mode!.View);
    }

    [Fact]
    public void GivenTheResetOrAToggle_WhenStarting_ThenNoModeStarts()
    {
        Assert.Null(RepeatMode.Start(ActionIds.PaneResetSizes, Tab, PaneKind.Graph));
        Assert.Null(RepeatMode.Start(ActionIds.DiffToggleWrap, Tab, PaneKind.Diff));
    }

    [Fact]
    public void GivenAMode_WhenTheTabOrThePaneChanges_ThenItNoLongerApplies()
    {
        var mode = new RepeatMode(RepeatKind.Resize, Tab, PaneKind.Graph);

        Assert.True(mode.AppliesTo(Tab, PaneKind.Graph));
        Assert.False(mode.AppliesTo(new TabId(4), PaneKind.Graph));
        Assert.False(mode.AppliesTo(null, PaneKind.Graph));
        Assert.False(mode.AppliesTo(Tab, PaneKind.Detail));
    }
}
