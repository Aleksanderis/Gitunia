using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

/// <summary>The one-line editor and the multi-line editor share one key table, so on one line they must agree.</summary>
[Trait("Os", "Independent")]
public class LineEditKeysTests
{
    private const string Line = "foo bar baz";
    private const int Caret = 5;

    public static TheoryData<string, KeyModifiers> Keys => new()
    {
        { KeyNames.Backspace, KeyModifiers.None },
        { KeyNames.Delete, KeyModifiers.None },
        { KeyNames.Left, KeyModifiers.None },
        { KeyNames.Right, KeyModifiers.None },
        { KeyNames.Home, KeyModifiers.None },
        { KeyNames.End, KeyModifiers.None },
        { "a", KeyModifiers.Ctrl },
        { "e", KeyModifiers.Ctrl },
        { "u", KeyModifiers.Ctrl },
        { "k", KeyModifiers.Ctrl },
        { "w", KeyModifiers.Ctrl },
        { KeyNames.Left, KeyModifiers.Ctrl },
        { KeyNames.Right, KeyModifiers.Ctrl },
        { KeyNames.Backspace, KeyModifiers.Ctrl },
        { KeyNames.Delete, KeyModifiers.Ctrl },
        { "b", KeyModifiers.Alt },
        { "f", KeyModifiers.Alt },
        { "d", KeyModifiers.Alt },
        { KeyNames.Left, KeyModifiers.Alt },
        { KeyNames.Right, KeyModifiers.Alt },
        { KeyNames.Backspace, KeyModifiers.Alt },
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public void GivenALineKey_WhenAppliedToOneLine_ThenBothEditorsEndWithTheSameTextAndCaret(
        string name,
        KeyModifiers modifiers)
    {
        var key = new KeyEvent(name, modifiers);
        var editor = new MessageEditor();
        editor.SetText(Line);
        editor.PlaceCursor(0, Caret);

        var edit = PromptTextEditor.Apply(Line, Caret, key);
        editor.Apply(key);

        Assert.NotNull(LineEditKeys.Resolve(key.Normalize()));
        Assert.Equal(edit?.Text ?? Line, editor.Text);
        Assert.Equal(edit?.Caret ?? Caret, editor.CursorColumn);
    }

    [Theory]
    [InlineData(KeyNames.Enter, KeyModifiers.None)]
    [InlineData(KeyNames.Up, KeyModifiers.None)]
    [InlineData(KeyNames.Tab, KeyModifiers.None)]
    [InlineData("q", KeyModifiers.Ctrl)]
    public void GivenAKeyThatIsNotALineKey_WhenResolving_ThenThereIsNoAction(string name, KeyModifiers modifiers)
    {
        Assert.Null(LineEditKeys.Resolve(new KeyEvent(name, modifiers)));
    }
}
