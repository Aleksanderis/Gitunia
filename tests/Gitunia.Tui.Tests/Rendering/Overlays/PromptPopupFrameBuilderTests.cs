using Gitunia.Core.Editing;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

[Trait("Os", "Independent")]
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
        Assert.Contains("› feat ", text[2], StringComparison.Ordinal);
        Assert.Equal(" ", CursorCell.Of(frame.Lines[2]));
        Assert.Contains("Enter create   Esc cancel", text[^2], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenACaretInsideTheText_WhenBuilding_ThenItIsDrawnThereAndLongTextKeepsItVisible()
    {
        var inside = PromptPopup.ForText("T", "C", new TextField("feature", 3), "ok");
        var longText = PromptPopup.ForText("T", "C", new TextField(new string('a', 120) + "XY", 60), "ok");

        var short1 = PromptPopupFrameBuilder.Build(inside, 80, 24, GraphGlyphSet.Unicode);
        var long1 = PromptPopupFrameBuilder.Build(longText, 80, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(short1);
        Assert.NotNull(long1);
        Assert.Contains("› feature", short1.Lines[2].Text, StringComparison.Ordinal);
        Assert.Equal("t", CursorCell.Of(short1.Lines[2]));
        Assert.Equal("a", CursorCell.Of(long1.Lines[2]));
        Assert.Equal(long1.Width, TextWidth.Of(long1.Lines[2].Text));
        Assert.DoesNotContain("XY", long1.Lines[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenATypedAnswer_WhenBuilding_ThenTheTextIsPlainLikeThePickerNotTheKeyColour()
    {
        var prompt = PromptPopup.ForText("New branch", "At abc1234", "feat", "create");

        var frame = PromptPopupFrameBuilder.Build(prompt, 80, 24, GraphGlyphSet.Unicode)!;

        var typed = frame.Lines[2].Segments.Where(segment => segment.Text.Contains("feat", StringComparison.Ordinal));
        Assert.All(typed, segment => Assert.Equal(SegmentRole.TypedText, segment.Role));
        Assert.DoesNotContain(frame.Lines[2].Segments, segment => segment.Role == SegmentRole.OverlayKey);
    }

    [Theory]
    [InlineData(62)]
    [InlineData(64)]
    [InlineData(66)]
    public void GivenAScreenJustWiderThanThePopup_WhenBuilding_ThenItTakesTheWholeWidthSoNoAppBorderShowsBesideIt(
        int screenWidth)
    {
        var prompt = PromptPopup.ForText("New branch", "At abc1234", "feat", "create");

        var frame = PromptPopupFrameBuilder.Build(prompt, screenWidth, 30, GraphGlyphSet.Unicode)!;

        Assert.Equal(0, frame.Column);
        Assert.Equal(screenWidth, frame.Width);
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
        Assert.Contains("TAIL ", frame.Lines[2].Text, StringComparison.Ordinal);
        Assert.Equal(" ", CursorCell.Of(frame.Lines[2]));
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
        Assert.DoesNotContain("› ", text, StringComparison.Ordinal);
        Assert.DoesNotContain(frame.Lines, line => line.IsSelected);
        Assert.Contains("s  soft", text, StringComparison.Ordinal);
        Assert.Contains("h  hard", text, StringComparison.Ordinal);
        Assert.Contains("Esc cancel", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFilterPrompt_WhenBuilding_ThenTheHighlightIsTheSelectionBarAndThePositionIsInTheTitle()
    {
        var rows = new PickerWindow(["release", "relay"], 1, "2/2");
        var prompt = PromptPopup.ForFilter("Switch branch", "Branch", "rel", rows, "switch");

        var frame = PromptPopupFrameBuilder.Build(prompt, 80, 24, GraphGlyphSet.Ascii);

        Assert.NotNull(frame);
        var text = string.Join('\n', frame.Lines.Select(line => line.Text));
        Assert.Matches(@"^ Switch branch +2/2 $", frame.Lines[0].Text);
        Assert.Contains("> rel ", frame.Lines[2].Text, StringComparison.Ordinal);
        Assert.Equal(" ", CursorCell.Of(frame.Lines[2]));
        var release = Assert.Single(frame.Lines, line => line.Text.Contains("release", StringComparison.Ordinal));
        var relay = Assert.Single(frame.Lines, line => line.Text.Contains("relay", StringComparison.Ordinal));
        Assert.False(release.IsSelected);
        Assert.True(relay.IsSelected);
        Assert.Equal(1, frame.Lines.Count(line => line.IsSelected));
        Assert.DoesNotContain("> relay", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(2/2)", text, StringComparison.Ordinal);
        Assert.Contains("type to filter   Ctrl-j/k move   Enter switch   Esc cancel", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl-j/k move", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(24, 8)]
    [InlineData(14, 6)]
    [InlineData(10, 2)]
    [InlineData(9, 1)]
    public void GivenAFullCandidateListOnAShortScreen_WhenBuilding_ThenThePopupShrinksAroundTheHighlight(
        int screenHeight,
        int expectedRows)
    {
        var rows = new List<string>();
        for (var number = 0; number < 8; number++)
        {
            rows.Add($"branch{number}");
        }

        var window = new PickerWindow(rows, 6, "7/20");
        var prompt = PromptPopup.ForFilter("Switch branch", "Branch", string.Empty, window, "switch");

        var frame = PromptPopupFrameBuilder.Build(prompt, 80, screenHeight, GraphGlyphSet.Ascii);

        Assert.NotNull(frame);
        Assert.True(frame.Lines.Count <= screenHeight - 2);
        var selected = Assert.Single(frame.Lines, line => line.IsSelected);
        Assert.Contains("branch6", selected.Text, StringComparison.Ordinal);
        Assert.Contains("7/20", frame.Lines[0].Text, StringComparison.Ordinal);
        var branchRows = frame.Lines.Skip(1).Where(line => line.Text.Contains("branch", StringComparison.Ordinal));
        Assert.Equal(expectedRows, branchRows.Count(line => line.Text.Any(char.IsDigit)));
    }

    [Fact]
    public void GivenAWideCharacterTitle_WhenBuilding_ThenTheCountEndsAtTheRightEdge()
    {
        var window = new PickerWindow(["a"], 0, "1/1");
        var prompt = PromptPopup.ForFilter("ブランチ", "C", string.Empty, window, "ok");

        var frame = PromptPopupFrameBuilder.Build(prompt, 80, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        Assert.Equal(frame.Width, TextWidth.Of(frame.Lines[0].Text));
        Assert.EndsWith("1/1 ", frame.Lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenANarrowPopup_WhenTheTitleAndCountDoNotFit_ThenTheCountIsLeftOut()
    {
        var window = new PickerWindow(["a"], 0, "1/1");
        var prompt = PromptPopup.ForFilter(new string('T', 40), "C", string.Empty, window, "ok");

        var frame = PromptPopupFrameBuilder.Build(prompt, 44, 24, GraphGlyphSet.Ascii);

        Assert.NotNull(frame);
        Assert.DoesNotContain("1/1", frame.Lines[0].Text, StringComparison.Ordinal);
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
    public void GivenTwoPrompts_WhenComparing_ThenTheyAreTheSameTextOnlyWithTheSameTextHighlightAndCount()
    {
        var first = PromptPopup.ForFilter("T", "C", "x", new PickerWindow(["d"]), "ok");
        var second = PromptPopup.ForFilter("T", "C", "x", new PickerWindow(["d"]), "ok");
        var typed = PromptPopup.ForFilter("T", "C", "xy", new PickerWindow(["d"]), "ok");
        var moved = PromptPopup.ForFilter("T", "C", "x", new PickerWindow(["d"], 0, "1/1"), "ok");

        Assert.True(first.SameText(second));
        Assert.False(first.SameText(typed));
        Assert.False(first.SameText(moved));
        Assert.False(first.SameText(null));
    }

    [Fact]
    public void GivenAWideScreenAndALongContext_WhenBuilding_ThenTheContextIsNotWrapped()
    {
        var context = "Create a branch from the commit abc1234 and check it out in the linked worktree afterwards";
        var prompt = PromptPopup.ForText("New branch", context, "feat", "create");

        var frame = PromptPopupFrameBuilder.Build(prompt, 120, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains(frame.Lines, line => line.Text.Contains(context, StringComparison.Ordinal));
        Assert.True(frame.Width > 64 && frame.Width <= 96);
    }
}
