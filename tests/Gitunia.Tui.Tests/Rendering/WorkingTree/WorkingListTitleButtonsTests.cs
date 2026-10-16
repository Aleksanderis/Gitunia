using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

public class WorkingListTitleButtonsTests
{
    [Fact]
    public void GivenAnUnstagedListThatFits_WhenLayingOut_ThenStageAllSitsAtTheRightEndWithOneColumnOfLineAfterIt()
    {
        var button = WorkingListTitleButtons.Layout(WorkingTreeSection.Unstaged, "Unstaged 12", 40);

        Assert.NotNull(button);
        Assert.Equal(DefaultMouseMap.StageAllButton, button.View);
        Assert.Equal(40, button.Start + button.Width + 1);
    }

    [Fact]
    public void GivenAStagedList_WhenLayingOut_ThenItGetsUnstageAll()
    {
        var button = WorkingListTitleButtons.Layout(WorkingTreeSection.Staged, "Staged 3", 40);

        Assert.Equal(DefaultMouseMap.UnstageAllButton, button!.View);
    }

    [Theory]
    [InlineData(WorkingTreeSection.Unstaged, 24)]
    [InlineData(WorkingTreeSection.Staged, 24)]
    [InlineData(WorkingTreeSection.Unstaged, 0)]
    public void GivenARowThatIsTooNarrow_WhenLayingOut_ThenThereIsNoButton(WorkingTreeSection section, int width)
    {
        Assert.Null(WorkingListTitleButtons.Layout(section, "Unstaged 123", width));
    }

    [Fact]
    public void GivenTheButtons_WhenLookingAtTheMouseMap_ThenEachRunsItsAllAction()
    {
        var bindings = DefaultMouseMap.CreateBindings();

        Assert.Contains(bindings, binding => binding.View == DefaultMouseMap.StageAllButton && binding.Action == ActionIds.WorkingTreeStageAll);
        Assert.Contains(bindings, binding => binding.View == DefaultMouseMap.UnstageAllButton && binding.Action == ActionIds.WorkingTreeUnstageAll);
    }
}
