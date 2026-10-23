using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

[Trait("Os", "Independent")]
public class WorkingTreePathStyleTests
{
    private const string Deep = "src/Gitunia.Tui/Rendering/WorkingTree/WorkingTreeRowRenderer.cs";

    [Fact]
    public void GivenTheNameFirstStyle_WhenRendering_ThenTheNameComesFirstAndTheDirectoryIsFaint()
    {
        var line = Render(Modified(Deep), 70);

        Assert.StartsWith("M WorkingTreeRowRenderer.cs  src/Gitunia.Tui/Render", line.Text, StringComparison.Ordinal);
        Assert.Contains(
            line.Segments,
            segment => segment.Role == SegmentRole.Faint
                && segment.Text.StartsWith("  src/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(40)]
    [InlineData(32)]
    [InlineData(28)]
    public void GivenNarrowerLists_WhenRendering_ThenTheNameIsNeverCutAndTheRowKeepsItsWidth(int width)
    {
        var line = Render(Modified(Deep), width);

        Assert.Contains("WorkingTreeRowRenderer.cs", line.Text, StringComparison.Ordinal);
        Assert.Equal(width, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenNoStyle_WhenRendering_ThenItIsTheUnchangedFullPath()
    {
        var line = WorkingTreeRowRenderer.Render(
            Modified(Deep),
            WorkingTreeSection.Unstaged,
            70,
            false,
            GraphGlyphSet.Unicode);

        Assert.StartsWith($"M {Deep}", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAStagedRename_WhenRenderingNameFirst_ThenTheOldNameFollowsTheDirectory()
    {
        var line = WorkingTreeRowRenderer.Render(
            StagedRename("src/old.cs", "src/new.cs"),
            WorkingTreeSection.Staged,
            50,
            false,
            GraphGlyphSet.Unicode,
            null,
            FilePathStyle.NameFirst);

        Assert.StartsWith("R new.cs  src <- old.cs", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAConflict_WhenRenderingNameFirstNarrow_ThenTheConflictTextKeepsItsColumnsAndTheDirectoryGivesWay()
    {
        var line = Render(Conflict(Deep), 50);

        Assert.EndsWith("both modified", line.Text.TrimEnd(), StringComparison.Ordinal);
        Assert.StartsWith("! WorkingTreeRowRenderer.cs", line.Text, StringComparison.Ordinal);
        Assert.Equal(50, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenALoneSurrogate_WhenRenderingNameFirst_ThenItShowsAsAReplacementCharacter()
    {
        var line = Render(Modified("dir/a\uD800b.cs"), 40);

        Assert.StartsWith("M a�b.cs  dir", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPaneRenderer_WhenRenderingAFileRowNameFirst_ThenTheStyleReachesTheRow()
    {
        var tree = Tree(Modified(Deep));

        var line = WorkingTreePaneRenderer.RenderRow(
            tree,
            WorkingTreeSection.Unstaged,
            0,
            90,
            false,
            GraphGlyphSet.Unicode,
            null,
            FilePathStyle.NameFirst);

        Assert.StartsWith("M WorkingTreeRowRenderer.cs  src/", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenANameFirstList_WhenAskingForJumpRows_ThenTheTextIsTheLayoutThatIsDrawn()
    {
        var tree = Tree(Modified(Deep));
        var rows = new WorkingTreeJumpRows(tree, WorkingTreeSection.Unstaged, 40, "…", FilePathStyle.NameFirst);

        var text = rows.TextOf(0);

        Assert.StartsWith("WorkingTreeRowRenderer.cs  …", text, StringComparison.Ordinal);
        Assert.True(TextWidth.Of(text) <= rows.ColumnsOf(0));
    }

    [Fact]
    public void GivenAWideList_WhenLayingOutTheChip_ThenItSitsLeftOfStageAllWithOneColumnBetween()
    {
        var stage = WorkingListTitleButtons.Layout(WorkingTreeSection.Unstaged, "Unstaged 12", 60)!;

        var chip = WorkingListTitleButtons.ViewButton(WorkingTreeSection.Unstaged, "Unstaged 12", 60, null)!;

        Assert.Equal(DefaultMouseMap.ListViewButton, chip.View);
        Assert.Equal(" <space>vv view ▾ ", chip.Label);
        Assert.Equal(stage.Start - 1, chip.Start + chip.Width);
    }

    [Fact]
    public void GivenAListWithRoomForOnlyOneChip_WhenLayingOut_ThenTheChipDropsItsKeyThenGoes()
    {
        var plain = WorkingListTitleButtons.ViewButton(WorkingTreeSection.Staged, "Staged 3", 40, null);

        Assert.Equal(" view ▾ ", plain!.Label);
        Assert.Null(WorkingListTitleButtons.ViewButton(WorkingTreeSection.Staged, "Staged 3", 30, null));
    }

    [Theory]
    [InlineData(WorkingTreeSection.Staged, "Staged 3")]
    [InlineData(WorkingTreeSection.Unstaged, "Unstaged 12")]
    public void GivenAnyWidthThatFitBothChipsPlain_WhenLayingOut_ThenTheStageAllKeyGoesBeforeThePathChip(
        WorkingTreeSection section, string title)
    {
        var titleColumns = TextWidth.Of(title) + 4;
        var plainStageWidth = section == WorkingTreeSection.Staged ? " unstage all ".Length : " stage all ".Length;
        var plainPathWidth = " view ▾ ".Length;
        for (var width = 20; width <= 80; width++)
        {
            var stageStart = width - plainStageWidth - 1;
            var bothFitPlain = stageStart >= titleColumns + 1 && stageStart - 1 - plainPathWidth >= titleColumns + 1;
            if (bothFitPlain)
            {
                Assert.NotNull(WorkingListTitleButtons.ViewButton(section, title, width, null));
            }
        }
    }

    [Fact]
    public void GivenAListTooNarrowForStageAll_WhenLayingOutTheChip_ThenItCanStillTakeTheRightEnd()
    {
        var chip = WorkingListTitleButtons.ViewButton(WorkingTreeSection.Unstaged, "Unstaged 1", 24, null);

        Assert.Null(WorkingListTitleButtons.Layout(WorkingTreeSection.Unstaged, "Unstaged 1", 24));
        Assert.Equal(23, chip!.Start + chip.Width);
    }

    [Theory]
    [InlineData(DefaultKeymap.WorkingUnstagedView)]
    [InlineData(DefaultKeymap.WorkingStagedView)]
    public void GivenTheListChip_WhenLookingUpItsKey_ThenItIsTheKeyThatOpensTheViewDropdown(string view)
    {
        Assert.Equal("<space>vv", ChipKeys.Default.For(DefaultMouseMap.ListViewButton, view));
    }

    [Fact]
    public void GivenTheDetailChip_WhenLookingUpItsKey_ThenItIsTheKeyThatOpensTheViewDropdown()
    {
        Assert.Equal(
            "<space>vv",
            ChipKeys.Default.For(DefaultMouseMap.DetailViewButton, DefaultKeymap.DetailChangesView));
    }

    [Fact]
    public void GivenTheMouseMap_WhenLookingUpTheChips_ThenBothOpenTheViewDropdown()
    {
        var chips = DefaultMouseMap.CreateBindings()
            .Where(binding => binding.View is DefaultMouseMap.DetailViewButton
                or DefaultMouseMap.ListViewButton)
            .ToList();

        Assert.Equal(2, chips.Count);
        Assert.All(chips, binding => Assert.Equal(ActionIds.ListOpenViewOptions, binding.Action));
    }

    private static StyledLine Render(StatusEntry entry, int width) => WorkingTreeRowRenderer.Render(
        entry,
        WorkingTreeSection.Unstaged,
        width,
        false,
        GraphGlyphSet.Unicode,
        null,
        FilePathStyle.NameFirst);
}
