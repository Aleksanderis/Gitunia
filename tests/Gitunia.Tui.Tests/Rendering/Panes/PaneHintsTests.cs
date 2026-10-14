using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Panes;

public class PaneHintsTests
{
    public static TheoryData<PaneKind> Panes() => new(Enum.GetValues<PaneKind>());

    [Theory]
    [InlineData(PaneKind.Graph, "gg/G")]
    [InlineData(PaneKind.Sidebar, "/")]
    [InlineData(PaneKind.Detail, "l")]
    [InlineData(PaneKind.Diff, "w")]
    public void GivenAPane_WhenAskingForHints_ThenTheyMentionThatPanesKeys(PaneKind pane, string expectedKeys)
    {
        Assert.Contains(PaneHints.For(pane), hint => hint.Keys == expectedKeys);
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenAskingForHints_ThenEveryHintHasKeysAndADescription(PaneKind pane)
    {
        Assert.NotEmpty(PaneHints.For(pane));
        Assert.All(PaneHints.For(pane), hint =>
        {
            Assert.False(string.IsNullOrWhiteSpace(hint.Keys), "A hint has no keys.");
            Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description.");
        });
    }

    [Fact]
    public void GivenThePickerHints_WhenListing_ThenEveryHintHasADescription()
    {
        Assert.All(PaneHints.ForPicker(), hint => Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description."));
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenAskingForHints_ThenNoKeyUsesTheCaretNotation(PaneKind pane)
    {
        Assert.All(PaneHints.For(pane), hint => Assert.DoesNotContain('^', hint.Keys));
    }

    [Fact]
    public void GivenAFocusedDiffPane_WhenBuildingTheStatusBar_ThenItShowsKeysNextToTheirDescriptions()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Hints: PaneHints.For(PaneKind.Diff));

        var text = StatusText(model);

        Assert.Contains("]c/[c hunk", text, StringComparison.Ordinal);
        Assert.Contains("w wrap", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoHints_WhenBuildingTheStatusBar_ThenItShowsTheGraphHintsWithTheSpelledOutNotation()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty);

        var text = StatusText(model);

        Assert.Contains("Ctrl-d/u page", text, StringComparison.Ordinal);
        Assert.DoesNotContain('^', text);
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPaneAt80Columns_WhenBuildingTheStatusBar_ThenHelpAndCommandsStayVisible(PaneKind pane)
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, "Could not read the stashes: boom", Hints: PaneHints.For(pane));

        var text = string.Concat(ChromeBuilder.Status(model, 80, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));

        Assert.Contains("? help  : commands", text, StringComparison.Ordinal);
    }

    private static string StatusText(ScreenModel model) =>
        string.Concat(ChromeBuilder.Status(model, 160, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));
}
