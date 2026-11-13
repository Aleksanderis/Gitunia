using Gitunia.Core.Config.Models;
using Gitunia.Core.Graph;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Search.Models;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Graph;

public class GraphRowRendererTests
{
    // The short hash of commit number n is n in seven hex digits (CommitFactory.Id).
    private static string[] Rows(GraphGlyphSet glyphs, int width, params CommitInfo[] commits)
    {
        var graph = CommitFactory.Graph(commits);
        var options = new GraphRowOptions(width, GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, width), glyphs);
        return [.. Enumerable.Range(0, graph.Count).Select(index => GraphRowRenderer.Render(graph[index], options, false).Text.TrimEnd())];
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
                "├ │  main work",
                "│ ├  side work",
                "●─╯  root",
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
                "| |  main work",
                "| |  side work",
                "*-/  root",
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
    public void GivenEdgeSpanningAThroughLane_WhenRendering_ThenTheLaneHasAGapInsteadOfACrossing()
    {
        // Lanes 0, 1 and 2 wait for 10, 11 and 12; commit 12 sits in lane 2 and merges commit 10 in lane 0.
        var rows = Rows(
            GraphGlyphSet.Unicode,
            30,
            CommitFactory.Commit(20, "a", [10]),
            CommitFactory.Commit(21, "b", [11]),
            CommitFactory.Commit(22, "c", [12]),
            CommitFactory.Commit(12, "d", [13, 10]));

        Assert.Equal("├───○  d", rows[3]);
    }

    [Fact]
    public void GivenCommitWithARefInTheMiddleOfALine_WhenRendering_ThenItKeepsItsNodeDot()
    {
        var tag = new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false);

        var rows = Rows(
            GraphGlyphSet.Unicode,
            40,
            CommitFactory.Commit(3, "tip", [2]),
            CommitFactory.Commit(2, "tagged", [1], tag),
            CommitFactory.Commit(1, "root", []));

        Assert.StartsWith("●", rows[1]);
    }

    [Fact]
    public void GivenCommitWithoutARefInTheMiddleOfALine_WhenRendering_ThenItIsPartOfTheLine()
    {
        var rows = Rows(
            GraphGlyphSet.Unicode,
            40,
            CommitFactory.Commit(3, "tip", [2]),
            CommitFactory.Commit(2, "plain", [1]),
            CommitFactory.Commit(1, "root", []));

        Assert.StartsWith("├", rows[1]);
    }

    [Fact]
    public void GivenBranchesWithHeadAndRemoteMarks_WhenRendering_ThenTheMarksSitInsideTheBoxAndNoParenthesesAreDrawn()
    {
        var commit = CommitFactory.Commit(
            1,
            "release",
            [],
            new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true),
            new CommitDecoration("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, false));
        var graph = CommitFactory.Graph(commit);

        foreach (var glyphs in new[] { GraphGlyphSet.Unicode, GraphGlyphSet.Ascii })
        {
            var line = GraphRowRenderer.Render(graph[0], new GraphRowOptions(80, 1, glyphs), false);

            var box = line.Segments.Single(segment => segment.Role == SegmentRole.LaneLabel);
            Assert.StartsWith($" {glyphs.CurrentBranch}main", box.Text, StringComparison.Ordinal);
            Assert.EndsWith($"{glyphs.Remote} ", box.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("(", line.Text.Replace("(r)", ""), StringComparison.Ordinal);
            Assert.DoesNotContain(")", line.Text.Replace("(r)", ""), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GivenLocalBranchNotCheckedOut_WhenRendering_ThenItCarriesTheLocalGlyphAndRemoteOnlyDoesNot()
    {
        var commit = CommitFactory.Commit(
            1,
            "release",
            [],
            new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false),
            new CommitDecoration("refs/remotes/origin/topic", "origin/topic", RefKind.RemoteBranch, false),
            new CommitDecoration("refs/remotes/origin/other", "origin/other", RefKind.RemoteBranch, false));
        var graph = CommitFactory.Graph(commit);

        var line = GraphRowRenderer.Render(graph[0], new GraphRowOptions(80, 1, GraphGlyphSet.Unicode), false);

        var labels = line.Segments
            .Where(segment => segment.Role == SegmentRole.LaneLabel)
            .Select(segment => segment.Text)
            .ToArray();
        Assert.Equal($" {GraphGlyphSet.Unicode.LocalBranch}topic{GraphGlyphSet.Unicode.Remote} ", labels[0]);
        Assert.Equal(" origin/other ", labels[1]);
    }

    [Fact]
    public void GivenAnyBranchLabel_WhenRendering_ThenTheBoxHasOneSpaceOfPaddingOnEachSide()
    {
        var commit = CommitFactory.Commit(
            1,
            "release",
            [],
            new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false),
            new CommitDecoration("refs/remotes/origin/other", "origin/other", RefKind.RemoteBranch, false));
        var graph = CommitFactory.Graph(commit);

        foreach (var glyphs in new[] { GraphGlyphSet.Unicode, GraphGlyphSet.Ascii })
        {
            var line = GraphRowRenderer.Render(graph[0], new GraphRowOptions(120, 1, glyphs), false);

            var boxes = line.Segments.Where(segment => segment.Role == SegmentRole.LaneLabel).ToArray();
            Assert.Equal(2, boxes.Length);
            Assert.All(boxes, box => Assert.Matches(@"^ \S.*\S $", box.Text));
        }
    }

    [Theory]
    [InlineData(LabelCapStyle.Rounded, "\uE0B6", "\uE0B4")]
    [InlineData(LabelCapStyle.Angled, "\uE0B2", "\uE0B0")]
    public void GivenCaps_WhenRendering_ThenEachBoxIsWrappedInLaneColouredCapsWithNoPaddingInside(
        LabelCapStyle style,
        string left,
        string right)
    {
        var commit = CommitFactory.Commit(
            1,
            "release",
            [],
            new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true),
            new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false));
        var graph = CommitFactory.Graph(commit);
        var glyphs = GraphGlyphSet.Unicode.WithLabelCaps(style, hasColors: true);

        var line = GraphRowRenderer.Render(graph[0], new GraphRowOptions(120, 1, glyphs), false);

        var segments = line.Segments.ToList();
        var boxes = segments.Where(segment => segment.Role == SegmentRole.LaneLabel).ToList();
        Assert.Equal(2, boxes.Count);
        foreach (var box in boxes)
        {
            var at = segments.IndexOf(box);
            Assert.Equal(new StyledSegment(left, SegmentRole.Lane, box.ColorIndex), segments[at - 1]);
            Assert.Equal(new StyledSegment(right, SegmentRole.Lane, box.ColorIndex), segments[at + 1]);
            Assert.Matches(@"^\S.*\S$", box.Text);
        }

        Assert.Contains($"{left}{glyphs.CurrentBranch}main", line.Text, StringComparison.Ordinal);
        Assert.Equal(1, TextWidth.Of(left));
        Assert.Equal(1, TextWidth.Of(right));
    }

    [Fact]
    public void GivenAGlyphSetWithCaps_WhenAskingIfItHasCaps_ThenOnlyTheCappedSetSaysYes()
    {
        Assert.True(GraphGlyphSet.Unicode.WithLabelCaps(LabelCapStyle.Rounded, hasColors: true).HasLabelCaps);
        Assert.False(GraphGlyphSet.Unicode.WithLabelCaps(LabelCapStyle.None, hasColors: true).HasLabelCaps);
        Assert.False(GraphGlyphSet.Ascii.WithLabelCaps(LabelCapStyle.Rounded, hasColors: true).HasLabelCaps);
    }

    [Fact]
    public void GivenNoCapsOrTiersThatCannotShowThem_WhenAskingForCaps_ThenTheSetKeepsThePlainBox()
    {
        Assert.Equal(GraphGlyphSet.Unicode, GraphGlyphSet.Unicode.WithLabelCaps(LabelCapStyle.None, hasColors: true));
        Assert.Equal(GraphGlyphSet.Unicode, GraphGlyphSet.Unicode.WithLabelCaps(LabelCapStyle.Rounded, hasColors: false));
        Assert.Equal(GraphGlyphSet.Ascii, GraphGlyphSet.Ascii.WithLabelCaps(LabelCapStyle.Rounded, hasColors: true));
    }

    [Fact]
    public void GivenNoCaps_WhenRendering_ThenNoCapSegmentIsDrawn()
    {
        var commit = CommitFactory.Commit(
            1, "release", [], new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true));
        var graph = CommitFactory.Graph(commit);

        var line = GraphRowRenderer.Render(graph[0], new GraphRowOptions(120, 1, GraphGlyphSet.Unicode), false);

        Assert.DoesNotContain(line.Segments, segment => segment.Text is "\uE0B6" or "\uE0B4");
    }

    [Fact]
    public void GivenUpstreamGoneBranch_WhenRendering_ThenTheMarkFollowsTheBoxAfterASpace()
    {
        var commit = CommitFactory.Commit(
            1,
            "release",
            [],
            new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false));
        var graph = CommitFactory.Graph(commit);

        foreach (var glyphs in new[] { GraphGlyphSet.Unicode, GraphGlyphSet.Ascii })
        {
            var options = new GraphRowOptions(120, 1, glyphs, UpstreamGoneBranches: new HashSet<string> { "topic" });

            var line = GraphRowRenderer.Render(graph[0], options, false);

            Assert.Contains($" {glyphs.LocalBranch}topic {glyphs.UpstreamGone}", line.Text, StringComparison.Ordinal);
            Assert.StartsWith(" ", glyphs.UpstreamGone, StringComparison.Ordinal);
        }
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

        Assert.StartsWith("●   ◉ main    ⎇ topic  tag: v1 release", row);
    }

    [Fact]
    public void GivenABranchTheLayoutDidNotSee_WhenRendering_ThenItsChipTakesTheHashColourOfItsName()
    {
        const string known = "known";
        var added = Enumerable.Range(0, 50).Select(number => $"added-{number}")
            .First(name => BranchColors.PreferredSlot(name) != 3);
        var commit = CommitFactory.Commit(
            1,
            "tip",
            [],
            new CommitDecoration($"refs/heads/{known}", known, RefKind.LocalBranch, false),
            new CommitDecoration($"refs/remotes/origin/{added}", $"origin/{added}", RefKind.RemoteBranch, false));
        var row = new GraphRow(commit.Id, 0, 1, [], 1, 3, true, [new BranchSlot(known, 3)]);
        var entry = new GraphEntry(
            commit.Subject,
            commit.AuthorName,
            commit.AuthorTime,
            commit.Decorations,
            row);

        var line = GraphRowRenderer.Render(entry, new GraphRowOptions(120, 1, GraphGlyphSet.Unicode), false);

        var labels = line.Segments.Where(segment => segment.Role == SegmentRole.LaneLabel).ToArray();
        Assert.Equal(3, labels[0].ColorIndex);
        Assert.Equal(BranchColors.PreferredSlot(added), labels[1].ColorIndex);
    }

    [Fact]
    public void GivenBranchLabels_WhenRendering_ThenEachBranchChipCarriesItsOwnColourAndTagsDoNot()
    {
        // A name whose colour is not the first one, so a label that lost its colour index would show.
        var current = Enumerable.Range(0, 50).Select(number => $"branch-{number}")
            .First(name => BranchColors.PreferredSlot(name) != 0);
        var commit = CommitFactory.Commit(
            1,
            "release",
            [],
            new CommitDecoration($"refs/heads/{current}", current, RefKind.LocalBranch, true),
            new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false),
            new CommitDecoration("refs/remotes/origin/other", "origin/other", RefKind.RemoteBranch, false),
            new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false));
        var graph = CommitFactory.Graph(commit);
        var options = new GraphRowOptions(120, 1, GraphGlyphSet.Unicode);

        var line = GraphRowRenderer.Render(graph[0], options, false);

        var slot = BranchColors.PreferredSlot(current);
        Assert.NotEqual(0, slot);
        Assert.Equal(slot, graph[0].Row.NodeColor);
        var labels = line.Segments.Where(segment => segment.Role == SegmentRole.LaneLabel).ToArray();
        var glyphs = GraphGlyphSet.Unicode;
        Assert.Equal(
            [$" {glyphs.CurrentBranch}{current} ", $" {glyphs.LocalBranch}topic ", " origin/other "],
            labels.Select(segment => segment.Text));
        Assert.Equal(slot, labels[0].ColorIndex);
        Assert.Equal(3, labels.Select(segment => segment.ColorIndex).Distinct().Count());
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.Tag);
    }

    [Fact]
    public void GivenTwoBranchesOnTwoLanes_WhenRendering_ThenEachLaneLineHasItsBranchColour()
    {
        var alpha = CommitFactory.Commit(
            1, "alpha tip", [3], new CommitDecoration("refs/heads/alpha", "alpha", RefKind.LocalBranch, false));
        var beta = CommitFactory.Commit(
            2, "beta tip", [3], new CommitDecoration("refs/heads/beta", "beta", RefKind.LocalBranch, false));
        var root = CommitFactory.Commit(3, "root", []);
        var graph = CommitFactory.Graph(alpha, beta, root);
        var options = new GraphRowOptions(80, 2, GraphGlyphSet.Unicode);

        var line = GraphRowRenderer.Render(graph[1], options, false);

        var laneSegments = line.Segments.Where(segment => segment.Role == SegmentRole.Lane).ToArray();
        Assert.Equal(graph[0].Row.NodeColor, laneSegments[0].ColorIndex);
        Assert.Equal(graph[1].Row.NodeColor, laneSegments[^1].ColorIndex);
        Assert.NotEqual(graph[0].Row.NodeColor, graph[1].Row.NodeColor);
    }

    [Fact]
    public void GivenABranchTipAheadOfAnotherBranchTipOnTheSameChain_WhenRendering_ThenBothLabelsSitOnOneStraightLine()
    {
        var feature = CommitFactory.Commit(
            1, "feature work", [2], new CommitDecoration("refs/heads/feature", "feature", RefKind.LocalBranch, false));
        var main = CommitFactory.Commit(
            2, "main work", [3], new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true));
        var root = CommitFactory.Commit(3, "root", []);

        var rows = Rows(GraphGlyphSet.Unicode, 60, feature, main, root);

        Assert.StartsWith("●   ⎇ feature  feature work", rows[0]);
        Assert.StartsWith("●   ◉ main", rows[1]);
        Assert.StartsWith("●  root", rows[2]);
    }

    [Fact]
    public void GivenAFeatureBranchForkedFromMain_WhenRendering_ThenItKeepsItsLaneToTheForkWhereItJoinsMain()
    {
        var main = CommitFactory.Commit(
            1, "main tip", [4], new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true));
        var tip = CommitFactory.Commit(
            2, "feature tip", [3], new CommitDecoration("refs/heads/feature", "feature", RefKind.LocalBranch, false));
        var first = CommitFactory.Commit(3, "feature first", [4]);
        var fork = CommitFactory.Commit(4, "fork point", [5]);
        var root = CommitFactory.Commit(5, "root", []);

        var rows = Rows(GraphGlyphSet.Unicode, 60, main, tip, first, fork, root);

        Assert.StartsWith("●   ◉ main  main tip", rows[0]);
        Assert.StartsWith("│ ●   ⎇ feature  feature tip", rows[1]);
        Assert.StartsWith("│ ├  feature first", rows[2]);
        Assert.StartsWith("●─╯  fork point", rows[3]);
        Assert.StartsWith("●  root", rows[4]);
        var graph = CommitFactory.Graph(main, tip, first, fork, root);
        Assert.Equal(graph[0].Row.NodeColor, graph[3].Row.NodeColor);
        Assert.Equal(graph[0].Row.NodeColor, graph[4].Row.NodeColor);
        Assert.Equal(graph[1].Row.NodeColor, graph[2].Row.NodeColor);
        Assert.NotEqual(graph[0].Row.NodeColor, graph[1].Row.NodeColor);

        // The label of the feature tip is a box in the feature lane's colour, the curve at the fork is drawn in it too
        // while the node of the fork keeps main's colour.
        var options = new GraphRowOptions(60, 2, GraphGlyphSet.Unicode);
        var tipLine = GraphRowRenderer.Render(graph[1], options, false);
        var label = tipLine.Segments.Single(segment => segment.Role == SegmentRole.LaneLabel);
        Assert.Equal(graph[1].Row.NodeColor, label.ColorIndex);
        var forkLine = GraphRowRenderer.Render(graph[3], options, false);
        var curve = forkLine.Segments.Where(segment => segment.Role == SegmentRole.Lane).ToArray();
        Assert.Equal(graph[0].Row.NodeColor, curve[0].ColorIndex);
        Assert.Contains("●", curve[0].Text, StringComparison.Ordinal);
        Assert.Equal(graph[1].Row.NodeColor, curve[^1].ColorIndex);
        Assert.Contains("╯", curve[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAMainTipOlderThanTheFeatureTip_WhenRendering_ThenTheLeftmostLaneGoesToTheBottomAndMainBendsIn()
    {
        var tip = CommitFactory.Commit(
            1, "feature tip", [2], new CommitDecoration("refs/heads/feature", "feature", RefKind.LocalBranch, false));
        var first = CommitFactory.Commit(2, "feature first", [4]);
        var main = CommitFactory.Commit(
            3, "main tip", [4], new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true));
        var fork = CommitFactory.Commit(4, "fork point", [5]);
        var root = CommitFactory.Commit(5, "root", []);

        var rows = Rows(GraphGlyphSet.Unicode, 60, tip, first, main, fork, root);

        Assert.StartsWith("●   ⎇ feature  feature tip", rows[0]);
        Assert.StartsWith("├  feature first", rows[1]);
        Assert.StartsWith("│ ●   ◉ main", rows[2]);
        Assert.StartsWith("●─╯  fork point", rows[3]);
        Assert.StartsWith("●  root", rows[4]);
        var graph = CommitFactory.Graph(tip, first, main, fork, root);
        Assert.Equal(graph[0].Row.NodeColor, graph[3].Row.NodeColor);
        Assert.NotEqual(graph[2].Row.NodeColor, graph[3].Row.NodeColor);
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

        Assert.StartsWith("*   HEAD -> main (r)  release", row);
    }

    [Fact]
    public void GivenDetachedHead_WhenRendering_ThenHeadIsListed()
    {
        var commit = CommitFactory.Commit(1, "x", [], new CommitDecoration("HEAD", "HEAD", RefKind.Head, true));

        Assert.StartsWith("●  HEAD x", Rows(GraphGlyphSet.Unicode, 40, commit)[0]);
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
            commits.Add(CommitFactory.Commit(100 + lane, $"branch {lane}", [lane]));
        }

        var graph = CommitFactory.Graph([.. commits]);
        var options = new GraphRowOptions(30, 2, GraphGlyphSet.Unicode);

        for (var index = 0; index < graph.Count; index++)
        {
            Assert.Equal(30, TextWidth.Of(GraphRowRenderer.Render(graph[index], options, false).Text));
        }
    }

    [Fact]
    public void GivenAMarkedCommit_WhenRendering_ThenOnlyItsRowStartsItsMessageWithTheMark()
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(2, "second", [1]),
            CommitFactory.Commit(1, "first", []));
        var options = new GraphRowOptions(60, 1, GraphGlyphSet.Ascii, MarkedCommit: CommitFactory.Id(1));

        var rows = Enumerable.Range(0, graph.Count)
            .Select(index => GraphRowRenderer.Render(graph[index], options, false).Text)
            .ToArray();

        Assert.DoesNotContain("[mark]", rows[0], StringComparison.Ordinal);
        Assert.Contains("[mark] first", rows[1], StringComparison.Ordinal);
        Assert.Equal(60, TextWidth.Of(rows[1]));
    }

    [Fact]
    public void GivenAMarkedCommit_WhenReadingTheJumpText_ThenTheMarkIsPartOfIt()
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "first", []));
        var options = new GraphRowOptions(60, 1, GraphGlyphSet.Ascii, MarkedCommit: CommitFactory.Id(1));

        Assert.Equal("[mark] first", GraphRowRenderer.MessageText(graph[0], options));
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
    public void GivenBodyHitWithTextOnlyInTheBody_WhenRendering_ThenTheWholeSubjectIsMarked()
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(1, "Fix typo", []),
            CommitFactory.Commit(2, "Other", []));
        var hits = new HashSet<ObjectId> { graph[0].Id };
        var options = new GraphRowOptions(
            60,
            1,
            GraphGlyphSet.Unicode,
            new SearchQuery(SearchField.Body, "closes", hits));

        var hit = GraphRowRenderer.Render(graph[0], options, false);
        var miss = GraphRowRenderer.Render(graph[1], options, false);

        Assert.Contains(hit.Segments, segment => segment.Role == SegmentRole.SearchMatch && segment.Text == "Fix typo");
        Assert.DoesNotContain(miss.Segments, segment => segment.Role == SegmentRole.SearchMatch);
    }

    [Fact]
    public void GivenAJumpLabel_WhenRendering_ThenTheMatchIsHighlightedAndTheLabelReplacesTheNextCharacter()
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "Fix typo", []));
        var options = new GraphRowOptions(60, 1, GraphGlyphSet.Unicode);

        var line = GraphRowRenderer.Render(graph[0], options, false, new JumpLabel(0, 0, 3, 'k'));

        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.SearchMatch && segment.Text == "Fix");
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.JumpLabel && segment.Text == "k");
        Assert.Contains("Fixktypo", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAJumpLabelAtTheEndOfTheSubject_WhenRendering_ThenTheLabelIsAppended()
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "Fix", []));
        var options = new GraphRowOptions(60, 1, GraphGlyphSet.Unicode);

        var line = GraphRowRenderer.Render(graph[0], options, false, new JumpLabel(0, 0, 3, 'k'));

        Assert.Contains("Fixk", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenHash_WhenRendering_ThenItHasItsOwnRoleAndLanesCarryTheirLane()
    {
        var graph = CommitFactory.Graph(Diamond());
        var options = new GraphRowOptions(100, 2, GraphGlyphSet.Unicode);

        var line = GraphRowRenderer.Render(graph[2], options, false);

        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.Hash && segment.Text == "0000003");
        var nodeColor = graph[2].Row.NodeColor;
        Assert.Contains(
            line.Segments,
            segment => segment.Role == SegmentRole.Lane
                && segment.ColorIndex == nodeColor
                && segment.Text.Contains('├', StringComparison.Ordinal));
    }

    // Newest first: B's tip and the main tip both wait for "merge A", which also starts a lane for the merged-in branch A.
    private static CommitInfo[] EndAndStartOnOneSide() =>
    [
        CommitFactory.Commit(8, "b2", [7]),
        CommitFactory.Commit(9, "m3", [6]),
        CommitFactory.Commit(7, "b1", [6]),
        CommitFactory.Commit(6, "merge A", [5, 4]),
        CommitFactory.Commit(5, "m2", [3]),
        CommitFactory.Commit(4, "a2", [2]),
        CommitFactory.Commit(3, "m1", [1]),
        CommitFactory.Commit(2, "a1", [1]),
        CommitFactory.Commit(1, "root", []),
    ];

    private static string[] DiagonalRows(GraphGlyphSet glyphs, params CommitInfo[] commits)
    {
        var graph = CommitFactory.Graph(commits);
        var options = new GraphRowOptions(60, GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, 60), glyphs);
        return [.. Enumerable.Range(0, graph.Count).Select(index => GraphRowRenderer.Render(graph, index, options, false).Text.TrimEnd())];
    }

    [Fact]
    public void GivenALaneEndingAndAnotherStartingAtOneCommit_WhenRendering_ThenTheArrivalKeepsItsBendAndTheDepartureIsADiagonalBelow()
    {
        var rows = DiagonalRows(GraphGlyphSet.Unicode, EndAndStartOnOneSide());

        Assert.StartsWith("├ │", rows[2], StringComparison.Ordinal);
        Assert.StartsWith("○─╯", rows[3], StringComparison.Ordinal);
        Assert.StartsWith("├ \\", rows[4], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheAsciiSet_WhenRenderingTheCommitWithAnEndingAndAStartingLane_ThenTheDepartureIsABackslash()
    {
        var rows = DiagonalRows(GraphGlyphSet.Ascii, EndAndStartOnOneSide());

        Assert.StartsWith("o-/", rows[3], StringComparison.Ordinal);
        Assert.StartsWith("| \\", rows[4], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheCommitIsTheLastLoadedRow_WhenRendering_ThenTheDepartureBendStays()
    {
        var commits = EndAndStartOnOneSide()[..4];

        var rows = DiagonalRows(GraphGlyphSet.Unicode, commits);

        Assert.StartsWith("○─┤", rows[3], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenDiagonalRows_WhenRendering_ThenEveryRowKeepsTheWidthOfItsTextColumns()
    {
        var graph = CommitFactory.Graph(EndAndStartOnOneSide());
        var options = new GraphRowOptions(60, GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, 60), GraphGlyphSet.Unicode);

        var widths = Enumerable.Range(0, graph.Count)
            .Select(index => TextWidth.Of(GraphRowRenderer.Render(graph, index, options, false).Text));

        Assert.All(widths, width => Assert.Equal(60, width));
    }

    [Fact]
    public void GivenTheCommitBelowTheNodeSitsOnTheNewLane_WhenRendering_ThenTheDiagonalIsTheCommitAndHasNoTick()
    {
        var rows = DiagonalRows(
            GraphGlyphSet.Unicode,
            CommitFactory.Commit(8, "b2", [7]),
            CommitFactory.Commit(7, "b1", [6]),
            CommitFactory.Commit(10, "m4", [9]),
            CommitFactory.Commit(9, "m3", [6]),
            CommitFactory.Commit(6, "merge A", [5, 4]),
            CommitFactory.Commit(4, "a2", [2]),
            CommitFactory.Commit(5, "m2", [3]),
            CommitFactory.Commit(2, "a1", [1]),
            CommitFactory.Commit(3, "m1", [1]),
            CommitFactory.Commit(1, "root", []));

        Assert.StartsWith("○─╯", rows[4], StringComparison.Ordinal);
        Assert.StartsWith("│ \\", rows[5], StringComparison.Ordinal);
    }
}
