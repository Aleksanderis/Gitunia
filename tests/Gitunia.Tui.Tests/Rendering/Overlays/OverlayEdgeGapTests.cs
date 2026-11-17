using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Core.Reflog;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Reflog.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

/// <summary>
/// An overlay that stops one to three columns short of the screen edge would show the app frame right beside its
/// border (<c>││</c>), so it takes the whole width (<see cref="OverlayMetrics.Snap"/>).
/// </summary>
[Trait("Os", "Independent")]
public class OverlayEdgeGapTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    [Theory]
    [InlineData(40)]
    [InlineData(64)]
    [InlineData(80)]
    [InlineData(100)]
    [InlineData(120)]
    public void GivenAnyOverlay_WhenRenderingAtCommonWidths_ThenNoOneToThreeColumnGapIsLeftBesideIt(int screenWidth)
    {
        var prompt = new ConfirmPrompt("Delete", ["Delete branch x?"], "Cannot be undone.", "Delete");
        var entries = new[] { new ReflogEntry(0, new string('a', 40), "commit", "message", "now") };
        var frames = new Dictionary<string, OverlayFrame?>
        {
            ["palette"] = PaletteFrameBuilder.Build(
                new PaletteState(PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView)),
                screenWidth,
                30,
                GraphGlyphSet.Unicode),
            ["help"] = HelpFrameBuilder.Build(
                HelpBuilder.Contextual(Table, DefaultKeymap.GraphView),
                0,
                DefaultKeymap.GraphView,
                0,
                screenWidth,
                30,
                GraphGlyphSet.Unicode),
            ["reflog"] = ReflogFrameBuilder.Build(
                new ReflogState(new TabId(1), "HEAD", "HEAD", entries),
                screenWidth,
                30,
                GraphGlyphSet.Unicode),
            ["confirm"] = ConfirmFrameBuilder.Build(prompt, screenWidth, 30, GraphGlyphSet.Unicode),
            ["mainline"] = MainlineFrameBuilder.Build(
                "Revert merge commit abc: keep the mainline of",
                ["aaaaaaa main two", "bbbbbbb topic two"],
                screenWidth,
                30,
                GraphGlyphSet.Unicode,
                2),
        };

        foreach (var (name, frame) in frames)
        {
            Assert.NotNull(frame);
            var gap = screenWidth - frame.Width;
            Assert.False(gap is >= 1 and <= OverlayMetrics.SnapGap, $"{name} at {screenWidth}: gap {gap}");
        }
    }
}
