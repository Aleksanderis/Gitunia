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
    public void GivenOnlyWhitespace_WhenCheckingBlank_ThenItIsBlank()
    {
        Assert.True(Editor(" \n\t").IsBlank);
        Assert.False(Editor(" \nx").IsBlank);
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
