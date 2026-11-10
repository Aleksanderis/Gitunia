using Gitunia.Core.Conflicts;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts;
using Xunit;

namespace Gitunia.Core.Tests.Conflicts;

[Trait("Os", "Independent")]
public class ConflictEditorClipboardTests
{
    private const string Single = "top\n<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nend\n";

    private const string LongHunk = "top\n<<<<<<< HEAD\na\nb\nc\n=======\nx\ny\n>>>>>>> other\nend\n";

    [Fact]
    public void GivenALineOfTheResult_WhenCopyingAndPuttingBelow_ThenTheLineIsDuplicatedAndUndoRemovesIt()
    {
        var state = State(Single).FocusPane(ConflictPane.Result).MoveToTop();

        var copied = state.Yanked(state.YankLine()!);
        var put = copied.Put(after: true);

        Assert.Equal("top\ntop\n<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nend\n", put.ToText());
        Assert.Equal(1, put.Result.Editor.CursorRow);
        Assert.Equal(1, put.UnresolvedCount);
        Assert.Equal(Single, put.Undo().ToText());
    }

    [Fact]
    public void GivenADeletedLine_WhenPuttingAbove_ThenTheLineComesBackWhereTheCursorIs()
    {
        var state = State(Single).FocusPane(ConflictPane.Result).MoveToTop();

        var deleted = state.ApplyResultCommand(ConflictResultCommand.DeleteLine);
        var put = deleted.Put(after: false);

        Assert.Equal(new ConflictYank("top", true), deleted.Yank);
        Assert.Equal(Single, put.ToText());
        Assert.Equal(0, put.Result.Editor.CursorRow);
    }

    [Fact]
    public void GivenNothingCopiedYet_WhenPutting_ThenTheStateIsUnchanged()
    {
        var state = State(Single).FocusPane(ConflictPane.Result);

        Assert.Same(state, state.Put(after: true));
    }

    [Fact]
    public void GivenTheCursorInAVersionPane_WhenPutting_ThenTheStateIsUnchanged()
    {
        var state = State(Single).Yanked(new ConflictYank("x", true));

        Assert.Same(state, state.Put(after: true));
    }

    [Fact]
    public void GivenAVisualSelectionInAVersionPane_WhenYankingRows_ThenTheLinesOfThatPaneAreCopied()
    {
        var state = State(LongHunk).ToggleVisual().MoveCursor(1);

        var yank = state.YankSelectedRows();

        Assert.Equal(new ConflictYank("a\nb", true), yank);
    }

    [Fact]
    public void GivenAFillerRowOfAShorterVersion_WhenYankingTheLine_ThenNothingIsCopied()
    {
        var state = State(LongHunk).FocusPane(ConflictPane.Theirs).MoveCursor(2);

        Assert.Null(state.YankLine());
    }

    [Fact]
    public void GivenACharacterRangeSelectedInTheResult_WhenYanking_ThenExactlyThoseCharactersAreCopied()
    {
        var state = State(Single).FocusPane(ConflictPane.Result).MoveToTop().ClickResultLine(0);

        var selected = state.ExtendResultSelection(0, 2);

        Assert.Equal(new ConflictYank("to", false), selected.YankResultSelection());
    }

    [Fact]
    public void GivenTypingWithASelection_WhenCutting_ThenTheSelectionLeavesTheResultAsOneUndoStep()
    {
        var typing = State(Single).FocusPane(ConflictPane.Result).MoveToTop().BeginEdit();
        var selected = typing.ExtendResultSelection(0, 2);

        var cut = selected.CutSelection();

        Assert.Equal("p\n<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nend\n", cut.ToText());
        Assert.True(cut.IsEditing);
        Assert.Equal(Single, cut.EndEdit().Undo().ToText());
    }

    [Fact]
    public void GivenAPasteInNormalMode_WhenPasting_ThenTheTextIsInsertedAtTheCursorWithLineBreaksAndUndoTakesItBack()
    {
        var state = State(Single).FocusPane(ConflictPane.Result).MoveToTop();

        var pasted = state.Paste("x\ny");

        Assert.Equal("x\nytop\n<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nend\n", pasted.ToText());
        Assert.Equal(1, pasted.UnresolvedCount);
        Assert.Equal(Single, pasted.Undo().ToText());
    }

    [Fact]
    public void GivenTypingOrAVersionPane_WhenPasting_ThenTheStateIsUnchanged()
    {
        var version = State(Single);
        var typing = version.FocusPane(ConflictPane.Result).BeginEdit();

        Assert.Same(version, version.Paste("x"));
        Assert.Same(typing, typing.Paste("x"));
    }

    private static ConflictEditorState State(string text) =>
        ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(text).Parts!);
}
