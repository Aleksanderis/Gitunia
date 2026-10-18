using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Mouse;

public class MouseMapTests
{
    [Theory]
    [InlineData(DefaultKeymap.GraphView)]
    [InlineData(DefaultKeymap.DetailView)]
    [InlineData(DefaultKeymap.SidebarView)]
    [InlineData(DefaultKeymap.DiffView)]
    public void GivenAListPane_WhenWheelingDown_ThenItMovesDownLikeAKeyWithACount(string view)
    {
        var map = DefaultMouseMap.Create();

        var binding = map.Resolve(view, MouseGesture.WheelDown);

        Assert.NotNull(binding);
        Assert.Equal(ActionIds.ListDown, binding.Action);
        Assert.Equal(DefaultMouseMap.WheelLines, binding.Count);
    }

    [Fact]
    public void GivenTheGraph_WhenDoubleClicking_ThenItRunsTheActionOfEnter()
    {
        var map = DefaultMouseMap.Create();

        var binding = map.Resolve(DefaultKeymap.GraphView, MouseGesture.DoubleClick);
        var enter = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create())).Feed("Enter");

        Assert.Equal(enter.Action, binding?.Action);
    }

    [Fact]
    public void GivenTheDetailPane_WhenDoubleClicking_ThenItRunsTheActionOfEnter()
    {
        var map = DefaultMouseMap.Create();

        var binding = map.Resolve(DefaultKeymap.DetailView, MouseGesture.DoubleClick);
        var enter = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.DetailView).Feed("Enter");

        Assert.Equal(enter.Action, binding?.Action);
    }

    [Fact]
    public void GivenTheTabRow_WhenClicking_ThenTheClickedTabNumberIsTheCount()
    {
        var binding = DefaultMouseMap.Create().Resolve(DefaultMouseMap.TabsView, MouseGesture.Click);

        Assert.Equal(ActionIds.TabNext, binding?.Action);
        Assert.True(binding?.UsesTarget);
    }

    [Fact]
    public void GivenTheDirtyMarker_WhenClicking_ThenTheWorkingTreeOpens()
    {
        var binding = DefaultMouseMap.Create().Resolve(DefaultMouseMap.TabDirtyMarker, MouseGesture.Click);

        Assert.Equal(ActionIds.WorkingTreeOpen, binding?.Action);
    }

    [Fact]
    public void GivenAGestureNobodyBinds_WhenResolving_ThenNothingIsReturned()
    {
        var map = DefaultMouseMap.Create();

        Assert.Null(map.Resolve(DefaultKeymap.GraphView, MouseGesture.Click));
        Assert.Null(map.Resolve("Unknown", MouseGesture.WheelUp));
    }

    [Fact]
    public void GivenAViewBindingAndItsParent_WhenResolving_ThenTheViewShadowsTheParent()
    {
        var map = new MouseMap(
            [
                new MouseBinding("Lists", MouseGesture.WheelDown, new ActionId("parent")),
                new MouseBinding("Graph", MouseGesture.WheelDown, new ActionId("child")),
            ],
            new Dictionary<string, string> { ["Graph"] = "Lists", ["Detail"] = "Lists" });

        Assert.Equal(new ActionId("child"), map.Resolve("Graph", MouseGesture.WheelDown)?.Action);
        Assert.Equal(new ActionId("parent"), map.Resolve("Detail", MouseGesture.WheelDown)?.Action);
    }

    [Fact]
    public void GivenAParentCycle_WhenResolving_ThenItStopsWithoutAResult()
    {
        var map = new MouseMap([], new Dictionary<string, string> { ["A"] = "B", ["B"] = "A" });

        Assert.Null(map.Resolve("A", MouseGesture.Click));
    }

    [Fact]
    public void GivenTheDefaultMap_WhenListingBindings_ThenEveryActionIsKnownToTheCatalog()
    {
        var unknown = DefaultMouseMap.CreateBindings()
            .Where(binding => !ActionCatalog.IsDescribed(binding.Action))
            .Select(binding => binding.Action.Value)
            .ToList();

        Assert.Empty(unknown);
    }

    [Fact]
    public void GivenTheUxMouseTable_WhenComparedWithTheDefaults_ThenBothListTheSameBindings()
    {
        var documented = UxMouseReader.ReadRows().Select(row => $"{row.View} {row.Gesture} {row.Action}").Order().ToList();
        var registered = DefaultMouseMap.CreateBindings().Select(binding => $"{binding.View} {binding.Gesture} {binding.Action}").Order().ToList();

        Assert.NotEmpty(documented);
        Assert.Equal(registered, documented);
    }

    [Fact]
    public void GivenTheDefaultMap_WhenListingBindings_ThenEveryActionIsReachableByAKeyToo()
    {
        // Opening the working tree (the dirty marker, #331) is the one-way half of the key toggle <space>s.
        // The right-click row menu (#428) is the palette (':') narrowed to the row's actions: its keys are theirs.
        var keyed = DefaultKeymap.CreateBindings()
            .Select(binding => binding.Action)
            .Append(ActionIds.WorkingTreeOpen)
            .Append(ActionIds.PaletteOpenForRow)
            .ToHashSet();

        var mouseOnly = DefaultMouseMap.CreateBindings()
            .Where(binding => !keyed.Contains(binding.Action))
            .Select(binding => $"{binding.View} {binding.Gesture} {binding.Action}")
            .ToList();

        Assert.Empty(mouseOnly);
    }

    [Theory]
    [InlineData(DefaultMouseMap.DiffPreviousHunkButton, "[c")]
    [InlineData(DefaultMouseMap.DiffNextHunkButton, "]c")]
    [InlineData(DefaultMouseMap.DiffWrapButton, "<space>vw")]
    [InlineData(DefaultMouseMap.DiffZoomButton, "<space>z")]
    public void GivenADiffButton_WhenClicking_ThenItRunsTheActionOfItsKey(string buttonView, string keys)
    {
        var binding = DefaultMouseMap.Create().Resolve(buttonView, MouseGesture.Click);
        var key = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.DiffView).Feed(keys);

        Assert.Equal(key.Action, binding?.Action);
    }

    [Theory]
    [InlineData(DefaultMouseMap.SidebarBranchNewButton, "<space>bc")]
    [InlineData(DefaultMouseMap.SidebarBranchNewOnlyButton, "<space>bC")]
    [InlineData(DefaultMouseMap.SidebarBranchSwitchButton, "<space>bs")]
    [InlineData(DefaultMouseMap.SidebarBranchRenameButton, "<space>br")]
    [InlineData(DefaultMouseMap.SidebarBranchDeleteButton, "<space>bd")]
    [InlineData(DefaultMouseMap.SidebarWorktreeAddButton, "<space>wa")]
    [InlineData(DefaultMouseMap.SidebarWorktreeRemoveButton, "<space>wd")]
    [InlineData(DefaultMouseMap.SidebarWorktreeLockButton, "<space>wl")]
    [InlineData(DefaultMouseMap.SidebarWorktreeUnlockButton, "<space>wu")]
    [InlineData(DefaultMouseMap.SidebarWorktreePruneButton, "<space>wp")]
    [InlineData(DefaultMouseMap.SidebarTagCreateButton, "<space>Tc")]
    [InlineData(DefaultMouseMap.SidebarTagDeleteButton, "<space>Td")]
    [InlineData(DefaultMouseMap.SidebarTagPushButton, "<space>Tp")]
    [InlineData(DefaultMouseMap.SidebarTagDeleteRemoteButton, "<space>TD")]
    [InlineData(DefaultMouseMap.SidebarMergeButton, "<space>gm")]
    [InlineData(DefaultMouseMap.SidebarMergeNoFastForwardButton, "<space>gM")]
    [InlineData(DefaultMouseMap.SidebarMergeSquashButton, "<space>gs")]
    [InlineData(DefaultMouseMap.SidebarStashPushButton, "<space>Ss")]
    [InlineData(DefaultMouseMap.SidebarStashPushUntrackedButton, "<space>Su")]
    [InlineData(DefaultMouseMap.SidebarStashApplyButton, "<space>Sa")]
    [InlineData(DefaultMouseMap.SidebarStashPopButton, "<space>Sp")]
    [InlineData(DefaultMouseMap.SidebarStashDropButton, "<space>Sd")]
    public void GivenASidebarHeaderButton_WhenClicking_ThenItRunsTheActionOfItsKey(string buttonView, string keys)
    {
        var binding = DefaultMouseMap.Create().Resolve(buttonView, MouseGesture.Click);
        var key = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.SidebarView).Feed(keys);

        Assert.Equal(key.Action, binding?.Action);
    }

    [Theory]
    [InlineData(DefaultMouseMap.SidebarRebaseButton, "<space>gr")]
    [InlineData(DefaultMouseMap.SidebarResetButton, "<space>gx")]
    public void GivenARebaseOrResetButton_WhenClicking_ThenItRunsTheActionOfItsKey(string buttonView, string keys)
    {
        var binding = DefaultMouseMap.Create().Resolve(buttonView, MouseGesture.Click);
        var key = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.SidebarView).Feed(keys);

        Assert.Equal(key.Action, binding?.Action);
    }

    [Fact]
    public void GivenARightClickOnAGraphRow_WhenResolving_ThenTheRowCommandsOpen()
    {
        var map = DefaultMouseMap.Create();

        var binding = map.Resolve(DefaultKeymap.GraphView, MouseGesture.RightClick);

        Assert.Equal(ActionIds.PaletteOpenForRow, binding?.Action);
        Assert.Equal(
            ActionIds.PaletteOpenForRow,
            map.Resolve(DefaultKeymap.SidebarView, MouseGesture.RightClick)?.Action);
        Assert.Null(map.Resolve(DefaultKeymap.DiffView, MouseGesture.RightClick));
    }

    [Theory]
    [InlineData(DefaultMouseMap.DiffStageButton, DefaultKeymap.WorkingUnstagedDiffView, "<space>a")]
    [InlineData(DefaultMouseMap.DiffDiscardButton, DefaultKeymap.WorkingUnstagedDiffView, "<space>d")]
    [InlineData(DefaultMouseMap.DiffUnstageButton, DefaultKeymap.WorkingStagedDiffView, "<space>u")]
    [InlineData(DefaultMouseMap.DiffZoomButton, DefaultKeymap.WorkingUnstagedDiffView, "<space>z")]
    public void GivenAWorkingTreeDiffButton_WhenClicking_ThenItRunsTheActionOfItsKey(string buttonView, string diffView, string keys)
    {
        var binding = DefaultMouseMap.Create().Resolve(buttonView, MouseGesture.Click);
        var key = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), diffView).Feed(keys);

        Assert.Equal(key.Action, binding?.Action);
    }

    [Fact]
    public void GivenADragOnEveryListPane_WhenResolving_ThenItInheritsTheListsBindings()
    {
        var map = DefaultMouseMap.Create();

        foreach (var view in new[] { DefaultKeymap.GraphView, DefaultKeymap.DetailView, DefaultKeymap.DiffView })
        {
            Assert.Equal(ActionIds.VisualToggle, map.Resolve(view, MouseGesture.DragStart)?.Action);
            Assert.True(map.Resolve(view, MouseGesture.DragUp)?.UsesTarget);
            Assert.True(map.Resolve(view, MouseGesture.DragDown)?.UsesTarget);
        }
    }

    [Fact]
    public void GivenTheDefaultMap_WhenCheckedForDuplicates_ThenEveryViewAndGestureIsBoundOnce()
    {
        var duplicates = DefaultMouseMap.CreateBindings()
            .GroupBy(binding => new { binding.View, binding.Gesture })
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString())
            .ToList();

        Assert.Empty(duplicates);
    }
}
