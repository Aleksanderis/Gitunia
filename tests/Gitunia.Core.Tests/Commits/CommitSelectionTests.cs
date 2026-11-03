using Gitunia.Core.Clipboard;
using Gitunia.Core.Commits;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Commits;

public class CommitSelectionTests
{
    private static readonly KeyEvent ShiftRight = new(KeyNames.Right, KeyModifiers.Shift);

    [Fact]
    public void GivenASelection_WhenCopying_ThenTheClipboardGetsItAndTheTextStays()
    {
        var harness = Selected("hello world", 6);

        harness.Press(new KeyEvent("w", KeyModifiers.Alt));

        var copy = harness.Take<CopyToClipboardEffect>();
        Assert.Equal("world", copy.Text);
        Assert.Equal("hello world", harness.Draft.Editor.Text);
        Assert.True(harness.Draft.Editor.HasSelection);
    }

    [Fact]
    public void GivenASelection_WhenCutting_ThenTheClipboardGetsItAndTheTextIsRemoved()
    {
        var harness = Selected("hello world", 6);

        harness.Press(new KeyEvent("x", KeyModifiers.Ctrl));

        Assert.Equal("world", harness.Take<CopyToClipboardEffect>().Text);
        Assert.Equal("hello ", harness.Draft.Editor.Text);
        Assert.False(harness.Draft.Editor.HasSelection);
    }

    [Fact]
    public void GivenNoSelection_WhenCopyingOrCutting_ThenNothingIsCopiedAndTheTextStays()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("hello");

        harness.Press(new KeyEvent("w", KeyModifiers.Alt));
        harness.Press(new KeyEvent("x", KeyModifiers.Ctrl));

        Assert.Empty(harness.Effects.OfType<CopyToClipboardEffect>());
        Assert.Equal("hello", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAnInsertSession_WhenUndoingAndRedoingWithCtrlKeys_ThenTheSessionComesAndGoes()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("hello");

        harness.Press(new KeyEvent("z", KeyModifiers.Ctrl));
        Assert.Equal(string.Empty, harness.Draft.Editor.Text);
        harness.Press(new KeyEvent("y", KeyModifiers.Ctrl));

        Assert.Equal("hello", harness.Draft.Editor.Text);
        Assert.True(harness.IsEditing);
    }

    [Fact]
    public void GivenNothingToRedo_WhenPressingCtrlY_ThenTheStatusSaysSo()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("hello");

        harness.Press(new KeyEvent("y", KeyModifiers.Ctrl));

        Assert.Equal(CommitTexts.NothingToRedoNotice, harness.LastStatus);
        Assert.Equal("hello", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAnUndoInNormalMode_WhenRedoing_ThenTheChangeReturns()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("first");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeMessageUndo);
        Assert.Equal(string.Empty, harness.Draft.Editor.Text);

        harness.Run(ActionIds.WorkingTreeMessageRedo);

        Assert.Equal("first", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenNothingToRedoInNormalMode_WhenRedoing_ThenTheStatusSaysSo()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Send(new TabCommand(harness.TabId, new FocusPaneCommand(PaneKind.WorkingMessage)));

        harness.Run(ActionIds.WorkingTreeMessageRedo);

        Assert.Equal(CommitTexts.NothingToRedoNotice, harness.LastStatus);
    }

    [Fact]
    public void GivenASelection_WhenEscLeavesInsertMode_ThenTheSelectionIsGone()
    {
        var harness = Selected("hello world", 6);

        harness.Run(ActionIds.Cancel);

        Assert.False(harness.Draft.Editor.HasSelection);
        Assert.Equal("hello world", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAClickThenADrag_WhenTheDragReachesAnotherLine_ThenTheTextBetweenIsSelected()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("hello");
        harness.Run(ActionIds.WorkingTreeMessageNewLine);
        harness.Type("world");
        harness.Send(new CommitCursorPlaced(harness.TabId, 0, 2));

        harness.Send(new CommitSelectionDragged(harness.TabId, 1, 3));

        Assert.Equal("llo\nwor", harness.Draft.Editor.SelectedText);
    }

    [Fact]
    public void GivenADragWhileACommitRuns_WhenReducing_ThenTheSelectionIsUnchanged()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("hello");
        harness.Mark(RunningOperationKind.Commit);

        harness.Send(new CommitSelectionDragged(harness.TabId, 0, 1));

        Assert.False(harness.Draft.Editor.HasSelection);
    }

    [Fact]
    public void GivenAWord_WhenDoubleClickSelectsIt_ThenTheWordIsSelectedWithoutLeavingInsertMode()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("hello world");
        harness.Send(new CommitCursorPlaced(harness.TabId, 0, 8));

        Assert.True(harness.Run(ActionIds.WorkingTreeMessageSelectWord));

        Assert.Equal("world", harness.Draft.Editor.SelectedText);
        Assert.True(harness.IsEditing);
    }

    [Fact]
    public void GivenALongMessage_WhenTheWheelScrolls_ThenTheCursorMovesThreeLinesInNormalMode()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("1\n2\n3\n4\n5\n6\n7");
        harness.Send(new TabCommand(harness.TabId, new FocusPaneCommand(PaneKind.WorkingMessage)));
        harness.Send(new CommitCursorPlaced(harness.TabId, 0, 0));

        Assert.True(harness.Run(ActionIds.WorkingTreeMessageScrollDown));
        Assert.Equal(3, harness.Draft.Editor.CursorRow);
        harness.Run(ActionIds.WorkingTreeMessageScrollDown);
        harness.Run(ActionIds.WorkingTreeMessageScrollDown);
        Assert.Equal(6, harness.Draft.Editor.CursorRow);
        harness.Run(ActionIds.WorkingTreeMessageScrollUp);
        Assert.Equal(3, harness.Draft.Editor.CursorRow);
    }

    // A box in Insert mode holding the text with the end selected from a column.
    private static CommitHarness Selected(string text, int from)
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type(text);
        harness.Send(new CommitCursorPlaced(harness.TabId, 0, from));
        while (harness.Draft.Editor.CursorColumn < text.Length)
        {
            harness.Press(ShiftRight);
        }

        return harness;
    }
}
