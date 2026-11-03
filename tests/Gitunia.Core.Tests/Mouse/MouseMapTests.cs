using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Mouse;

[Trait("Os", "Independent")]
public class MouseMapTests
{
    [Theory]
    [InlineData(DefaultMouseMap.ReflogSelectButton)]
    [InlineData(DefaultMouseMap.ReflogResetButton)]
    [InlineData(DefaultMouseMap.ReflogBranchButton)]
    [InlineData(DefaultMouseMap.ReflogCheckoutButton)]
    [InlineData(DefaultMouseMap.ReflogPickerRow)]
    [InlineData(DefaultMouseMap.MainlineParentRow)]
    public void GivenAReflogOverlayView_WhenClicked_ThenItIsBoundAndRunsWhileAnOverlayIsOpen(string view)
    {
        Assert.Contains(view, DefaultMouseMap.OverlayViews);
        Assert.NotNull(DefaultMouseMap.Create().Resolve(view, MouseGesture.Click));
    }

    [Theory]
    [InlineData(DefaultMouseMap.RemoteFetchButton, "remote.fetch")]
    [InlineData(DefaultMouseMap.RemotePullButton, "remote.pull")]
    [InlineData(DefaultMouseMap.RemotePushButton, "remote.push")]
    [InlineData(DefaultMouseMap.RemoteCancelButton, "remote.cancel")]
    public void GivenARemoteChip_WhenClicked_ThenItRunsTheActionTheKeyRuns(string view, string action)
    {
        Assert.Equal(action, DefaultMouseMap.Create().Resolve(view, MouseGesture.Click)?.Action.Value);
    }

    [Fact]
    public void GivenAMainlineParentRow_WhenClicked_ThenTheClickTargetIsTheParentNumber()
    {
        var binding = DefaultMouseMap.Create().Resolve(DefaultMouseMap.MainlineParentRow, MouseGesture.Click);

        Assert.Equal(ActionIds.PickMainline, binding?.Action);
        Assert.True(binding?.UsesTarget);
    }

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
    public void GivenTheGraph_WhenDoubleClicking_ThenItOpensTheRowMenuLikeSpaceDot()
    {
        var map = DefaultMouseMap.Create();

        var binding = map.Resolve(DefaultKeymap.GraphView, MouseGesture.DoubleClick);
        var keys = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create())).Feed("<space>.");

        Assert.Equal(keys.Action, binding?.Action);
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
        // The mainline parent row (#449) is the digit key, which the open prompt reads as typed text in Insert mode.
        // A help tab label (#652) is the jump straight to a tab that Tab, Shift-Tab, j and k reach one step at a time.
        var keyed = DefaultKeymap.CreateBindings()
            .Select(binding => binding.Action)
            .Append(ActionIds.WorkingTreeOpen)
            .Append(ActionIds.PickMainline)
            .Append(ActionIds.HelpTabSelect)
            .ToHashSet();

        var mouseOnly = DefaultMouseMap.CreateBindings()
            .Where(binding => !keyed.Contains(binding.Action))
            .Select(binding => $"{binding.View} {binding.Gesture} {binding.Action}")
            .ToList();

        Assert.Empty(mouseOnly);
    }

    [Theory]
    [InlineData(DefaultMouseMap.DiffPreviousFileButton, "[f")]
    [InlineData(DefaultMouseMap.DiffNextFileButton, "]f")]
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

    [Fact]
    public void GivenAClickOnTheRowMenuMarker_WhenResolving_ThenTheRowMenuOpens()
    {
        var binding = DefaultMouseMap.Create().Resolve(DefaultMouseMap.RowMenuButton, MouseGesture.Click);

        Assert.Equal(ActionIds.MenuOpenForRow, binding?.Action);
    }

    [Fact]
    public void GivenTheMouseGestures_WhenListingThem_ThenNoRightMiddleOrCtrlGestureExists()
    {
        Assert.DoesNotContain(
            Enum.GetNames<MouseGesture>(),
            name => name.Contains("Right") || name.Contains("Middle") || name.Contains("Ctrl"));
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
    public void GivenTheHistoryDiff_WhenDoubleOrTripleClicking_ThenTheWordOrTheLineIsSelectedAndTheChangesViewDiffStillStages()
    {
        var map = DefaultMouseMap.Create();

        Assert.Equal(ActionIds.VisualSelectWord, map.Resolve(DefaultKeymap.DiffView, MouseGesture.DoubleClick)?.Action);
        Assert.Equal(ActionIds.VisualSelectLine, map.Resolve(DefaultKeymap.DiffView, MouseGesture.TripleClick)?.Action);
        Assert.Equal(ActionIds.WorkingTreeStage, map.Resolve(DefaultKeymap.WorkingUnstagedDiffView, MouseGesture.DoubleClick)?.Action);
        Assert.Null(map.Resolve(DefaultKeymap.WorkingUnstagedDiffView, MouseGesture.TripleClick));
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
