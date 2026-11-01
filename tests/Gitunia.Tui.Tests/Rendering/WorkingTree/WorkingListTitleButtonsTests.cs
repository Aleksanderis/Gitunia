using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

[Trait("Os", "Independent")]
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

    [Theory]
    [InlineData(WorkingTreeSection.Unstaged, "Unstaged 12", " <space>A stage all ")]
    [InlineData(WorkingTreeSection.Staged, "Staged 3", " <space>U unstage all ")]
    public void GivenAWideList_WhenLayingOut_ThenTheButtonShowsItsKeyFromTheKeymap(
        WorkingTreeSection section, string title, string label)
    {
        var button = WorkingListTitleButtons.Layout(section, title, 60)!;

        Assert.Equal(label, button.Label);
        Assert.Equal("<space>A".Length, button.KeyLength);
    }

    [Theory]
    [InlineData(WorkingTreeSection.Unstaged, 36, " stage all ")]
    [InlineData(WorkingTreeSection.Unstaged, 37, " <space>A stage all ")]
    [InlineData(WorkingTreeSection.Staged, 31, " unstage all ")]
    public void GivenANarrowList_WhenLayingOut_ThenTheKeyGoesBeforeTheButton(
        WorkingTreeSection section, int width, string label)
    {
        var title = section == WorkingTreeSection.Staged ? "Staged 3" : "Unstaged 12";

        var button = WorkingListTitleButtons.Layout(section, title, width);

        Assert.Equal(label, button!.Label);
        Assert.Equal(width, button.Start + button.Width + 1);
    }

    [Fact]
    public void GivenARebinding_WhenLayingOut_ThenTheButtonShowsTheNewKey()
    {
        var bindings = DefaultKeymap.CreateBindings()
            .Select(Rebound);
        var keys = new ChipKeys(DefaultKeymap.Create().WithBindings(bindings));

        var button = WorkingListTitleButtons.Layout(WorkingTreeSection.Unstaged, "Unstaged 12", 60, keys)!;

        Assert.Equal(" Ctrl-s stage all ", button.Label);
    }

    [Fact]
    public void GivenAWideList_WhenLayingOutTheResolveChip_ThenItShowsItsKeyAndEndsBeforeTheOtherChips()
    {
        var resolve = WorkingListTitleButtons.ResolveButton("Unstaged 12", 90, null)!;
        var view = WorkingListTitleButtons.ViewButton(WorkingTreeSection.Unstaged, "Unstaged 12", 90, null)!;

        Assert.Equal(DefaultMouseMap.ListResolveButton, resolve.View);
        Assert.Equal(" <space>gr resolve ", resolve.Label);
        Assert.True(resolve.Start + resolve.Width < view.Start);
    }

    [Fact]
    public void GivenANarrowList_WhenLayingOutTheResolveChip_ThenThereIsNoChipWhereItWouldTouchTheTitle()
    {
        Assert.Null(WorkingListTitleButtons.ResolveButton("Unstaged 12", 30, null));
    }

    [Fact]
    public void GivenTheResolveChip_WhenLookingAtTheMouseMap_ThenItRunsTheOpenAction()
    {
        Assert.Contains(
            DefaultMouseMap.CreateBindings(),
            binding => binding.View == DefaultMouseMap.ListResolveButton && binding.Action == ActionIds.ConflictOpen);
    }

    [Theory]
    [InlineData(WorkingTreeSection.Unstaged, "Unstaged 12")]
    [InlineData(WorkingTreeSection.Staged, "Staged 3")]
    public void GivenANarrowList_WhenLayingOutTheDiffChip_ThenItShowsItsKeyAndSitsLeftOfTheStageButton(
        WorkingTreeSection section, string title)
    {
        var diff = WorkingListTitleButtons.DiffButton(section, title, 60, null)!;
        var stage = WorkingListTitleButtons.Layout(section, title, 60)!;

        Assert.Equal(DefaultMouseMap.ListDiffButton, diff.View);
        Assert.Equal(" Enter diff \u203A ", diff.Label);
        Assert.Equal(stage.Start - 1, diff.Start + diff.Width);
    }

    [Fact]
    public void GivenARowThatFitsOnlyTwoChips_WhenTheDiffChipIsShown_ThenTheViewChipGivesWayToIt()
    {
        var withoutDiff = WorkingListTitleButtons.ViewButton(WorkingTreeSection.Unstaged, "Unstaged 12", 40, null);
        var diff = WorkingListTitleButtons.DiffButton(WorkingTreeSection.Unstaged, "Unstaged 12", 40, null);
        var view = WorkingListTitleButtons.ViewButton(WorkingTreeSection.Unstaged, "Unstaged 12", 40, null, true);

        Assert.NotNull(withoutDiff);
        Assert.NotNull(diff);
        Assert.Null(view);
    }

    [Fact]
    public void GivenAWideEnoughList_WhenTheDiffChipIsShown_ThenViewAndResolveEndBeforeIt()
    {
        var diff = WorkingListTitleButtons.DiffButton(WorkingTreeSection.Unstaged, "Unstaged 12", 90, null)!;
        var view = WorkingListTitleButtons.ViewButton(WorkingTreeSection.Unstaged, "Unstaged 12", 90, null, true)!;
        var resolve = WorkingListTitleButtons.ResolveButton("Unstaged 12", 90, null, true)!;

        Assert.True(view.Start + view.Width < diff.Start);
        Assert.True(resolve.Start + resolve.Width < view.Start);
    }

    [Fact]
    public void GivenTooNarrowARow_WhenLayingOutTheDiffChip_ThenThereIsNone()
    {
        Assert.Null(WorkingListTitleButtons.DiffButton(WorkingTreeSection.Unstaged, "Unstaged 12", 20, null));
    }

    [Fact]
    public void GivenTheDiffChip_WhenLookingAtTheMouseMap_ThenItRunsTheOpenDiffAction()
    {
        Assert.Contains(
            DefaultMouseMap.CreateBindings(),
            binding => binding.View == DefaultMouseMap.ListDiffButton
                && binding.Action == ActionIds.WorkingTreeOpenDiff);
    }

    private static KeyBinding Rebound(KeyBinding binding)
    {
        if (binding.Action == ActionIds.WorkingTreeStageAll
            && binding.View == DefaultKeymap.WorkingUnstagedView)
        {
            return binding with { Sequence = KeySequenceParser.Parse("Ctrl-s") };
        }

        return binding;
    }
}
