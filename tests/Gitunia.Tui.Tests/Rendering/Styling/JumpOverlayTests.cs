using Gitunia.Core.Jump.Models;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

public class JumpOverlayTests
{
    [Fact]
    public void GivenALabel_WhenDrawing_ThenTheMatchIsMarkedAndTheLabelReplacesTheNextCharacter()
    {
        var line = Draw("Fix typo", new JumpLabel(0, 0, 3, 'k'), 20);

        Assert.StartsWith("Fixktypo", line.Text, StringComparison.Ordinal);
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.SearchMatch && segment.Text == "Fix");
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.JumpLabel && segment.Text == "k");
    }

    [Fact]
    public void GivenAWideCharacterAfterTheMatch_WhenDrawing_ThenABlankKeepsTheRestOfTheRowInPlace()
    {
        var line = Draw("ab中cd", new JumpLabel(0, 0, 2, 'k'), 10);

        // The wide character took two columns; the label and one blank take the same two.
        Assert.StartsWith("abk cd", line.Text, StringComparison.Ordinal);
        Assert.Equal(TextWidth.Of("ab中cd"), TextWidth.Of("abk cd"));
    }

    [Fact]
    public void GivenASurrogatePairAfterTheMatch_WhenDrawing_ThenBothHalvesAreReplaced()
    {
        var line = Draw("ab\U0001F600cd", new JumpLabel(0, 0, 2, 'k'), 10);

        Assert.StartsWith("abk cd", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain('�', line.Text);
    }

    [Fact]
    public void GivenAMatchThatEndsTheText_WhenDrawing_ThenTheLabelIsAppended()
    {
        var line = Draw("Fix", new JumpLabel(0, 0, 3, 'k'), 10);

        Assert.StartsWith("Fixk", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenATargetWithoutALetter_WhenDrawing_ThenTheMatchIsMarkedAndTheTextStaysWhole()
    {
        var line = Draw("Fix typo", new JumpLabel(0, 0, 3, JumpLabel.NoLetter), 20);

        Assert.StartsWith("Fix typo", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(line.Segments, segment => segment.Role == SegmentRole.JumpLabel);
    }

    private static StyledLine Draw(string shown, JumpLabel label, int width)
    {
        var builder = new LineBuilder(width, "…");
        JumpOverlay.Append(builder, shown, SegmentRole.Plain, label);

        return builder.Build();
    }
}
