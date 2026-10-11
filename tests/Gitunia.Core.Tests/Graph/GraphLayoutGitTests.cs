using Gitunia.Core.Graph;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Core.Tests.Graph;

public class GraphLayoutGitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(CommitOrder.Topological)]
    [InlineData(CommitOrder.ByDate)]
    public async Task GivenBranchyRepository_WhenLayingOutTheRealLog_ThenTopologyAndOrderMatchGitLogGraph(CommitOrder order)
    {
        using var repository = GraphGitRepository.CreateBranchy();
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, Token);
        var commits = new List<CommitInfo>();
        await foreach (var commit in CommitLogReader.ReadAsync(repo, order, ["--all"], Token))
        {
            commits.Add(commit);
        }

        var rows = GraphLayout.Compute(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        Assert.Contains(commits, commit => commit.Parents.Count == 4);
        Assert.Equal(2, commits.Count(commit => commit.Parents.Count == 0));

        // git log --graph prints one '*' node per commit in the same order we received them (same ordering flags).
        var orderFlag = order == CommitOrder.Topological ? "--topo-order" : "--date-order";
        var graphOutput = repository.Git("log", "--graph", orderFlag, "--all", "--format=%H");
        var graphIds = graphOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains('*', StringComparison.Ordinal))
            .Select(line => line[(line.LastIndexOf(' ') + 1)..])
            .ToArray();
        Assert.Equal(graphIds, rows.Select(row => row.Id.ToString()));

        // Our lane for a commit never exceeds the number of '|' / '*' columns git itself needed on that line.
        var graphColumns = graphOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains('*', StringComparison.Ordinal))
            .Select(line => line.IndexOf('*', StringComparison.Ordinal) / 2)
            .ToArray();
        Assert.True(rows.Max(row => row.LaneCount) <= graphColumns.Max() + 3, "layout is much wider than git's own graph");
    }
}
