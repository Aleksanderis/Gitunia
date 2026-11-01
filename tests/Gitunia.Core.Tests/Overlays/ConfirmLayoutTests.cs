using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

[Trait("Os", "Independent")]
public class ConfirmLayoutTests
{
    private static readonly ConfirmPrompt Short = new("Delete", ["Delete branch x?"], "Cannot be undone.", "Delete");

    [Fact]
    public void GivenAShortPrompt_WhenTheScreenIsWide_ThenTheCompactWidthIsKept()
    {
        Assert.Equal(64, ConfirmLayout.WidthFor(Short, 120));
    }

    [Fact]
    public void GivenAShortPrompt_WhenTheScreenIsRoomy_ThenItFits()
    {
        Assert.True(ConfirmLayout.Fits(Short, 80, 24));
    }

    [Theory]
    [InlineData(17, 24)]
    [InlineData(80, 9)]
    [InlineData(0, 0)]
    public void GivenAShortPrompt_WhenTheScreenIsTooSmall_ThenItDoesNotFit(int width, int height)
    {
        Assert.False(ConfirmLayout.Fits(Short, width, height));
    }

    [Fact]
    public void GivenALongParagraph_WhenItWrapsOverMoreRows_ThenItNeedsAtLeastThoseRows()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 80));
        var prompt = new ConfirmPrompt("Delete", [text], "Cannot be undone.", "Delete");

        Assert.False(ConfirmLayout.Fits(prompt, 80, 12));
        Assert.True(ConfirmLayout.Fits(prompt, 80, 30));
    }

    [Fact]
    public void GivenAWideCharacterPrompt_WhenMeasured_ThenColumnsNotCharactersDecide()
    {
        var wide = new string('中', 40);
        var prompt = new ConfirmPrompt("Delete", [wide], "x", "Delete");

        // 40 characters take 80 columns, plus the border and the padding.
        Assert.Equal(84, ConfirmLayout.WidthFor(prompt, 120));
        Assert.True(ConfirmLayout.Fits(prompt, 120, 8 + 2));

        // On a narrow screen the same count of narrow characters fits one row, the wide ones need two.
        var narrow = new ConfirmPrompt("Delete", [new string('x', 40)], "x", "Delete");
        Assert.True(ConfirmLayout.Fits(narrow, 60, 10));
        Assert.False(ConfirmLayout.Fits(prompt, 60, 10));
    }
}
