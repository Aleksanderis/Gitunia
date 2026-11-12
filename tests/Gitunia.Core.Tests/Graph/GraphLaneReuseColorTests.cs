using Gitunia.Core.Graph;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class GraphLaneReuseColorTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(12)]
    public void GivenALaneEndingAndAnotherStartingInTheSameLane_WhenLayingOut_ThenTheTwoLinesHaveDifferentColours(int colorCount)
    {
        // Newest first: 8 and 9 both end at 6, which also starts a lane for its second parent 4.
        var commits = new[]
        {
            Commit(8, 7),
            Commit(9, 6),
            Commit(7, 6),
            Commit(6, 5, 4),
            Commit(5, 3),
            Commit(4, 2),
            Commit(3, 1),
            Commit(2, 1),
            Commit(1),
        };

        var rows = new GraphLayoutBuilder(null, colorCount).AppendRange(commits);

        var reused = rows.Where(row => ReusedLanes(row).Any()).ToArray();
        Assert.NotEmpty(reused);
        foreach (var row in reused)
        {
            foreach (var lane in ReusedLanes(row))
            {
                var ended = row.Edges.Single(edge => edge.Kind == GraphEdgeKind.Incoming && edge.FromLane == lane);
                var started = row.Edges.Single(edge => edge.Kind == GraphEdgeKind.Outgoing && edge.ToLane == lane);
                Assert.NotEqual(ended.Color, started.Color);
            }
        }
    }

    // Lanes other than the node's where a line ends and another one starts in the same row.
    private static IEnumerable<int> ReusedLanes(GraphRow row) =>
        row.Edges
            .Where(edge => edge.Kind == GraphEdgeKind.Incoming && edge.FromLane != row.Lane)
            .Select(edge => edge.FromLane)
            .Where(lane => row.Edges.Any(edge => edge.Kind == GraphEdgeKind.Outgoing && edge.ToLane == lane));
}
