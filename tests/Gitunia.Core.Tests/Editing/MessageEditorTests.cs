using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

public class MessageEditorTests
{
    private const string Emoji = "\U0001F600";
    private const string Wide = "中";
    private const string Combined = "é";

    [Fact]
    public void GivenTypedKeysAndEnter_WhenApplying_ThenTextHasSubjectBlankLineAndBody()
    {
        var editor = Type("fix: bug", KeyNames.Enter, KeyNames.Enter, "why");

        Assert.Equal("fix: bug\n\nwhy", editor.Text);
        Assert.Equal(2, editor.CursorRow);
        Assert.Equal(3, editor.CursorColumn);
    }

    [Fact]
    public void GivenBackspace_WhenAtLineStart_ThenLinesJoinAndCursorStaysAtTheJoin()
    {
        var editor = Editor("ab\ncd");
        editor.Apply(new KeyEvent(KeyNames.Home));

        editor.Apply(new KeyEvent(KeyNames.Backspace));

        Assert.Equal("abcd", editor.Text);
        Assert.Equal(0, editor.CursorRow);
        Assert.Equal(2, editor.CursorColumn);
    }

    [Fact]
    public void GivenWideEmojiAndCombiningText_WhenBackspacing_ThenWholeElementsGo()
    {
        var editor = Editor($"a{Wide}{Emoji}{Combined}");

        editor.Apply(new KeyEvent(KeyNames.Backspace));
        Assert.Equal($"a{Wide}{Emoji}", editor.Text);
        editor.Apply(new KeyEvent(KeyNames.Backspace));
        Assert.Equal($"a{Wide}", editor.Text);
        editor.Apply(new KeyEvent(KeyNames.Backspace));
        Assert.Equal("a", editor.Text);
    }

    [Fact]
    public void GivenWideCharacters_WhenMovingLeftAndRight_ThenCursorNeverSplitsAnElement()
    {
        var editor = Editor($"{Emoji}{Combined}");

        editor.Apply(new KeyEvent(KeyNames.Left));
        Assert.Equal(Emoji.Length, editor.CursorColumn);
        editor.Apply(new KeyEvent(KeyNames.Left));
        Assert.Equal(0, editor.CursorColumn);
        editor.Apply(new KeyEvent(KeyNames.Right));
        Assert.Equal(Emoji.Length, editor.CursorColumn);
        editor.Apply(new KeyEvent(KeyNames.Delete));
        Assert.Equal(Emoji, editor.Text);
    }

    [Fact]
    public void GivenCursorMotion_WhenCrossingLines_ThenLeftRightWrapAndUpDownKeepTheColumn()
    {
        var editor = Editor("abcd\nx\nabcd");

        editor.Apply(new KeyEvent(KeyNames.Up));
        Assert.Equal((1, 1), (editor.CursorRow, editor.CursorColumn));
        editor.Apply(new KeyEvent(KeyNames.Up));
        Assert.Equal((0, 4), (editor.CursorRow, editor.CursorColumn));
        editor.Apply(new KeyEvent(KeyNames.Right));
        Assert.Equal((1, 0), (editor.CursorRow, editor.CursorColumn));
        editor.Apply(new KeyEvent(KeyNames.Left));
        Assert.Equal((0, 4), (editor.CursorRow, editor.CursorColumn));
        editor.Apply(new KeyEvent(KeyNames.Down));
        editor.Apply(new KeyEvent(KeyNames.Down));
        Assert.Equal((2, 4), (editor.CursorRow, editor.CursorColumn));
        editor.Apply(new KeyEvent(KeyNames.Down));
        Assert.Equal(2, editor.CursorRow);
    }

    [Fact]
    public void GivenHomeEndAndCtrlKeys_WhenApplying_ThenTheyMoveAndDelete()
    {
        var editor = Editor("one two three");

        editor.Apply(new KeyEvent("w", KeyModifiers.Ctrl));
        Assert.Equal("one two ", editor.Text);
        editor.Apply(new KeyEvent("a", KeyModifiers.Ctrl));
        editor.Apply(new KeyEvent("k", KeyModifiers.Ctrl));
        Assert.Equal(string.Empty, editor.Text);
        editor.SetText("abc def");
        editor.Apply(new KeyEvent("u", KeyModifiers.Ctrl));
        Assert.Equal(string.Empty, editor.Text);
    }

    [Fact]
    public void GivenDeleteAtLineEnd_WhenApplying_ThenNextLineJoins()
    {
        var editor = Editor("ab\ncd");
        editor.Apply(new KeyEvent(KeyNames.Up));
        editor.Apply(new KeyEvent(KeyNames.End));

        editor.Apply(new KeyEvent(KeyNames.Delete));

        Assert.Equal("abcd", editor.Text);
    }

    [Fact]
    public void GivenPastedText_WhenInserting_ThenLineEndingsNormalizeAndControlsDrop()
    {
        var editor = Editor("a|z");
        editor.Apply(new KeyEvent(KeyNames.Left));
        editor.Apply(new KeyEvent(KeyNames.Left));

        editor.Insert("1\r\n2\r3\u001b4");

        Assert.Equal("a1\n2\n34|z", editor.Text);
        Assert.Equal((2, 2), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenKeysTheCallerOwns_WhenApplying_ThenEditorDeclinesAndKeepsTheText()
    {
        var editor = Editor("text");

        Assert.False(editor.Apply(new KeyEvent(KeyNames.Enter, KeyModifiers.Alt)));
        Assert.False(editor.Apply(new KeyEvent("g", KeyModifiers.Ctrl)));
        Assert.False(editor.Apply(new KeyEvent("s", KeyModifiers.Alt)));
        Assert.False(editor.Apply(new KeyEvent(KeyNames.Escape)));
        Assert.Equal("text", editor.Text);
    }

    [Fact]
    public void GivenEdgesOfTheText_WhenDeletingOrInsertingNothing_ThenNothingChanges()
    {
        var editor = Editor("ab");
        editor.Apply(new KeyEvent("a", KeyModifiers.Ctrl));
        editor.Apply(new KeyEvent(KeyNames.Backspace));
        editor.Apply(new KeyEvent("w", KeyModifiers.Ctrl));
        editor.Insert(string.Empty);
        editor.Apply(new KeyEvent("e", KeyModifiers.Ctrl));
        editor.Apply(new KeyEvent(KeyNames.Delete));

        Assert.Equal("ab", editor.Text);
    }

    [Fact]
    public void GivenCtrlWAtLineStart_WhenApplying_ThenLinesJoin()
    {
        var editor = Editor("ab\ncd");
        editor.Apply(new KeyEvent(KeyNames.Home));

        editor.Apply(new KeyEvent("w", KeyModifiers.Ctrl));

        Assert.Equal("abcd", editor.Text);
    }

    [Fact]
    public void GivenShiftedEnter_WhenApplying_ThenItIsANewLine()
    {
        var editor = Editor("a");

        Assert.True(editor.Apply(new KeyEvent(KeyNames.Enter, KeyModifiers.Shift)));
        Assert.Equal("a\n", editor.Text);
    }

    [Fact]
    public void GivenWideCharacters_WhenMovingUpAndDown_ThenTheDisplayColumnIsKept()
    {
        var editor = Editor($"{Wide}{Wide}{Wide}\nabcdef");

        editor.Apply(new KeyEvent(KeyNames.Up));
        Assert.Equal((0, 3), (editor.CursorRow, editor.CursorColumn));
        editor.Apply(new KeyEvent(KeyNames.Down));
        Assert.Equal((1, 6), (editor.CursorRow, editor.CursorColumn));
        editor.Apply(new KeyEvent(KeyNames.Left));
        editor.Apply(new KeyEvent(KeyNames.Left));
        editor.Apply(new KeyEvent(KeyNames.Left));
        Assert.Equal(3, editor.CursorDisplayColumn);

        editor.Apply(new KeyEvent(KeyNames.Up));

        // Column 3 falls inside the second wide character, so the cursor stays before it.
        Assert.Equal((0, 1), (editor.CursorRow, editor.CursorColumn));
        editor.Apply(new KeyEvent(KeyNames.Down));
        Assert.Equal((1, 3), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenALongerLineBelow_WhenMovingDown_ThenTheCursorClampsAndTheWantedColumnReturns()
    {
        var editor = Editor($"abcdef\n{Wide}\nabcdef");
        editor.Apply(new KeyEvent(KeyNames.Up));
        editor.Apply(new KeyEvent(KeyNames.Up));
        editor.Apply(new KeyEvent(KeyNames.End));
        editor.Apply(new KeyEvent(KeyNames.Down));
        Assert.Equal(Wide.Length, editor.CursorColumn);

        editor.Apply(new KeyEvent(KeyNames.Down));

        Assert.Equal((2, 6), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenTab_WhenInInsertMode_ThenSpacesFillToTheNextFourColumnStop()
    {
        var editor = Editor("ab");

        Assert.True(editor.Apply(new KeyEvent(KeyNames.Tab)));
        Assert.Equal("ab  ", editor.Text);
        editor.Apply(new KeyEvent(KeyNames.Tab));
        Assert.Equal("ab      ", editor.Text);
        editor.SetText($"{Wide}{Wide}");
        editor.Apply(new KeyEvent(KeyNames.Tab));
        Assert.Equal($"{Wide}{Wide}    ", editor.Text);
        Assert.False(editor.Apply(new KeyEvent(KeyNames.Tab, KeyModifiers.Shift)));
    }

    [Fact]
    public void GivenAClick_WhenPlacingTheCursor_ThenItLandsOnTheElementAtTheDisplayColumn()
    {
        var editor = Editor($"a{Wide}b\nxy");

        editor.PlaceCursor(0, 2);
        Assert.Equal((0, 1), (editor.CursorRow, editor.CursorColumn));
        editor.PlaceCursor(0, 3);
        Assert.Equal((0, 2), (editor.CursorRow, editor.CursorColumn));
        editor.PlaceCursor(0, 40);
        Assert.Equal((0, 3), (editor.CursorRow, editor.CursorColumn));
        editor.PlaceCursor(9, 1);
        Assert.Equal((1, 1), (editor.CursorRow, editor.CursorColumn));
        editor.PlaceCursor(-3, -2);
        Assert.Equal((0, 0), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenAClick_WhenMovingVerticallyAfterwards_ThenTheClickedColumnIsKept()
    {
        var editor = Editor("abcdef\nabcdef");

        editor.PlaceCursor(0, 2);
        editor.Apply(new KeyEvent(KeyNames.Down));

        Assert.Equal((1, 2), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenOnlyWhitespace_WhenCheckingBlank_ThenItIsBlank()
    {
        Assert.True(Editor(" \n\t").IsBlank);
        Assert.False(Editor(" \nx").IsBlank);
    }

    [Fact]
    public void GivenNormalModeMotions_WhenMovingByLettersAndLines_ThenTheCursorStaysInsideTheText()
    {
        var editor = Editor("ab\ncdef");
        editor.PlaceCursor(0, 0);

        editor.MoveLeftInLine();
        editor.MoveRightInLine();
        editor.MoveRightInLine();
        editor.MoveRightInLine();
        var afterRight = (editor.CursorRow, editor.CursorColumn);
        editor.MoveLines(1);
        editor.MoveToLineEnd();
        editor.MoveLines(1);

        Assert.Equal((0, 2), afterRight);
        Assert.Equal((1, 4), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenWords_WhenMovingForwardAndBackward_ThenTheCursorCrossesLines()
    {
        var editor = Editor("one two\n  three");
        editor.PlaceCursor(0, 0);

        editor.MoveWordForward();
        var second = (editor.CursorRow, editor.CursorColumn);
        editor.MoveWordForward();
        var third = (editor.CursorRow, editor.CursorColumn);
        editor.MoveWordBackward();
        editor.MoveWordBackward();
        editor.MoveWordBackward();

        Assert.Equal((0, 4), second);
        Assert.Equal((1, 2), third);
        Assert.Equal((0, 0), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenDeleteKeys_WhenDeletingACharacterAndALine_ThenTheTextShrinksAndTheLastLineIsEmptied()
    {
        var editor = Editor("abc\ndef");
        editor.PlaceCursor(0, 1);

        editor.DeleteUnderCursor();
        editor.DeleteLine();
        editor.DeleteLine();

        Assert.Equal(string.Empty, editor.Text);
        Assert.Equal((0, 0), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenAnUndoPoint_WhenTheTextIsUnchanged_ThenDroppingItLeavesNothingToUndo()
    {
        var editor = Editor("abc");
        editor.MarkUndoPoint();

        editor.DropUndoPointIfUnchanged();

        Assert.False(editor.Undo());
    }

    [Fact]
    public void GivenAnUndoPoint_WhenTheTextChanged_ThenDroppingKeepsItAndUndoRestores()
    {
        var editor = Editor("abc");
        editor.MarkUndoPoint();
        editor.Insert("d");

        editor.DropUndoPointIfUnchanged();

        Assert.True(editor.Undo());
        Assert.Equal("abc", editor.Text);
    }

    [Fact]
    public void GivenNoUndoPoint_WhenDropping_ThenNothingHappens()
    {
        var editor = Editor("abc");

        editor.DropUndoPointIfUnchanged();

        Assert.Equal("abc", editor.Text);
    }

    [Fact]
    public void GivenOpenLineBelow_WhenUndoing_ThenTheTextAndCursorComeBack()
    {
        var editor = Editor("abc");
        editor.PlaceCursor(0, 1);
        editor.MarkUndoPoint();
        editor.OpenLineBelow();
        editor.Insert("x");

        var undone = editor.Undo();
        var nothingLeft = editor.Undo();

        Assert.True(undone);
        Assert.False(nothingLeft);
        Assert.Equal("abc", editor.Text);
        Assert.Equal((0, 1), (editor.CursorRow, editor.CursorColumn));
    }

    [Fact]
    public void GivenAWideCharacter_WhenMovingRightAndDeleting_ThenItIsNeverSplit()
    {
        var editor = Editor($"{Wide}b");
        editor.PlaceCursor(0, 0);

        editor.MoveRightInLine();
        editor.MoveLeftInLine();
        editor.DeleteUnderCursor();

        Assert.Equal("b", editor.Text);
    }

    [Fact]
    public void GivenUndoPoints_WhenTheTextIsReplaced_ThenNothingCanBeUndone()
    {
        var editor = Editor("old");
        editor.MarkUndoPoint();

        editor.SetText("new");

        Assert.False(editor.Undo());
        Assert.Equal("new", editor.Text);
    }

    private static MessageEditor Editor(string text)
    {
        var editor = new MessageEditor();
        editor.SetText(text);

        return editor;
    }

    private static MessageEditor Type(params string[] keys)
    {
        var editor = new MessageEditor();
        foreach (var key in keys)
        {
            if (KeyNames.IsNamed(key))
            {
                editor.Apply(new KeyEvent(key));
                continue;
            }

            foreach (var character in key)
            {
                editor.Apply(new KeyEvent(character.ToString()));
            }
        }

        return editor;
    }
}
