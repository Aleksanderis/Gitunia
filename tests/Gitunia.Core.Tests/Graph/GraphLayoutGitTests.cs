using Gitunia.Core.Graph;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories;
using Xunit;

namespace Gitunia.Core.Tests.Graph;

public class GraphLayoutGitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(CommitOrder.Topological)]
    [InlineData(CommitOrder.ByDate)]
    public async Task GivenBranchyRepository_WhenLayingOutTheRealLog_ThenOrderMatchesGitLogGraphAndTopologyMatchesGitsParentLinks(CommitOrder order)
    {
        using var repository = GraphGitRepository.CreateBranchy();
        var repo = await Repo.OpenAsync(repository.CreateRunner(), repository.Path, Token);
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

        // Parent links as git itself reports them are exactly what the rows' outgoing edges were validated against.
        var parentLines = repository.Git("log", "--all", "--format=%H %P")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(line => line.Split(' ')[0], line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray());
        Assert.All(commits, commit => Assert.Equal(parentLines[commit.Id.ToString()], commit.Parents.Select(parent => parent.ToString())));
    }
}
