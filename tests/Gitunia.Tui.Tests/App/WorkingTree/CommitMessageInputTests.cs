using Gitunia.App;
using Gitunia.Core.Commits;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Persistence;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Tests.Fakes;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.WorkingTree;

// The keys and clicks of the message box become commands; what they do is tested in the Core reducer tests.
public sealed class CommitMessageInputTests : IDisposable
{
    [Fact]
    public void GivenAPlacedCursor_WhenDraggingToAnotherCell_ThenTheTextBetweenIsSelected()
    {
        SetText("first line\nsecond line");
        _input.PlaceCursor(2, 0, 20);

        _input.DragSelection(4, 1, 20);

        Assert.Equal("rst line\nseco", Tab.Commit.Draft!.Editor.SelectedText);
    }

    [Fact]
    public void GivenADragAboveTheBox_WhenExtending_ThenTheSelectionStopsAtTheFirstLine()
    {
        SetText("first line\nsecond line");
        _input.PlaceCursor(3, 1, 20);

        _input.DragSelection(0, -4, 20);

        Assert.Equal("first line\nsec", Tab.Commit.Draft!.Editor.SelectedText);
    }

    private readonly TestShell _shell = new();
    private readonly TestCommitInput.RecordingRoute _route = new();
    private readonly CommitMessageInput _input;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gitunia-commit-input-{Guid.NewGuid():N}");

    public CommitMessageInputTests()
    {
        Directory.CreateDirectory(_root);
        var location = new RepositoryLocation(_root, _root, _root, false, false);
        _shell.Host.Dispatch(new OpenRepositoryCommand(location));
        Send(new WorkingTreeLoadedCommand(Snapshot(StagedModified("a.txt"))));
        Send(new OpenWorkingTreeCommand());
        _input = new CommitMessageInput(_shell.Host, _route);
    }

    private Dispatcher Host => _shell.Host;

    private RepositoryState Tab => Host.Workspace.ActiveTab!;

    [Fact]
    public void GivenTheListsFocused_WhenWritingTheMessage_ThenTheBoxStartsEditing()
    {
        Assert.True(_input.TryExecute(Tab, ActionIds.WorkingTreeWriteMessage));

        Assert.Equal(PaneKind.WorkingMessage, Tab.View.FocusedPane);
        Assert.Equal(KeyMode.Insert, _shell.Mode);
        Assert.True(_input.IsEditing);
        Assert.True(_input.ShowsCursor);
    }

    [Fact]
    public void GivenAnActionOfAnotherPane_WhenAsked_ThenTheBoxLeavesItToTheOthers()
    {
        Assert.False(_input.TryExecute(Tab, ActionIds.ListDown));
        Assert.False(_input.TryExecute(Tab, ActionIds.Cancel));
        Assert.False(_input.TryExecute(Tab, ActionIds.NavigateBack));
    }

    [Fact]
    public void GivenInsertMode_WhenKeysAreTyped_ThenTheBoxTakesThem()
    {
        _input.TryExecute(Tab, ActionIds.WorkingTreeWriteMessage);

        Assert.True(_input.HandleTextKey(new KeyEvent("h")));
        Assert.True(_input.HandleTextKey(new KeyEvent("i")));

        Assert.Equal("hi", Tab.Commit.Draft!.Editor.Text);
    }

    [Fact]
    public void GivenNoInsertMode_WhenAKeyIsTyped_ThenTheBoxDoesNotTakeIt()
    {
        Assert.False(_input.HandleTextKey(new KeyEvent("x")));
        Assert.Null(Tab.Commit.Draft);
    }

    [Fact]
    public void GivenAnotherPromptInInsertMode_WhenEnterArrives_ThenItIsPassedOnAsAccept()
    {
        _input.TryExecute(Tab, ActionIds.WorkingTreeWriteMessage);
        _input.TryExecute(Tab, ActionIds.Cancel);
        Host.Dispatch(new SetKeyModeCommand(KeyMode.Insert));

        Assert.True(_input.TryExecute(Tab, ActionIds.WorkingTreeMessageNewLine));

        Assert.Equal([ActionIds.InputAccept], _route.Routed);
        Assert.True(Tab.Commit.Draft is null or { Editor.IsBlank: true });
    }

    [Fact]
    public void GivenEnterInTheEditedBox_WhenPressed_ThenALineIsAddedAndNothingIsRouted()
    {
        _input.TryExecute(Tab, ActionIds.WorkingTreeWriteMessage);
        _input.HandleTextKey(new KeyEvent("a"));

        _input.TryExecute(Tab, ActionIds.WorkingTreeMessageNewLine);

        Assert.Equal(2, Tab.Commit.Draft!.Editor.Lines.Count);
        Assert.Empty(_route.Routed);
    }

    [Fact]
    public void GivenAMessage_WhenClickingAnotherLine_ThenTheCursorLandsThereWithoutScrollOffset()
    {
        SetText("first line\nsecond line");

        _input.PlaceCursor(3, 0, 20);

        Assert.Equal((0, 3), (Tab.Commit.Draft!.Editor.CursorRow, Tab.Commit.Draft.Editor.CursorColumn));
    }

    [Fact]
    public void GivenAScrolledCursorLineWhileEditing_WhenClickingIt_ThenTheScrollOffsetIsAdded()
    {
        _input.TryExecute(Tab, ActionIds.WorkingTreeWriteMessage);
        foreach (var character in "abcdefghij")
        {
            _input.HandleTextKey(new KeyEvent(character.ToString()));
        }

        _input.PlaceCursor(1, 0, 4);

        Assert.Equal(8, Tab.Commit.Draft!.Editor.CursorColumn);
    }

    [Fact]
    public void GivenALongLineAndNormalMode_WhenClickingPastTheBoxWidth_ThenTheCursorLandsWhereTheTextIsScrolledTo()
    {
        _input.TryExecute(Tab, ActionIds.WorkingTreeWriteMessage);
        foreach (var _ in Enumerable.Range(0, 30))
        {
            _input.HandleTextKey(new KeyEvent("x"));
        }

        _input.TryExecute(Tab, ActionIds.Cancel);
        var offset = MessageEditorRenderer.ScrollOffset(Tab.Commit.Draft!.Editor, 10);

        _input.PlaceCursor(2, 0, 10);

        Assert.True(offset + 2 > 2);
        Assert.Equal(offset + 2, Tab.Commit.Draft.Editor.CursorColumn);
    }

    [Fact]
    public void GivenTheBoxIsScrolledDown_WhenClicking_ThenTheRowIsRelativeToTheFirstVisibleLine()
    {
        SetText("a\nb\nc\nd");
        Tab.Commit.Draft!.Top = 2;

        _input.PlaceCursor(0, 1, 20);

        Assert.Equal(3, Tab.Commit.Draft.Editor.CursorRow);
    }

    [Fact]
    public void GivenNoDraft_WhenClicking_ThenNothingHappens()
    {
        _input.PlaceCursor(5, 3, 20);

        Assert.Null(Tab.Commit.Draft);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private void SetText(string text) =>
        Host.Dispatch(new CommitDraftRestored(Tab.Id, new SessionDraft(text, false, false)));

    private void Send(RepositoryCommand command) => Host.Dispatch(new TabCommand(Host.Workspace.ActiveTabId!.Value, command));
}
