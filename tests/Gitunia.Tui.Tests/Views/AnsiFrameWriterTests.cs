using Gitunia.Platform;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views;
using Xunit;
using StyledSegment = Gitunia.Tui.Rendering.Styling.StyledSegment;

namespace Gitunia.Tui.Tests.Views;

public class AnsiFrameWriterTests
{
    [Fact]
    public void GivenSixteenColourScheme_WhenWriting_ThenForegroundsUseThirtyAndNinetyCodes()
    {
        var line = new StyledLine([new StyledSegment("a", SegmentRole.LocalBranch), new StyledSegment("b", SegmentRole.Error)]);

        var text = AnsiFrameWriter.Render([line], new PaletteSettings(ColorDepth.Ansi16, false));

        // Local branch is bright green (92), error bright red (91), both bold.
        Assert.Contains("\u001b[0;1;92ma", text, StringComparison.Ordinal);
        Assert.Contains("\u001b[0;1;91mb", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenSixteenColourScheme_WhenWritingASelectedRow_ThenTheBackgroundUsesAHundredCode()
    {
        var line = new StyledLine([new StyledSegment("a", SegmentRole.Plain)], true);

        var text = AnsiFrameWriter.Render([line], new PaletteSettings(ColorDepth.Ansi16, false));

        Assert.Contains(";100m", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoColour_WhenWritingASelectedRow_ThenOnlyStylesAreWritten()
    {
        var line = new StyledLine([new StyledSegment("a", SegmentRole.Plain)], true);

        var text = AnsiFrameWriter.Render([line], new PaletteSettings(ColorDepth.None, false));

        Assert.Equal("\u001b[0;7ma\u001b[0m\n", text);
    }

    [Fact]
    public void GivenSegmentSelectionOverrides_WhenWriting_ThenTheyWinOverTheLine()
    {
        var line = new StyledLine([new StyledSegment("a", SegmentRole.Plain, 0, SegmentSelection.None), new StyledSegment("b", SegmentRole.Plain, 0, SegmentSelection.Dim)], true);

        var text = AnsiFrameWriter.Render([line], new PaletteSettings(ColorDepth.None, false));

        Assert.Equal("\u001b[0ma\u001b[0;4mb\u001b[0m\n", text);
    }
}
