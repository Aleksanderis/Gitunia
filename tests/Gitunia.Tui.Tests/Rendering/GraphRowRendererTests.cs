using Gitunia.Git;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class GraphRowRendererTests
{
    // The short hash of commit number n is n in seven hex digits (CommitFactory.Id).
    private static string[] Rows(GraphGlyphSet glyphs, int width, params CommitInfo[] commits)
    {
        var graph = CommitFactory.Graph(commits);
        var options = new GraphRowOptions(width, GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, width), glyphs);
        return Enumerable.Range(0, graph.Count).Select(index => GraphRowRenderer.Render(graph[index], options, false).Text.TrimEnd()).ToArray();
    }

    private static CommitInfo[] Diamond() =>
    [
        CommitFactory.Commit(4, "merge side", [2, 3]),
        CommitFactory.Commit(2, "main work", [1]),
        CommitFactory.Commit(3, "side work", [1]),
        CommitFactory.Commit(1, "root", []),
    ];

    [Fact]
    public void GivenMergeOfTwoBranches_WhenRendering_ThenLanesForkAndJoin()
    {
        var rows = Rows(GraphGlyphSet.Unicode, 40, Diamond());

        Assert.Equal(
            [
                "○─╮  merge side",
                "● │  main work",
                "├─●  side work",
                "●  root",
            ],
            rows);
    }

    [Fact]
    public void GivenMergeOfTwoBranches_WhenRenderingWithoutUnicode_ThenAsciiLanes()
    {
        var rows = Rows(GraphGlyphSet.Ascii, 40, Diamond());

        Assert.Equal(
            [
                "o-\\  merge side",
                "* |  main work",
                "|-*  side work",
                "*  root",
            ],
            rows);
    }

    [Fact]
    public void GivenManyLanesElsewhere_WhenRenderingARowWithFewLines_ThenTheMessageStartsRightAfterItsOwnLinesAndTheColumnsStayAligned()
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(20, "first", [10]),
            CommitFactory.Commit(21, "second", [11]),
            CommitFactory.Commit(22, "third", [12]),
            CommitFactory.Commit(10, "fourth", []),
            CommitFactory.Commit(11, "fifth", []),
            CommitFactory.Commit(12, "sixth", []));
        var options = new GraphRowOptions(100, GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, 100), GraphGlyphSet.Unicode);

        var rows = Enumerable.Range(0, graph.Count).Select(index => GraphRowRenderer.Render(graph[index], options, false).Text).ToArray();

        Assert.True(graph.MaximumLaneCount >= 3);
        Assert.Equal(3, rows[0].IndexOf("first", StringComparison.Ordinal));
        Assert.Equal(5, rows[1].IndexOf("second", StringComparison.Ordinal));
        Assert.All(rows, row => Assert.Equal(TextWidth.Of(rows[0]), TextWidth.Of(row)));
        Assert.Equal(rows[0].IndexOf("Author", StringComparison.Ordinal), rows[4].IndexOf("Author", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenEdgeSpanningAThroughLane_WhenRendering_ThenCrossingIsDrawn()
    {
        // Lanes 0, 1 and 2 wait for 10, 11 and 12; commit 12 sits in lane 2 and its parent is commit 10 in lane 0.
        var rows = Rows(
            GraphGlyphSet.Unicode,
            30,
            CommitFactory.Commit(20, "a", [10]),
            CommitFactory.Commit(21, "b", [11]),
            CommitFactory.Commit(22, "c", [12]),
            CommitFactory.Commit(12, "d", [10]));

        Assert.Equal("├─┼─●  d", rows[3]);
    }

    [Fact]
    public void GivenAllRefKinds_WhenRendering_ThenDecorationsListedLikeGit()
    {
        var commit = CommitFactory.Commit(
            1,
            "release",
            [],
            new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true),
            new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false),
            new CommitDecoration("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, false),
            new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false));

        var row = Rows(GraphGlyphSet.Unicode, 120, commit)[0];

        Assert.StartsWith("●  (◆ main ↔, topic, tag: v1) release", row);
    }

    [Fact]
    public void GivenAsciiGlyphs_WhenRenderingMergedBranch_ThenAsciiMarkersAreUsed()
    {
        var commit = CommitFactory.Commit(
            1,
            "release",
            [],
            new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true),
            new CommitDecoration("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, false),
            new CommitDecoration("refs/remotes/origin/HEAD", "origin/HEAD", RefKind.RemoteBranch, false));

        var row = Rows(GraphGlyphSet.Ascii, 120, commit)[0];

        Assert.StartsWith("*  (HEAD -> main (r)) release", row);
    }

    [Fact]
    public void GivenDetachedHead_WhenRendering_ThenHeadIsListed()
    {
        var commit = CommitFactory.Commit(1, "x", [], new CommitDecoration("HEAD", "HEAD", RefKind.Head, true));

        Assert.StartsWith("●  (HEAD) x", Rows(GraphGlyphSet.Unicode, 40, commit)[0]);
    }

    [Fact]
    public void GivenSubjectWiderThanRow_WhenRendering_ThenCutWithEllipsisAtExactWidth()
    {
        var commit = CommitFactory.Commit(1, "a very long subject that will not fit", []);

        var row = Rows(GraphGlyphSet.Unicode, 24, commit)[0];

        Assert.Equal("●  a very long subject …", row);
        Assert.Equal(24, TextWidth.Of(row));
    }

    [Fact]
    public void GivenWideCharactersInSubject_WhenRendering_ThenRowKeepsItsWidthAndNeverSplitsACharacter()
    {
        var commit = CommitFactory.Commit(1, "修正バグ修正バグ", []);

        var row = Rows(GraphGlyphSet.Unicode, 16, commit)[0];

        Assert.Equal("●  修正バグ修正…", row);
        Assert.Equal(16, TextWidth.Of(row));
    }

    [Fact]
    public void GivenControlCharactersInSubject_WhenRendering_ThenTheyAreShownAsSpaces()
    {
        var commit = CommitFactory.Commit(1, "tab\there\u001b[31m", []);

        var row = Rows(GraphGlyphSet.Unicode, 40, commit)[0];

        Assert.DoesNotContain('\t', row);
        Assert.DoesNotContain('\u001b', row);
    }

    [Fact]
    public void GivenManyLanesOnNarrowTerminal_WhenChoosingLaneColumns_ThenLaneAreaStaysWithinItsShare()
    {
        Assert.Equal(8, GraphRowOptions.ChooseLaneColumns(30, 40));
        Assert.Equal(3, GraphRowOptions.ChooseLaneColumns(3, 200));
        Assert.Equal(1, GraphRowOptions.ChooseLaneColumns(5, 4));
    }

    [Fact]
    public void GivenMoreLanesThanColumns_WhenRendering_ThenRowStillHasExactWidth()
    {
        var commits = new List<CommitInfo>();
        for (var lane = 0; lane < 6; lane++)
        {
            commits.Add(CommitFactory.Commit(100 + lane, "branch " + lane, [lane]));
        }

        var graph = CommitFactory.Graph(commits.ToArray());
        var options = new GraphRowOptions(30, 2, GraphGlyphSet.Unicode);

        for (var index = 0; index < graph.Count; index++)
        {
            Assert.Equal(30, TextWidth.Of(GraphRowRenderer.Render(graph[index], options, false).Text));
        }
    }

    [Fact]
    public void GivenSelectedRow_WhenRendering_ThenLineIsMarkedSelected()
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "x", []));
        var options = new GraphRowOptions(20, 1, GraphGlyphSet.Unicode);

        Assert.True(GraphRowRenderer.Render(graph[0], options, true).IsSelected);
        Assert.False(GraphRowRenderer.Render(graph[0], options, false).IsSelected);
    }

    [Fact]
    public void GivenHash_WhenRendering_ThenItHasItsOwnRoleAndLanesCarryTheirLane()
    {
        var graph = CommitFactory.Graph(Diamond());
        var options = new GraphRowOptions(100, 2, GraphGlyphSet.Unicode);

        var line = GraphRowRenderer.Render(graph[2], options, false);

        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.Hash && segment.Text == "0000003");
        Assert.Contains(line.Segments, segment => segment is { Role: SegmentRole.Lane, ColorIndex: 1 } && segment.Text.Contains('●', StringComparison.Ordinal));
    }
}
