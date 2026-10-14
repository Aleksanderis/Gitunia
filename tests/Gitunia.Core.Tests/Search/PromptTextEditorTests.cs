using Gitunia.Core.Keymap;
using Gitunia.Core.Search;
using Xunit;

namespace Gitunia.Core.Tests.Search;

public class PromptTextEditorTests
{
    [Fact]
    public void GivenPrintableKey_WhenApplying_ThenItIsAppended()
    {
        Assert.Equal("fix:", PromptTextEditor.Apply("fix", new KeyEvent(":")));
        Assert.Equal("a b", $"{PromptTextEditor.Apply("a", new KeyEvent(" "))}b");
    }

    [Fact]
    public void GivenBackspace_WhenApplying_ThenTheLastCharacterGoesIncludingSurrogatePairs()
    {
        Assert.Equal("fi", PromptTextEditor.Apply("fix", new KeyEvent(KeyNames.Backspace)));
        Assert.Equal("a", PromptTextEditor.Apply("a\U0001F600", new KeyEvent(KeyNames.Backspace)));
        Assert.Equal(string.Empty, PromptTextEditor.Apply(string.Empty, new KeyEvent(KeyNames.Backspace)));
    }

    [Fact]
    public void GivenCtrlUAndCtrlW_WhenApplying_ThenLineOrWordIsDeleted()
    {
        Assert.Equal(string.Empty, PromptTextEditor.Apply("fix bug", new KeyEvent("u", KeyModifiers.Ctrl)));
        Assert.Equal("fix ", PromptTextEditor.Apply("fix bug", new KeyEvent("w", KeyModifiers.Ctrl)));
        Assert.Equal(string.Empty, PromptTextEditor.Apply("fix", new KeyEvent("w", KeyModifiers.Ctrl)));
    }

    [Fact]
    public void GivenOtherKeys_WhenApplying_ThenTheyDoNotEdit()
    {
        Assert.Null(PromptTextEditor.Apply("fix", new KeyEvent(KeyNames.Up)));
        Assert.Null(PromptTextEditor.Apply("fix", new KeyEvent("x", KeyModifiers.Alt)));
    }
}
