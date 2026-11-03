using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class MessageEditorSelectionTests
{
    private const KeyModifiers ShiftCtrl = KeyModifiers.Shift | KeyModifiers.Ctrl;

    [Fact]
    public void GivenShiftRight_WhenApplyingTwice_ThenTheTwoElementsAreSelectedAndTheCursorIsAfterThem()
    {
        var editor = Editor("hello");
        editor.SetCursor(0, 1);

        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));

        Assert.Equal("el", editor.SelectedText);
        Assert.Equal(3, editor.CursorColumn);
        Assert.Equal(new TextSelection(new TextPosition(0, 1), new TextPosition(0, 3)), editor.Selection);
    }

    [Fact]
    public void GivenShiftLeft_WhenSelectingBackwards_ThenTheSelectionStartsAtTheCursor()
    {
        var editor = Editor("hello");

        editor.Apply(new KeyEvent(KeyNames.Left, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Left, KeyModifiers.Shift));

        Assert.Equal("lo", editor.SelectedText);
        Assert.Equal(3, editor.Selection!.Value.Start.Column);
    }

    [Fact]
    public void GivenShiftDownAndHome_WhenSelectingAcrossLines_ThenTheTextHasTheLineBreak()
    {
        var editor = Editor("abc\ndef");
        editor.SetCursor(0, 1);

        editor.Apply(new KeyEvent(KeyNames.Down, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Home, KeyModifiers.Shift));

        Assert.Equal("bc\n", editor.SelectedText);
        Assert.Equal(new TextPosition(1, 0), editor.Selection!.Value.End);
    }

    [Fact]
    public void GivenCtrlShiftArrows_WhenSelectingWords_ThenWholeWordsAreSelected()
    {
        var editor = Editor("one two three");
        editor.SetCursor(0, 0);

        editor.Apply(new KeyEvent(KeyNames.Right, ShiftCtrl));
        editor.Apply(new KeyEvent(KeyNames.Right, ShiftCtrl));

        Assert.Equal("one two", editor.SelectedText);
        editor.Apply(new KeyEvent(KeyNames.End, ShiftCtrl));
        Assert.Equal("one two three", editor.SelectedText);
    }

    [Fact]
    public void GivenAnAltShiftWordStep_WhenSelecting_ThenTheWordIsSelected()
    {
        var editor = Editor("one two");
        editor.SetCursor(0, 0);

        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Alt | KeyModifiers.Shift));

        Assert.Equal("one", editor.SelectedText);
    }

    [Fact]
    public void GivenAWideCharacter_WhenExtendingOverIt_ThenItIsNeverSplit()
    {
        var editor = Editor("a中\U0001F600b");
        editor.SetCursor(0, 1);

        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));

        Assert.Equal("中\U0001F600", editor.SelectedText);
    }

    [Fact]
    public void GivenAShiftMoveThatDoesNotMove_WhenSelecting_ThenThereIsNoSelection()
    {
        var editor = Editor("ab");

        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));

        Assert.False(editor.HasSelection);
        Assert.Equal(string.Empty, editor.SelectedText);
    }

    [Fact]
    public void GivenAnEmptiedSelection_WhenBackspacingTwice_ThenNothingBeforeTheCursorIsLeftToDelete()
    {
        var editor = Editor("abc");
        editor.SetCursor(0, 1);
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Left, KeyModifiers.Shift));

        editor.Apply(new KeyEvent(KeyNames.Backspace));
        Assert.False(editor.HasSelection);
        editor.Apply(new KeyEvent(KeyNames.Backspace));

        Assert.Equal("bc", editor.Text);
    }

    [Fact]
    public void GivenASelection_WhenTyping_ThenItIsReplaced()
    {
        var editor = Select("hello world", 0, 5);

        editor.Apply(new KeyEvent("X"));

        Assert.Equal("X world", editor.Text);
        Assert.Equal(1, editor.CursorColumn);
        Assert.False(editor.HasSelection);
    }

    [Theory]
    [InlineData(KeyNames.Backspace, KeyModifiers.None)]
    [InlineData(KeyNames.Delete, KeyModifiers.None)]
    [InlineData(KeyNames.Backspace, KeyModifiers.Ctrl)]
    [InlineData("w", KeyModifiers.Ctrl)]
    [InlineData("u", KeyModifiers.Ctrl)]
    [InlineData("k", KeyModifiers.Ctrl)]
    [InlineData("d", KeyModifiers.Alt)]
    public void GivenASelection_WhenADeleteKeyIsPressed_ThenOnlyTheSelectionIsDeleted(string key, KeyModifiers modifiers)
    {
        var editor = Select("hello world", 2, 5);

        editor.Apply(new KeyEvent(key, modifiers));

        Assert.Equal("he world", editor.Text);
        Assert.Equal(2, editor.CursorColumn);
    }

    [Fact]
    public void GivenAMultiLineSelection_WhenDeleting_ThenTheEndsJoin()
    {
        var editor = Editor("abc\ndef\nghi");
        editor.SetCursor(0, 1);
        editor.Apply(new KeyEvent(KeyNames.Down, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Down, KeyModifiers.Shift));

        editor.Apply(new KeyEvent(KeyNames.Backspace));

        Assert.Equal("ahi", editor.Text);
        Assert.Equal(new TextPosition(0, 1), new TextPosition(editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenASelection_WhenEnterOrTabIsPressed_ThenItIsReplacedByTheKey()
    {
        var editor = Select("abcdef", 1, 3);

        editor.Apply(new KeyEvent(KeyNames.Enter));
        Assert.Equal("a\ndef", editor.Text);

        var tabbed = Select("abcdef", 1, 3);
        tabbed.Apply(new KeyEvent(KeyNames.Tab));
        Assert.Equal("a   def", tabbed.Text);
    }

    [Fact]
    public void GivenASelection_WhenPasting_ThenItIsReplacedWithTheTextAndNewlines()
    {
        var editor = Select("abcdef", 1, 3);

        editor.Insert("x\r\ny");

        Assert.Equal("ax\nydef", editor.Text);
        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void GivenASelection_WhenPressingLeftOrRight_ThenItCollapsesToItsStartOrEnd()
    {
        var left = Select("abcdef", 1, 4);
        left.Apply(new KeyEvent(KeyNames.Left));
        Assert.False(left.HasSelection);
        Assert.Equal(1, left.CursorColumn);

        var right = Select("abcdef", 1, 4);
        right.Apply(new KeyEvent(KeyNames.Right));
        Assert.False(right.HasSelection);
        Assert.Equal(4, right.CursorColumn);
    }

    [Theory]
    [InlineData(KeyNames.Up)]
    [InlineData(KeyNames.Down)]
    [InlineData(KeyNames.Home)]
    [InlineData(KeyNames.End)]
    public void GivenASelection_WhenAPlainMoveKeyIsPressed_ThenTheSelectionIsDropped(string key)
    {
        var editor = Editor("abc\ndef");
        editor.SetCursor(0, 1);
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));

        editor.Apply(new KeyEvent(key));

        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void GivenASelection_WhenExtendedAgainWithShift_ThenItKeepsItsAnchorEvenWhenCrossingIt()
    {
        var editor = Select("abcdef", 2, 4);

        editor.Apply(new KeyEvent(KeyNames.Left, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Left, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Left, KeyModifiers.Shift));

        Assert.Equal("b", editor.SelectedText);
        Assert.Equal(1, editor.CursorColumn);
    }

    [Fact]
    public void GivenAPointer_WhenExtendingToIt_ThenTheSelectionRunsFromTheCursorToThePointer()
    {
        var editor = Editor("hello\nworld");
        editor.PlaceCursor(0, 2);

        editor.ExtendTo(1, 3);
        Assert.Equal("llo\nwor", editor.SelectedText);

        editor.ExtendTo(0, 0);
        Assert.Equal("he", editor.SelectedText);
    }

    [Fact]
    public void GivenAClick_WhenPlacingTheCursor_ThenTheSelectionIsDropped()
    {
        var editor = Select("hello", 0, 3);

        editor.PlaceCursor(0, 4);

        Assert.False(editor.HasSelection);
    }

    [Theory]
    [InlineData("foo bar", 1, "foo")]
    [InlineData("foo bar", 3, " ")]
    [InlineData("foo  bar", 4, "  ")]
    [InlineData("foo.bar", 3, ".")]
    [InlineData("foo bar", 7, "bar")]
    [InlineData("naïve_x y", 2, "naïve_x")]
    public void GivenAColumn_WhenSelectingTheWord_ThenTheRunAroundItIsSelected(string text, int column, string wanted)
    {
        var editor = Editor(text);
        editor.SetCursor(0, column);

        editor.SelectWordAtCursor();

        Assert.Equal(wanted, editor.SelectedText);
    }

    [Fact]
    public void GivenAnEmptyLine_WhenSelectingTheWord_ThenNothingIsSelected()
    {
        var editor = Editor("a\n\nb");
        editor.SetCursor(1, 0);

        editor.SelectWordAtCursor();

        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void GivenASelection_WhenDeletingIt_ThenUndoBringsItBackWithoutASelection()
    {
        var editor = Select("hello world", 0, 5);
        editor.MarkUndoPoint();

        editor.Apply(new KeyEvent(KeyNames.Backspace));
        Assert.Equal(" world", editor.Text);
        Assert.True(editor.Undo());

        Assert.Equal("hello world", editor.Text);
        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void GivenAnUndoneEdit_WhenRedoing_ThenTheEditReturnsAndUndoWorksAgain()
    {
        var editor = Select("hello world", 0, 5);
        editor.MarkUndoPoint();
        editor.Apply(new KeyEvent("X"));
        editor.Undo();

        Assert.True(editor.Redo());
        Assert.Equal("X world", editor.Text);
        Assert.Equal(1, editor.CursorColumn);
        Assert.True(editor.Undo());
        Assert.Equal("hello world", editor.Text);
    }

    [Fact]
    public void GivenAPaste_WhenUndoingAndRedoing_ThenTheWholePasteComesAndGoes()
    {
        var editor = Editor("ab");
        editor.SetCursor(0, 1);
        editor.MarkUndoPoint();
        editor.Insert("one\ntwo\tthree");

        Assert.True(editor.Undo());
        Assert.Equal("ab", editor.Text);
        Assert.True(editor.Redo());
        Assert.Equal("aone\ntwo\tthreeb", editor.Text);
    }

    [Fact]
    public void GivenSeveralUndos_WhenRedoingThemAll_ThenEachStepReturnsInOrder()
    {
        var editor = Editor(string.Empty);
        foreach (var letter in new[] { "a", "b", "c" })
        {
            editor.MarkUndoPoint();
            editor.Apply(new KeyEvent(letter));
        }

        editor.Undo();
        editor.Undo();
        editor.Undo();
        Assert.Equal(string.Empty, editor.Text);

        editor.Redo();
        Assert.Equal("a", editor.Text);
        editor.Redo();
        Assert.Equal("ab", editor.Text);
        editor.Redo();
        Assert.Equal("abc", editor.Text);
        Assert.False(editor.Redo());
    }

    [Fact]
    public void GivenAnUndoFollowedByNewTyping_WhenRedoing_ThenNothingIsRestored()
    {
        var editor = Editor("a");
        editor.MarkUndoPoint();
        editor.Apply(new KeyEvent("b"));
        editor.Undo();
        editor.Apply(new KeyEvent("z"));

        Assert.False(editor.Redo());
        Assert.Equal("az", editor.Text);
    }

    [Fact]
    public void GivenAnUndoFollowedByANewUndoPoint_WhenRedoing_ThenNothingIsRestored()
    {
        var editor = Editor("a");
        editor.MarkUndoPoint();
        editor.Apply(new KeyEvent("b"));
        editor.Undo();
        editor.MarkUndoPoint();

        Assert.False(editor.Redo());
    }

    [Fact]
    public void GivenNothingUndone_WhenRedoing_ThenItReturnsFalse()
    {
        Assert.False(Editor("a").Redo());
    }

    [Fact]
    public void GivenASelectionAndAnUndoStack_WhenCloned_ThenTheCopyKeepsBoth()
    {
        var editor = Select("hello", 1, 3);
        editor.MarkUndoPoint();
        editor.Apply(new KeyEvent("x"));
        editor.Undo();
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));

        var copy = editor.Clone();

        Assert.Equal(editor.SelectedText, copy.SelectedText);
        Assert.True(copy.HasSelection);
    }

    [Fact]
    public void GivenASelection_WhenReplacingTheText_ThenTheSelectionIsGone()
    {
        var editor = Select("hello", 1, 3);

        editor.SetText("new");

        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void GivenASelectionInLines_WhenAskingItsRangeOfEachLine_ThenItCoversTheEdgesAndTheLineBreaks()
    {
        var editor = Editor("abc\ndef\nghi");
        editor.SetCursor(0, 1);
        editor.Apply(new KeyEvent(KeyNames.Down, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Down, KeyModifiers.Shift));
        var selection = editor.Selection!.Value;

        Assert.Equal(new ColumnRange(1, 3), selection.InLine(0, 3));
        Assert.Equal(new ColumnRange(0, 3), selection.InLine(1, 3));
        Assert.Equal(new ColumnRange(0, 1), selection.InLine(2, 3));
        Assert.True(selection.IncludesLineBreak(0));
        Assert.True(selection.IncludesLineBreak(1));
        Assert.False(selection.IncludesLineBreak(2));
        Assert.Null(selection.InLine(3, 0));
    }

    private static MessageEditor Editor(string text)
    {
        var editor = new MessageEditor();
        editor.SetText(text);

        return editor;
    }

    // An editor with the first line selected from one column to another.
    private static MessageEditor Select(string text, int from, int to)
    {
        var editor = Editor(text);
        editor.SetCursor(0, from);
        for (var step = from; step < to; step++)
        {
            editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));
        }

        return editor;
    }
}
