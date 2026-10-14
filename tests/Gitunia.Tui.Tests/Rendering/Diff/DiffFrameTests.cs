using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Commits.Models;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

public class DiffFrameTests
{
    [Theory]
    [InlineData(80)]
    [InlineData(100)]
    [InlineData(120)]
    public void GivenAnOpenDiff_WhenComposingAtOrAbove80Columns_ThenFileListAndDiffShareTheTitleRow(int width)
    {
        var lines = Compose(width, 30, new PaneArrangement(true, null));

        var titleRow = lines.Single(line => line.Contains("Details", StringComparison.Ordinal));
        Assert.Contains(" Diff ", titleRow, StringComparison.Ordinal);
        Assert.True(titleRow.IndexOf("Details", StringComparison.Ordinal) < titleRow.IndexOf(" Diff ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("a.txt", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("added line", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.True(TextWidth.Of(line) <= width));
        Assert.Equal(30, lines.Count);
    }

    [Fact]
    public void GivenAnOpenDiff_WhenComposingBelowTheSideBySideThreshold_ThenTheDiffReplacesTheDetailPane()
    {
        var lines = Compose(PaneLayout.MinimumWidthForSideBySide - 1, 30, new PaneArrangement(true, null));

        Assert.DoesNotContain(lines, line => line.Contains("Details", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(" Diff ", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenNoDiff_WhenComposing_ThenOnlyTheDetailPaneIsDrawn()
    {
        var lines = Compose(100, 30, PaneArrangement.Normal, withDiff: false);

        Assert.Contains(lines, line => line.Contains("Details", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains(" Diff ", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAZoomedDiff_WhenComposing_ThenItFillsTheAreaAndTheGraphIsGone()
    {
        var lines = Compose(100, 30, new PaneArrangement(true, PaneKind.Diff));

        Assert.Equal(30, lines.Count);
        Assert.Contains(" Diff ", lines[ScreenLayout.FrameTop], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains("Details", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("1970-01-01 0000002", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAZoomedDetailPane_WhenComposing_ThenItFillsTheAreaAndTheDiffIsGone()
    {
        var lines = Compose(100, 30, new PaneArrangement(true, PaneKind.Detail));

        Assert.Contains("Details", lines[ScreenLayout.FrameTop], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains(" Diff ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("1970-01-01 0000002", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAZoomedGraph_WhenComposing_ThenTheGraphTakesTheWholeAreaWithoutPanes()
    {
        var lines = Compose(100, 30, new PaneArrangement(true, PaneKind.Graph));

        Assert.Contains("1970-01-01 0000002", lines[ScreenLayout.GraphTop], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains("Details", StringComparison.Ordinal) || line.Contains(" Diff ", StringComparison.Ordinal));
        Assert.Equal(30, lines.Count);
    }

    private static List<string> Compose(int width, int height, PaneArrangement arrangement, bool withDiff = true)
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(2, "commit two", [1]), CommitFactory.Commit(1, "root", []));
        var details = new CommitDetails(
            CommitFactory.Id(2),
            [CommitFactory.Id(1)],
            "Ann",
            "ann@x.org",
            new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero),
            "Ann",
            "ann@x.org",
            new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero),
            "commit two",
            [new CommitFileChange("a.txt", null, FileChangeKind.Added, null)]);
        var content = DetailContentBuilder.Build(graph[0], new CommitDetailsState(graph[0].Id, CommitDetailsStatus.Loaded, details, string.Empty), BottomTab.Changes);
        var document = new DiffDocument(
            [new DiffRow(DiffRowKind.FileHeader, "a.txt", null, null, 0), new DiffRow(DiffRowKind.Added, "added line", null, 1, 0)],
            [],
            [0],
            [["a.txt"]],
            1);
        var model = new ScreenModel("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 2, false, string.Empty);
        var diff = withDiff ? new DiffPaneModel(document, "0000002", 0, 0, false) : null;
        return [.. ScreenComposer.Compose(model, graph, 0, 0, width, height, GraphGlyphSet.Unicode, new DetailPaneModel(content, 0, 0, false), null, null, diff, arrangement).Select(line => line.Text)];
    }
}
