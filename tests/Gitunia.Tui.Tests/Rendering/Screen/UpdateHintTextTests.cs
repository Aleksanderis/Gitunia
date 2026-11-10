using Gitunia.Core.Updates;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Screen;

public class UpdateHintTextTests
{
    private static readonly UpdateNotice Notice = new("0.9.0", "gitunia update");

    [Theory]
    [InlineData(120, "Gitunia 0.9.0 available: gitunia update")]
    [InlineData(80, "Gitunia 0.9.0 available")]
    [InlineData(60, "")]
    [InlineData(40, "")]
    [InlineData(0, "")]
    public void GivenAWidth_WhenChoosingTheText_ThenTheLongestFormThatLeavesRoomWins(int width, string expected)
    {
        Assert.Equal(expected, UpdateHintText.For(Notice, width));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(90)]
    [InlineData(60)]
    [InlineData(30)]
    public void GivenANotice_WhenDrawingTheStatusBar_ThenTheLineKeepsItsWidthAndTheHintEndsIt(int width)
    {
        var line = ChromeBuilder.Status(Model() with { Update = Notice }, width, GraphGlyphSet.Unicode);

        Assert.Equal(width, TextWidth.Of(line.Text));
        var hint = UpdateHintText.For(Notice, width);
        if (hint.Length > 0)
        {
            Assert.EndsWith(hint, line.Text.TrimEnd(), StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("available", line.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GivenNoNotice_WhenDrawingTheStatusBar_ThenNoHintIsDrawn()
    {
        var line = ChromeBuilder.Status(Model(), 120, GraphGlyphSet.Unicode);

        Assert.DoesNotContain("available", line.Text, StringComparison.Ordinal);
    }

    private static ScreenModel Model() =>
        new("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty);
}
