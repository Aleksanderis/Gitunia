using Gitunia.App;
using Gitunia.Core;
using Gitunia.App.Shell;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Tests.Fakes;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

/// <summary>The terminal side of Visual mode: keys to commands, the cursor to the range, the range to the list source.</summary>
public sealed class VisualRangeInputTests : IDisposable
{
    private readonly RangeSource _source = new(20);
    private readonly ListView _list;
    private readonly RangeSource _emptySource = new(0);
    private readonly ListView _emptyList;
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly List<AppEffect> _effects = [];
    private readonly VisualRangeInput _input;

    public VisualRangeInputTests()
    {
        _list = new ListView { Source = _source };
        _emptyList = new ListView { Source = _emptySource };
        var root = Path.Combine(Path.GetTempPath(), "gitunia-visual-input");
        var gitDirectory = Path.Combine(root, ".git");
        _host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, gitDirectory, gitDirectory, false, false)));
        _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new FocusPaneCommand(PaneKind.Graph)));
        _host.EffectRaised += _effects.Add;
        _input = new VisualRangeInput(_host, new FakePaneLists(_list));
    }

    public void Dispose()
    {
        _list.Dispose();
        _source.Dispose();
        _emptyList.Dispose();
        _emptySource.Dispose();
    }

    [Fact]
    public void GivenACursor_WhenTogglingVisual_ThenTheRangeIsDrawnAtTheCursorRow()
    {
        _list.SelectedItem = 4;

        var handled = _input.TryExecute(ActionIds.VisualToggle, null);

        Assert.True(handled);
        Assert.True(_input.IsActive);
        Assert.Equal(new RowRange(4, 4), _source.Range);
        Assert.Equal(KeyMode.Visual, _host.State.Shell.Mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheCursorMoves_ThenTheFarEndFollowsAndTheAnchorStays()
    {
        _list.SelectedItem = 4;
        _input.TryExecute(ActionIds.VisualToggle, null);

        _list.SelectedItem = 9;
        _input.OnCursorMoved();
        _list.SelectedItem = 2;
        _input.OnCursorMoved();

        Assert.Equal(new RowRange(4, 2), _source.Range);
        Assert.Equal(3, _source.Range!.Value.Count);
    }

    [Theory]
    [InlineData("visual.toggle")]
    [InlineData("cancel")]
    [InlineData("navigate.back")]
    [InlineData("app.quit")]
    public void GivenAnActiveRange_WhenToggledCancelledOrGoingBack_ThenTheRangeEndsAndTheListIsCleared(string action)
    {
        _list.SelectedItem = 4;
        _input.TryExecute(ActionIds.VisualToggle, null);

        var handled = _input.TryExecute(new ActionId(action), null);

        Assert.True(handled);
        Assert.False(_input.IsActive);
        Assert.Null(_source.Range);
        Assert.Equal(KeyMode.Normal, _host.State.Shell.Mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenCopying_ThenTheRangeIsAskedToBeCopied()
    {
        _list.SelectedItem = 4;
        _input.TryExecute(ActionIds.VisualToggle, null);
        _list.SelectedItem = 6;
        _input.OnCursorMoved();

        var handled = _input.TryExecute(ActionIds.VisualCopy, null);

        Assert.True(handled);
        var copy = Assert.Single(_effects.OfType<CopyVisualRangeEffect>());
        Assert.Equal(new RowRange(4, 6), copy.Range);
        Assert.Equal(PaneKind.Graph, copy.Pane);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheDragIsReleased_ThenThePointerSelectionIsReported()
    {
        var reported = 0;
        _input.PointerSelected += () => reported++;
        _list.SelectedItem = 4;
        _input.TryExecute(ActionIds.VisualToggle, null);

        _input.OnDragReleased();

        Assert.Equal(1, reported);
    }

    [Fact]
    public void GivenNoRange_WhenADragIsReleased_ThenNothingIsReported()
    {
        var reported = 0;
        _input.PointerSelected += () => reported++;

        _input.OnDragReleased();

        Assert.Equal(0, reported);
    }

    [Fact]
    public void GivenNoRange_WhenCopyingOrCancelling_ThenTheActionsAreLeftToOthers()
    {
        Assert.False(_input.TryExecute(ActionIds.VisualCopy, null));
        Assert.False(_input.TryExecute(ActionIds.Cancel, null));
        Assert.False(_input.TryExecute(ActionIds.NavigateBack, null));
        Assert.False(_input.TryExecute(ActionIds.ListDown, null));
        Assert.Empty(_effects.OfType<CopyVisualRangeEffect>());
    }

    [Fact]
    public void GivenAnActiveRange_WhenMotionsArrive_ThenTheInputLeavesThemToTheList()
    {
        _list.SelectedItem = 4;
        _input.TryExecute(ActionIds.VisualToggle, null);

        Assert.False(_input.TryExecute(ActionIds.ListDown, 2));
        Assert.True(_input.IsActive);
    }

    [Fact]
    public void GivenAnEmptyList_WhenTogglingVisual_ThenNothingStarts()
    {
        var emptyInput = new VisualRangeInput(_host, new FakePaneLists(_emptyList));

        emptyInput.TryExecute(ActionIds.VisualToggle, null);

        Assert.False(emptyInput.IsActive);
        Assert.Equal(VisualReducer.NothingToSelectNotice, NoticeOf(_effects));
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheRowsChange_ThenTheRangeEndsWithANotice()
    {
        _list.SelectedItem = 4;
        _input.TryExecute(ActionIds.VisualToggle, null);

        _source.Invalidate();

        Assert.False(_input.IsActive);
        Assert.Null(_source.Range);
        Assert.Equal(KeyMode.Normal, _host.State.Shell.Mode);
        Assert.Equal(VisualReducer.InvalidatedNotice, NoticeOf(_effects));
    }

    [Fact]
    public void GivenAnEndedRange_WhenTheRowsChangeMeaning_ThenNothingHappensAgain()
    {
        _list.SelectedItem = 4;
        _input.TryExecute(ActionIds.VisualToggle, null);
        _input.End();
        _host.SetKeyMode(KeyMode.Insert);

        _source.Invalidate();

        Assert.Equal(KeyMode.Insert, _host.State.Shell.Mode);
    }

    [Fact]
    public void GivenAnActiveRange_WhenTheFocusMovesToAnotherPane_ThenTheListIsCleared()
    {
        _list.SelectedItem = 4;
        _input.TryExecute(ActionIds.VisualToggle, null);

        _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new FocusPaneCommand(PaneKind.Detail)));

        Assert.False(_input.IsActive);
        Assert.Null(_source.Range);
    }

    private static string NoticeOf(List<AppEffect> effects) =>
        Assert.Single(effects.OfType<PostStatusEffect>()).Text;
}
