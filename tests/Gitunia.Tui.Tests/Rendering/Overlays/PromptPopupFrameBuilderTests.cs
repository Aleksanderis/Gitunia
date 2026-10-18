using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class PromptPopupFrameBuilderTests
{
    [Fact]
    public void GivenATextPrompt_WhenBuilding_ThenTitleContextFieldAndHintAreShown()
    {
        var prompt = PromptPopup.ForText("New branch", "At abc1234", "feat", "create");

        var frame = PromptPopupFrameBuilder.Build(prompt, 80, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        var text = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains("New branch", text[0], StringComparison.Ordinal);
        Assert.Contains("At abc1234", text[1], StringComparison.Ordinal);
        Assert.Contains("> feat_", text[2], StringComparison.Ordinal);
        Assert.Contains("Enter create   Esc cancel", text[^2], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnyPrompt_WhenBuilding_ThenItIsCentredAndEveryRowHasTheSameWidth()
    {
        var prompt = PromptPopup.ForText("Title", "Context", string.Empty, "ok");

        var frame = PromptPopupFrameBuilder.Build(prompt, 100, 30, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        Assert.Equal((100 - frame.Width) / 2, frame.Column);
        Assert.Equal((30 - frame.Height) / 2, frame.Row);
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenTextLongerThanThePopup_WhenBuilding_ThenTheEndOfTheTextStaysVisible()
    {
        var typed = new string('a', 200) + "TAIL";
        var prompt = PromptPopup.ForText("Title", "Context", typed, "ok");

        var frame = PromptPopupFrameBuilder.Build(prompt, 80, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        Assert.Contains("TAIL_", frame.Lines[2].Text, StringComparison.Ordinal);
        Assert.Equal(frame.Width, TextWidth.Of(frame.Lines[2].Text));
    }

    [Fact]
    public void GivenWideCharacters_WhenTheTextIsCut_ThenNoCharacterIsSplit()
    {
        var prompt = PromptPopup.ForText("Title", "Context", new string('漢', 80), "ok");

        var frame = PromptPopupFrameBuilder.Build(prompt, 40, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        Assert.Equal(frame.Width, TextWidth.Of(frame.Lines[2].Text));
    }

    [Fact]
    public void GivenAChoice_WhenBuilding_ThenThereIsNoFieldAndEachOptionHasARow()
    {
        var prompt = PromptPopup.ForChoice("Reset", "Move main", ["s  soft", "m  mixed", "h  hard"]);

        var frame = PromptPopupFrameBuilder.Build(prompt, 80, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.DoesNotContain("> ", text, StringComparison.Ordinal);
        Assert.Contains("s  soft", text, StringComparison.Ordinal);
        Assert.Contains("h  hard", text, StringComparison.Ordinal);
        Assert.Contains("Esc cancel", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFilterPrompt_WhenBuilding_ThenTheCandidateAndTheChooseKeysAreShown()
    {
        var prompt = PromptPopup.ForFilter("Switch branch", "Branch", "rel", "release (1/2)", "switch");

        var frame = PromptPopupFrameBuilder.Build(prompt, 80, 24, GraphGlyphSet.Ascii);

        Assert.NotNull(frame);
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Contains("release (1/2)", text, StringComparison.Ordinal);
        Assert.Contains("Up/Down select", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(10, 24)]
    [InlineData(80, 4)]
    [InlineData(0, 0)]
    public void GivenATinyScreen_WhenBuilding_ThenThereIsNoPopup(int width, int height)
    {
        var prompt = PromptPopup.ForText("Title", "Context", "x", "ok");

        Assert.Null(PromptPopupFrameBuilder.Build(prompt, width, height, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenTwoPromptsWithTheSameText_WhenComparing_ThenTheyAreTheSameTextEvenWithOtherLists()
    {
        var first = PromptPopup.ForFilter("T", "C", "x", "d", "ok");
        var second = PromptPopup.ForFilter("T", "C", "x", "d", "ok");
        var typed = PromptPopup.ForFilter("T", "C", "xy", "d", "ok");

        Assert.True(first.SameText(second));
        Assert.False(first.SameText(typed));
        Assert.False(first.SameText(null));
    }
}
