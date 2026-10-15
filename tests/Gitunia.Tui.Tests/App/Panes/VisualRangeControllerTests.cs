using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

public sealed class VisualRangeControllerTests : IDisposable
{
    private readonly RangeSource _source = new(20);
    private readonly ListView _list;
    private readonly RangeSource _emptySource = new(0);
    private readonly ListView _emptyList;
    private readonly ListView _sidebarList = new();
    private readonly VisualRangeController _controller;
    private PaneKind _focused = PaneKind.Graph;
    private CopiedRange? _copied;
    private KeyMode _mode = KeyMode.Normal;
    private bool _copySucceeds = true;

    public VisualRangeControllerTests()
    {
        _list = new ListView { Source = _source };
        _emptyList = new ListView { Source = _emptySource };
        _controller = new VisualRangeController(
            () => _focused,
            pane => pane == PaneKind.Sidebar ? _sidebarList : _list,
            () => _mode,
            newMode => _mode = newMode,
            (pane, range) =>
            {
                _copied = new CopiedRange(pane, range);
                return new RangeCopyResult(_copySucceeds, "copy notice");
            });
    }

    public void Dispose()
    {
        _list.Dispose();
        _sidebarList.Dispose();
        _source.Dispose();
        _emptyList.Dispose();
        _emptySource.Dispose();
    }

    [Fact]
    public void GivenACursor_WhenTogglingVisual_ThenARangeStartsAtTheCursorAndTheModeSwitches()
    {
        _list.SelectedItem = 4;

        var handled = _controller.TryExecute(ActionIds.VisualToggle, null);

        Assert.True(handled);
        Assert.True(_controller.IsActive);
        Assert.Equal(new RowRange(4, 4), _source.Range);
        Assert.Equal(KeyMode.Visual, _mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheCursorMoves_ThenTheFarEndFollowsAndTheAnchorStays()
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);

        _list.SelectedItem = 9;
        _controller.OnCursorMoved();
        _list.SelectedItem = 2;
        _controller.OnCursorMoved();

        Assert.Equal(new RowRange(4, 2), _source.Range);
        Assert.Equal(3, _source.Range!.Value.Count);
    }

    [Theory]
    [InlineData("visual.toggle")]
    [InlineData("cancel")]
    [InlineData("navigate.back")]
    [InlineData("app.quit")]
    public void GivenAnActiveRange_WhenToggledCancelledOrGoingBack_ThenTheRangeEndsAndNormalModeReturns(string action)
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);

        var handled = _controller.TryExecute(new ActionId(action), null);

        Assert.True(handled);
        Assert.False(_controller.IsActive);
        Assert.Null(_source.Range);
        Assert.Equal(KeyMode.Normal, _mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenCopying_ThenTheRangeIsCopiedAndEnds()
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);
        _list.SelectedItem = 6;
        _controller.OnCursorMoved();

        var handled = _controller.TryExecute(ActionIds.VisualCopy, null);

        Assert.True(handled);
        Assert.Equal(new CopiedRange(PaneKind.Graph, new RowRange(4, 6)), _copied);
        Assert.Equal("copy notice", _controller.Notice);
        Assert.False(_controller.IsActive);
        Assert.Equal(KeyMode.Normal, _mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenCopyFails_ThenTheRangeStays()
    {
        _copySucceeds = false;
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);

        var handled = _controller.TryExecute(ActionIds.VisualCopy, null);

        Assert.True(handled);
        Assert.Equal(new CopiedRange(PaneKind.Graph, new RowRange(4, 4)), _copied);
        Assert.True(_controller.IsActive);
        Assert.Equal(new RowRange(4, 4), _source.Range);
        Assert.Equal(KeyMode.Visual, _mode);
    }

    [Fact]
    public void GivenNoRange_WhenCopying_ThenNothingIsCopiedAndTheActionIsLeftToOthers()
    {
        Assert.False(_controller.TryExecute(ActionIds.VisualCopy, null));
        Assert.Null(_copied);
    }

    [Fact]
    public void GivenNoRange_WhenCancelling_ThenTheActionIsLeftToOthers()
    {
        Assert.False(_controller.TryExecute(ActionIds.Cancel, null));
        Assert.False(_controller.TryExecute(ActionIds.NavigateBack, null));
        Assert.False(_controller.TryExecute(ActionIds.ListDown, null));
    }

    [Fact]
    public void GivenAnActiveRange_WhenMotionsArrive_ThenTheControllerLeavesThemToTheList()
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);

        Assert.False(_controller.TryExecute(ActionIds.ListDown, 2));
        Assert.True(_controller.IsActive);
    }

    [Fact]
    public void GivenTheSidebarFocused_WhenTogglingVisual_ThenItSaysThereAreNoRangesThere()
    {
        _focused = PaneKind.Sidebar;

        var handled = _controller.TryExecute(ActionIds.VisualToggle, null);

        Assert.True(handled);
        Assert.False(_controller.IsActive);
        Assert.Equal(KeyMode.Normal, _mode);
        Assert.Equal("No range selection in the sidebar", _controller.Notice);
    }

    [Fact]
    public void GivenAnEmptyList_WhenTogglingVisual_ThenNothingStarts()
    {
        var emptyController = new VisualRangeController(
            () => PaneKind.Graph,
            _ => _emptyList,
            () => _mode,
            newMode => _mode = newMode);

        emptyController.TryExecute(ActionIds.VisualToggle, null);

        Assert.False(emptyController.IsActive);
        Assert.Equal("Nothing to select", emptyController.Notice);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheFocusMovesToAnotherPane_ThenTheRangeEnds()
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);

        _focused = PaneKind.Detail;
        _controller.EndIfFocusMoved();

        Assert.False(_controller.IsActive);
        Assert.Null(_source.Range);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheFocusStays_ThenTheRangeStays()
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);

        _controller.EndIfFocusMoved();

        Assert.True(_controller.IsActive);
    }

    [Theory]
    [InlineData(KeyMode.Insert)]
    [InlineData(KeyMode.Command)]
    [InlineData(KeyMode.Confirm)]
    public void GivenAPromptOwnsTheKeys_WhenTogglingVisual_ThenNoRangeStartsAndTheModeStays(KeyMode promptMode)
    {
        _list.SelectedItem = 4;
        _mode = promptMode;

        _controller.TryExecute(ActionIds.VisualToggle, null);

        Assert.False(_controller.IsActive);
        Assert.Null(_source.Range);
        Assert.Equal(promptMode, _mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheRowsChangeMeaning_ThenTheRangeEnds()
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);

        _source.Invalidate();

        Assert.False(_controller.IsActive);
        Assert.Null(_source.Range);
        Assert.Equal(KeyMode.Normal, _mode);
    }

    [Fact]
    public void GivenAnEndedRange_WhenTheRowsChangeMeaning_ThenNothingHappensAgain()
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);
        _controller.End();
        _mode = KeyMode.Insert;

        _source.Invalidate();

        Assert.Equal(KeyMode.Insert, _mode);
    }

    [Fact]
    public void GivenARangeEndedWhileInInsertMode_WhenEnding_ThenTheModeIsNotChanged()
    {
        _list.SelectedItem = 4;
        _controller.TryExecute(ActionIds.VisualToggle, null);
        _mode = KeyMode.Insert;

        _controller.End();

        Assert.Equal(KeyMode.Insert, _mode);
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
