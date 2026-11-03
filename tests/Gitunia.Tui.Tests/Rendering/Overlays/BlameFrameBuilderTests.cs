using Gitunia.App.Blame;
using Gitunia.Core.Blame;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

[Trait("Os", "Independent")]
public sealed class BlameFrameBuilderTests
{
    private const int Width = 100;
    private const int Height = 30;

    [Fact]
    public void GivenTheSearchBoxWithTheCaretInside_WhenBuildingTheFrame_ThenTheFooterDrawsTheCaretThere()
    {
        var state = new BlameState(new TabId(1), "a.txt", "a.txt", LineCount: 4, SearchText: "todo", IsEditingSearch: true, SearchCaret: 2);

        var frame = Frame(state);

        Assert.Contains("/to|do", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenGitStillBlaming_WhenBuildingTheFrame_ThenTheFooterShowsTheProgressHint()
    {
        var state = new BlameState(new TabId(1), "big.txt", "big.txt", LineCount: 4, BlamedLines: 0);

        var frame = Frame(state);

        Assert.Contains("blaming 0%", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenHalfTheLinesBlamed_WhenBuildingTheFrame_ThenTheFooterShowsFiftyPercent()
    {
        var state = new BlameState(new TabId(1), "big.txt", "big.txt", LineCount: 4, BlamedLines: 2);

        var frame = Frame(state);

        Assert.Contains("blaming 50%", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenGitFinished_WhenBuildingTheFrame_ThenTheProgressHintIsGone()
    {
        var state = new BlameState(new TabId(1), "big.txt", "big.txt", LineCount: 4, BlamedLines: 4, IsComplete: true);

        var frame = Frame(state);

        Assert.DoesNotContain("blaming", frame, StringComparison.Ordinal);
    }

    private static string Frame(BlameState state)
    {
        var data = new BlameData(1, ["a", "b", "c", "d"]);
        var frame = BlameFrameBuilder.Build(state, data, DateTimeOffset.UnixEpoch, Width, Height, GraphGlyphSet.Unicode)!;

        return string.Join('\n', frame.Lines.Select(line => line.Text));
    }
}
