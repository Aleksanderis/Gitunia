using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class GraphColumnsFrameTests
{
    [Fact]
    public void GivenCommits_WhenRenderingAt80_ThenAllColumnsLineUp()
    {
        Assert.Equal(
            [
                "●  Add the graph columns                     Ada Lovelace     2026-10-13 0000003",
                "●  修正バグ in the parser                    山田太郎         2026-10-12 0000002",
                "●  A message that is much too long for the … Alexander the G… 2026-10-11 0000001",
            ],
            Trimmed(80));
    }

    [Fact]
    public void GivenCommits_WhenRenderingAt120_ThenMessageAbsorbsTheExtraWidth()
    {
        Assert.Equal(
            [
                "●  Add the graph columns                                                             Ada Lovelace     2026-10-13 0000003",
                "●  修正バグ in the parser                                                            山田太郎         2026-10-12 0000002",
                "●  A message that is much too long for the column we have for it, so it is cut with… Alexander the G… 2026-10-11 0000001",
            ],
            Trimmed(120));
    }

    [Fact]
    public void GivenCommits_WhenRenderingAt200_ThenMessageTakesTheWholeWidthAndAuthorIsWider()
    {
        var rows = Trimmed(200);

        Assert.Equal(200, TextWidth.Of(Rows(200)[2]));
        Assert.StartsWith("●  A message that is much too long for the column we have for it, so it is cut with an ellipsis", rows[2], StringComparison.Ordinal);
        Assert.EndsWith("Alexander the Great… 2026-10-11 0000001", rows[2], StringComparison.Ordinal);
        Assert.EndsWith("Ada Lovelace         2026-10-13 0000003", rows[0], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNarrowingTerminal_WhenRendering_ThenHashGoesFirstThenDateThenAuthor()
    {
        Assert.Equal("●  A message that is much too lon… Alexander the G… 2026-10-11 0000001", Trimmed(70)[2]);
        Assert.Equal("●  A message that is much too long for the… Alexander the G…", Trimmed(60)[2]);
        Assert.Equal("●  A message that is much too long fo… Alexander the G…", Trimmed(55)[2]);
        Assert.Equal("●  A message that is much too long for the c…", Trimmed(45)[2]);
    }

    [Fact]
    public void GivenTerminalThatWidensAgain_WhenRendering_ThenDroppedColumnsComeBack()
    {
        Assert.DoesNotContain("2026-10-11", Trimmed(60)[2], StringComparison.Ordinal);
        Assert.Contains("2026-10-11", Trimmed(65)[2], StringComparison.Ordinal);
        Assert.DoesNotContain("0000001", Trimmed(65)[2], StringComparison.Ordinal);
        Assert.Contains("0000001", Trimmed(70)[2], StringComparison.Ordinal);
        Assert.DoesNotContain("2026-10-11", Trimmed(60)[2], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenLateEveningCommitWestOfUtc_WhenRendering_ThenTheAuthorsLocalDateIsShown()
    {
        var graph = CommitFactory.Graph(
            CommitFactory.CommitBy(1, "late", [], "Ada", new DateTimeOffset(2026, 10, 13, 23, 30, 0, TimeSpan.FromHours(-5))));
        var options = new GraphRowOptions(100, 1, GraphGlyphSet.Unicode);

        Assert.Contains("2026-10-13", GraphRowRenderer.Render(graph[0], options, false).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnyWidth_WhenRendering_ThenEveryRowFillsTheWidthExactly()
    {
        for (var width = 10; width <= 210; width++)
        {
            Assert.All(Rows(width), row => Assert.Equal(width, TextWidth.Of(row)));
        }
    }

    private static string[] Trimmed(int width) => [.. Rows(width).Select(row => row.TrimEnd())];

    private static string[] Rows(int width)
    {
        var graph = CommitFactory.Graph(
            CommitFactory.CommitBy(3, "Add the graph columns", [2], "Ada Lovelace", new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.FromHours(2))),
            CommitFactory.CommitBy(2, "修正バグ in the parser", [1], "山田太郎", new DateTimeOffset(2026, 10, 12, 23, 30, 0, TimeSpan.Zero)),
            CommitFactory.CommitBy(1, "A message that is much too long for the column we have for it, so it is cut with an ellipsis", [], "Alexander the Great of Macedon", new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero)));
        var options = new GraphRowOptions(width, 1, GraphGlyphSet.Unicode);
        return [.. Enumerable.Range(0, graph.Count).Select(index => GraphRowRenderer.Render(graph[index], options, false).Text)];
    }
}
