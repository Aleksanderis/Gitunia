using Gitunia.Core.Workspaces;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class PaneHintsTests
{
    [Theory]
    [InlineData(PaneKind.Graph, "gg G")]
    [InlineData(PaneKind.Sidebar, "filter")]
    [InlineData(PaneKind.Detail, "diff")]
    [InlineData(PaneKind.Diff, "wrap")]
    public void GivenAPane_WhenAskingForHints_ThenTheyMentionThatPanesKeys(PaneKind pane, string expected)
    {
        Assert.Contains(expected, PaneHints.For(pane), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFocusedDiffPane_WhenBuildingTheStatusBar_ThenItShowsTheDiffHints()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Hints: PaneHints.For(PaneKind.Diff));

        var line = ChromeBuilder.Status(model, 120, GraphGlyphSet.Unicode);

        Assert.Contains("]c [c", string.Concat(line.Segments.Select(segment => segment.Text)), StringComparison.Ordinal);
    }
}
