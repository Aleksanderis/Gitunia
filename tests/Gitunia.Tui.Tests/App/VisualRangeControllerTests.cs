using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.App;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public sealed class VisualRangeControllerTests : IDisposable
{
    private readonly RangeSource source = new(20);
    private readonly ListView list;
    private readonly ListView sidebarList = new();
    private readonly VisualRangeController controller;
    private PaneKind focused = PaneKind.Graph;
    private KeyMode mode = KeyMode.Normal;

    public VisualRangeControllerTests()
    {
        list = new ListView { Source = source };
        controller = new VisualRangeController(
            () => focused,
            pane => pane == PaneKind.Sidebar ? sidebarList : list,
            () => mode,
            newMode => mode = newMode);
    }

    public void Dispose()
    {
        list.Dispose();
        sidebarList.Dispose();
        source.Dispose();
    }

    [Fact]
    public void GivenACursor_WhenTogglingVisual_ThenARangeStartsAtTheCursorAndTheModeSwitches()
    {
        list.SelectedItem = 4;

        var handled = controller.TryExecute(ActionIds.VisualToggle, null);

        Assert.True(handled);
        Assert.True(controller.IsActive);
        Assert.Equal(new RowRange(4, 4), source.Range);
        Assert.Equal(KeyMode.Visual, mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheCursorMoves_ThenTheFarEndFollowsAndTheAnchorStays()
    {
        list.SelectedItem = 4;
        controller.TryExecute(ActionIds.VisualToggle, null);

        list.SelectedItem = 9;
        controller.OnCursorMoved();
        list.SelectedItem = 2;
        controller.OnCursorMoved();

        Assert.Equal(new RowRange(4, 2), source.Range);
        Assert.Equal(3, source.Range!.Value.Count);
    }

    [Theory]
    [InlineData("visual.toggle")]
    [InlineData("cancel")]
    [InlineData("navigate.back")]
    public void GivenAnActiveRange_WhenToggledCancelledOrGoingBack_ThenTheRangeEndsAndNormalModeReturns(string action)
    {
        list.SelectedItem = 4;
        controller.TryExecute(ActionIds.VisualToggle, null);

        var handled = controller.TryExecute(new ActionId(action), null);

        Assert.True(handled);
        Assert.False(controller.IsActive);
        Assert.Null(source.Range);
        Assert.Equal(KeyMode.Normal, mode);
    }

    [Fact]
    public void GivenNoRange_WhenCancelling_ThenTheActionIsLeftToOthers()
    {
        Assert.False(controller.TryExecute(ActionIds.Cancel, null));
        Assert.False(controller.TryExecute(ActionIds.NavigateBack, null));
        Assert.False(controller.TryExecute(ActionIds.ListDown, null));
    }

    [Fact]
    public void GivenAnActiveRange_WhenMotionsArrive_ThenTheControllerLeavesThemToTheList()
    {
        list.SelectedItem = 4;
        controller.TryExecute(ActionIds.VisualToggle, null);

        Assert.False(controller.TryExecute(ActionIds.ListDown, 2));
        Assert.True(controller.IsActive);
    }

    [Fact]
    public void GivenTheSidebarFocused_WhenTogglingVisual_ThenItSaysThereAreNoRangesThere()
    {
        focused = PaneKind.Sidebar;

        var handled = controller.TryExecute(ActionIds.VisualToggle, null);

        Assert.True(handled);
        Assert.False(controller.IsActive);
        Assert.Equal(KeyMode.Normal, mode);
        Assert.Equal("No range selection in the sidebar", controller.Notice);
    }

    [Fact]
    public void GivenAnEmptyList_WhenTogglingVisual_ThenNothingStarts()
    {
        using var emptySource = new RangeSource(0);
        using var empty = new ListView { Source = emptySource };
        var emptyController = new VisualRangeController(() => PaneKind.Graph, _ => empty, () => mode, newMode => mode = newMode);

        emptyController.TryExecute(ActionIds.VisualToggle, null);

        Assert.False(emptyController.IsActive);
        Assert.Equal("Nothing to select", emptyController.Notice);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheFocusMovesToAnotherPane_ThenTheRangeEnds()
    {
        list.SelectedItem = 4;
        controller.TryExecute(ActionIds.VisualToggle, null);

        focused = PaneKind.Detail;
        controller.EndIfFocusMoved();

        Assert.False(controller.IsActive);
        Assert.Null(source.Range);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheFocusStays_ThenTheRangeStays()
    {
        list.SelectedItem = 4;
        controller.TryExecute(ActionIds.VisualToggle, null);

        controller.EndIfFocusMoved();

        Assert.True(controller.IsActive);
    }

    [Theory]
    [InlineData(KeyMode.Insert)]
    [InlineData(KeyMode.Command)]
    [InlineData(KeyMode.Confirm)]
    public void GivenAPromptOwnsTheKeys_WhenTogglingVisual_ThenNoRangeStartsAndTheModeStays(KeyMode promptMode)
    {
        list.SelectedItem = 4;
        mode = promptMode;

        controller.TryExecute(ActionIds.VisualToggle, null);

        Assert.False(controller.IsActive);
        Assert.Null(source.Range);
        Assert.Equal(promptMode, mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheRowsChangeMeaning_ThenTheRangeEnds()
    {
        list.SelectedItem = 4;
        controller.TryExecute(ActionIds.VisualToggle, null);

        source.Invalidate();

        Assert.False(controller.IsActive);
        Assert.Null(source.Range);
        Assert.Equal(KeyMode.Normal, mode);
    }

    [Fact]
    public void GivenAnEndedRange_WhenTheRowsChangeMeaning_ThenNothingHappensAgain()
    {
        list.SelectedItem = 4;
        controller.TryExecute(ActionIds.VisualToggle, null);
        controller.End();
        mode = KeyMode.Insert;

        source.Invalidate();

        Assert.Equal(KeyMode.Insert, mode);
    }

    [Fact]
    public void GivenARangeEndedWhileInInsertMode_WhenEnding_ThenTheModeIsNotChanged()
    {
        list.SelectedItem = 4;
        controller.TryExecute(ActionIds.VisualToggle, null);
        mode = KeyMode.Insert;

        controller.End();

        Assert.Equal(KeyMode.Insert, mode);
    }

    private sealed class RangeSource(int count) : IListDataSource, IRangeSource
    {
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { }
            remove { }
        }

        public int Count => count;

        public int MaxItemLength => 0;

        public bool SuspendCollectionChangedEvent { get; set; }

        public RowRange? Range { get; set; }

        public event Action? RangeInvalidated;

        public void Invalidate() => RangeInvalidated?.Invoke();

        public bool IsMarked(int item) => false;

        public void SetMark(int item, bool value)
        {
        }

        public IList ToList() => Array.Empty<object>();

        public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
        {
        }

        public void Dispose()
        {
        }
    }
}
