using Gitunia.Core.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

public class PasteKeysTests
{
    [Fact]
    public void GivenAUrl_WhenConverting_ThenEveryCharacterIsOnePrintableKey()
    {
        var keys = PasteKeys.FromText("https://example.com/a.git", multiLine: false);

        Assert.Equal("https://example.com/a.git", string.Concat(keys.Select(key => key.Name)));
        Assert.All(keys, key => Assert.True(key.IsPrintable));
    }

    [Fact]
    public void GivenATextThatSpellsANamedKey_WhenConverting_ThenItStaysTypedLetters()
    {
        var keys = PasteKeys.FromText("Enter", multiLine: false);

        Assert.Equal(5, keys.Count);
        Assert.All(keys, key => Assert.True(key.IsPrintable));
    }

    [Fact]
    public void GivenTrailingLineBreaks_WhenSingleLine_ThenTheyAreDroppedAndNeverBecomeEnter()
    {
        var keys = PasteKeys.FromText("git@host:a/b.git\r\n\n", multiLine: false);

        Assert.Equal("git@host:a/b.git", string.Concat(keys.Select(key => key.Name)));
        Assert.DoesNotContain(keys, key => key.Name == KeyNames.Enter);
    }

    [Fact]
    public void GivenLineBreaksInTheMiddle_WhenSingleLine_ThenEachRunBecomesOneSpace()
    {
        var keys = PasteKeys.FromText("\nfix\r\n\r\nthe bug\r", multiLine: false);

        Assert.Equal("fix the bug", string.Concat(keys.Select(key => key.Name)));
    }

    [Fact]
    public void GivenLineBreaks_WhenMultiLine_ThenEachIsAnEnterKey()
    {
        var keys = PasteKeys.FromText("a\r\nb\nc", multiLine: true);

        Assert.Equal(["a", KeyNames.Enter, "b", KeyNames.Enter, "c"], keys.Select(key => key.Name));
    }

    [Fact]
    public void GivenTabsAndControlCharacters_WhenConverting_ThenTabsAreSpacesAndTheRestIsDropped()
    {
        var keys = PasteKeys.FromText("a\tb\u001bc\u0007", multiLine: false);

        Assert.Equal("a bc", string.Concat(keys.Select(key => key.Name)));
    }

    [Fact]
    public void GivenAnEmojiAndACombiningMark_WhenConverting_ThenEachStaysOneKey()
    {
        var keys = PasteKeys.FromText("\U0001F600é", multiLine: false);

        Assert.Equal(["\U0001F600", "é"], keys.Select(key => key.Name));
    }

    [Fact]
    public void GivenEmptyText_WhenConverting_ThenThereAreNoKeys()
    {
        Assert.Empty(PasteKeys.FromText(string.Empty, multiLine: false));
    }
}
