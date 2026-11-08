using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class PromptTextEditorTests
{
    private const string Smile = "\U0001F600";

    [Fact]
    public void GivenPrintableKeyInTheMiddle_WhenApplying_ThenItIsInsertedAtTheCaret()
    {
        var edit = PromptTextEditor.Apply("fix", 1, new KeyEvent(":"));

        Assert.Equal(new PromptEdit("f:ix", 2), edit);
    }

    [Fact]
    public void GivenCaretAtEnd_WhenTyping_ThenTheTextIsAppended()
    {
        Assert.Equal(new PromptEdit("fix:", 4), PromptTextEditor.Apply("fix", PromptCaret.End, new KeyEvent(":")));
        Assert.Equal(new PromptEdit("a", 1), PromptTextEditor.Apply(string.Empty, 0, new KeyEvent("a")));
    }

    [Fact]
    public void GivenBackspaceAndDelete_WhenApplying_ThenTheElementBeforeOrAfterTheCaretGoes()
    {
        Assert.Equal(new PromptEdit("fx", 1), PromptTextEditor.Apply("fix", 2, new KeyEvent(KeyNames.Backspace)));
        Assert.Equal(new PromptEdit("fx", 1), PromptTextEditor.Apply("fix", 1, new KeyEvent(KeyNames.Delete)));
        Assert.Equal(new PromptEdit("a", 1), PromptTextEditor.Apply($"a{Smile}", PromptCaret.End, new KeyEvent(KeyNames.Backspace)));
        Assert.Equal(new PromptEdit("a", 1), PromptTextEditor.Apply($"a{Smile}", 1, new KeyEvent(KeyNames.Delete)));
    }

    [Fact]
    public void GivenCaretAtTheEdges_WhenDeletingOutward_ThenNothingChanges()
    {
        Assert.Null(PromptTextEditor.Apply("fix", 0, new KeyEvent(KeyNames.Backspace)));
        Assert.Null(PromptTextEditor.Apply("fix", PromptCaret.End, new KeyEvent(KeyNames.Delete)));
        Assert.Null(PromptTextEditor.Apply(string.Empty, 0, new KeyEvent(KeyNames.Backspace)));
        Assert.Null(PromptTextEditor.Apply(string.Empty, 0, new KeyEvent(KeyNames.Left)));
        Assert.Null(PromptTextEditor.Apply(string.Empty, 0, new KeyEvent(KeyNames.Right)));
    }

    [Fact]
    public void GivenArrowsAndHomeEnd_WhenApplying_ThenTheCaretMovesByTextElement()
    {
        var text = $"a{Smile}b";

        Assert.Equal(new PromptEdit(text, 1), PromptTextEditor.Apply(text, 3, new KeyEvent(KeyNames.Left)));
        Assert.Equal(new PromptEdit(text, 0), PromptTextEditor.Apply(text, 1, new KeyEvent(KeyNames.Left)));
        Assert.Equal(new PromptEdit(text, 3), PromptTextEditor.Apply(text, 1, new KeyEvent(KeyNames.Right)));
        Assert.Equal(new PromptEdit(text, 0), PromptTextEditor.Apply(text, 3, new KeyEvent(KeyNames.Home)));
        Assert.Equal(new PromptEdit(text, 4), PromptTextEditor.Apply(text, 0, new KeyEvent(KeyNames.End)));
        Assert.Equal(new PromptEdit(text, 0), PromptTextEditor.Apply(text, 3, new KeyEvent("a", KeyModifiers.Ctrl)));
        Assert.Equal(new PromptEdit(text, 4), PromptTextEditor.Apply(text, 0, new KeyEvent("e", KeyModifiers.Ctrl)));
    }

    [Fact]
    public void GivenCaretInsideASurrogatePair_WhenApplying_ThenItSnapsToTheElementStart()
    {
        var edit = PromptTextEditor.Apply($"a{Smile}", 2, new KeyEvent("x"));

        Assert.Equal(new PromptEdit($"ax{Smile}", 2), edit);
    }

    [Fact]
    public void GivenWordKeys_WhenApplying_ThenTheCaretAndDeletionFollowTheWordRules()
    {
        const string text = "foo.bar baz";

        Assert.Equal(new PromptEdit(text, 8), PromptTextEditor.Apply(text, PromptCaret.End, new KeyEvent("Left", KeyModifiers.Ctrl)));
        Assert.Equal(new PromptEdit(text, 3), PromptTextEditor.Apply(text, 0, new KeyEvent("Right", KeyModifiers.Ctrl)));
        Assert.Equal(new PromptEdit(text, 4), PromptTextEditor.Apply(text, 8, new KeyEvent("b", KeyModifiers.Alt)));
        Assert.Equal(new PromptEdit(text, 7), PromptTextEditor.Apply(text, 4, new KeyEvent("f", KeyModifiers.Alt)));
        Assert.Equal(new PromptEdit("foo.bar ", 8), PromptTextEditor.Apply(text, PromptCaret.End, new KeyEvent("w", KeyModifiers.Ctrl)));
        Assert.Equal(new PromptEdit("foo. baz", 4), PromptTextEditor.Apply(text, 4, new KeyEvent("d", KeyModifiers.Alt)));
    }

    [Fact]
    public void GivenCtrlUAndCtrlK_WhenApplying_ThenTheTextBeforeOrAfterTheCaretGoes()
    {
        Assert.Equal(new PromptEdit("bug", 0), PromptTextEditor.Apply("fix bug", 4, new KeyEvent("u", KeyModifiers.Ctrl)));
        Assert.Equal(new PromptEdit("fix ", 4), PromptTextEditor.Apply("fix bug", 4, new KeyEvent("k", KeyModifiers.Ctrl)));
        Assert.Null(PromptTextEditor.Apply(string.Empty, 0, new KeyEvent("u", KeyModifiers.Ctrl)));
        Assert.Null(PromptTextEditor.Apply("fix", PromptCaret.End, new KeyEvent("k", KeyModifiers.Ctrl)));
    }

    [Fact]
    public void GivenOtherKeys_WhenApplying_ThenTheyDoNotEdit()
    {
        Assert.Null(PromptTextEditor.Apply("fix", 1, new KeyEvent(KeyNames.Up)));
        Assert.Null(PromptTextEditor.Apply("fix", 1, new KeyEvent("x", KeyModifiers.Alt)));
    }

    [Fact]
    public void GivenCaretPastTheText_WhenClamping_ThenItIsTheTextLength()
    {
        Assert.Equal(3, PromptCaret.Clamp("fix", 99));
        Assert.Equal(0, PromptCaret.Clamp("fix", -4));
        Assert.Equal(0, PromptCaret.Clamp(string.Empty, PromptCaret.End));
    }

    [Fact]
    public void GivenCaret_WhenMarking_ThenItIsUnderscoreAtTheEndAndBarInside()
    {
        Assert.Equal("fix_", PromptCaret.Mark("fix", PromptCaret.End));
        Assert.Equal("f|ix", PromptCaret.Mark("fix", 1));
        Assert.Equal("|fix", PromptCaret.Mark("fix", 0));
        Assert.Equal("_", PromptCaret.Mark(string.Empty, 0));
    }

    [Fact]
    public void GivenNarrowRoom_WhenMarking_ThenTheCaretStaysVisibleAndTheTotalFits()
    {
        Assert.Equal("cdef_", PromptCaret.Mark("abcdef", PromptCaret.End, 5));
        Assert.Equal("cd|ef", PromptCaret.Mark("abcdefgh", 4, 5));
        Assert.Equal("|abcd", PromptCaret.Mark("abcdefgh", 0, 5));
    }
}
