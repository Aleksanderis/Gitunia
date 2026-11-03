using Gitunia.Core.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

public class PasteKeysTests
{
    [Theory]
    [InlineData("https://example.com/a.git", "https://example.com/a.git")] // Every character is one printable key.
    [InlineData("Enter", "Enter")] // A text that spells a named key stays typed letters.
    [InlineData("git@host:a/b.git\r\n\n", "git@host:a/b.git")] // Trailing line breaks are dropped, never Enter.
    [InlineData("\nfix\r\n\r\nthe bug\r", "fix the bug")] // Each run of line breaks in the middle is one space.
    [InlineData("a\tb\u001bc\u0007", "a bc")] // Tabs are spaces, other control characters are dropped.
    [InlineData("", "")]
    public void GivenSingleLineText_WhenConverting_ThenItIsTypedAsPrintableKeys(string text, string expected)
    {
        var keys = PasteKeys.FromText(text, multiLine: false);

        Assert.Equal(expected, string.Concat(keys.Select(key => key.Name)));
        Assert.All(keys, key => Assert.True(key.IsPrintable));
    }

    [Fact]
    public void GivenLineBreaks_WhenMultiLine_ThenEachIsAnEnterKey()
    {
        var keys = PasteKeys.FromText("a\r\nb\nc", multiLine: true);

        Assert.Equal(["a", KeyNames.Enter, "b", KeyNames.Enter, "c"], keys.Select(key => key.Name));
    }

    [Fact]
    public void GivenAnEmojiAndACombiningMark_WhenConverting_ThenEachStaysOneKey()
    {
        var keys = PasteKeys.FromText("\U0001F600e\u0301", multiLine: false);

        Assert.Equal(["\U0001F600", "e\u0301"], keys.Select(key => key.Name));
    }

    [Fact]
    public void GivenTabsAndNewlines_WhenMultiLine_ThenTabsStayTabKeysSoNothingIsFlattened()
    {
        var keys = PasteKeys.FromText("a\tb\n\tc", multiLine: true);

        Assert.Equal(["a", KeyNames.Tab, "b", KeyNames.Enter, KeyNames.Tab, "c"], keys.Select(key => key.Name));
    }
}
