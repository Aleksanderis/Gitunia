using Gitunia.Core.Graph;
using Gitunia.Core.Tests.Graph;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Tests.Search;

/// <summary>A linear graph for search tests: row <c>i</c> is commit <c>i + 1</c> with the given subject and author.</summary>
public static class SearchTestGraph
{
    public static CommitGraph Build(params SearchTestRow[] rows)
    {
        var commits = rows
            .Select((row, index) => new CommitInfo(
                GraphTestData.Id(index + 1),
                index + 1 < rows.Length ? [GraphTestData.Id(index + 2)] : [],
                row.Author,
                "author@example.com",
                DateTimeOffset.UnixEpoch,
                row.Subject,
                []))
            .ToArray();
        var layout = GraphLayout.Compute(commits);
        var graph = new CommitGraph();
        graph.AddRange(commits.Select((commit, index) =>
            new GraphEntry(commit.Id, commit.Subject, commit.AuthorName, commit.AuthorTime, commit.Decorations, layout[index])).ToArray());
        return graph;
    }

    public static CommitGraph Numbered(int count) =>
        Build([.. Enumerable.Range(1, count).Select(number => new SearchTestRow($"Commit #{number}", number % 3 == 0 ? "Alice" : "Bob"))]);
}
