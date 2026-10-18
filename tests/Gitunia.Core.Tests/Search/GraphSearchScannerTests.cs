using Gitunia.Core.Search;
using Gitunia.Core.Search.Models;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Core.Tests.Search;

public class GraphSearchScannerTests
{
    [Fact]
    public void GivenMessageQuery_WhenScanning_ThenMatchesIgnoreCaseAndSubstring()
    {
        var graph = SearchTestGraph.Build(new SearchTestRow("Fix the Parser", "Bob"), new SearchTestRow("Add tests", "Bob"), new SearchTestRow("fix typo", "Alice"));

        var result = GraphSearchScanner.Scan(new SearchQuery(SearchField.Message, "FIX"), graph, 0, 100);

        Assert.Equal([0, 2], result.Matches);
        Assert.Equal(3, result.NextIndex);
    }

    [Fact]
    public void GivenBodyQuery_WhenScanning_ThenOnlyRowsInTheHitsMatchAndNoneBeforeTheAnswer()
    {
        var graph = SearchTestGraph.Build(
            new SearchTestRow("one", "Bob"),
            new SearchTestRow("two", "Bob"),
            new SearchTestRow("three", "Bob"));
        Assert.True(ObjectId.TryParse("0000000000000000000000000000000000000002", out var second));

        var answered = new SearchQuery(SearchField.Body, "closes", new HashSet<ObjectId> { second });

        Assert.Equal([1], GraphSearchScanner.Scan(answered, graph, 0, 100).Matches);
        Assert.Empty(GraphSearchScanner.Scan(answered with { BodyHits = null }, graph, 0, 100).Matches);
    }

    [Fact]
    public void GivenAuthorAndHashQueries_WhenScanning_ThenTheyMatchThoseFields()
    {
        var graph = SearchTestGraph.Build(new SearchTestRow("one", "Bob Builder"), new SearchTestRow("two", "Alice"), new SearchTestRow("three", "bobby"));

        Assert.Equal([0, 2], GraphSearchScanner.Scan(new SearchQuery(SearchField.Author, "bob"), graph, 0, 100).Matches);
        Assert.Equal([1], GraphSearchScanner.Scan(new SearchQuery(SearchField.Hash, "0000000000000000000000000000000000000002"), graph, 0, 100).Matches);
        Assert.Empty(GraphSearchScanner.Scan(new SearchQuery(SearchField.Hash, "zz"), graph, 0, 100).Matches);
    }

    [Fact]
    public void GivenSmallSlices_WhenScanningRepeatedly_ThenTheResultEqualsOneFullScan()
    {
        var graph = SearchTestGraph.Numbered(1000);
        var query = new SearchQuery(SearchField.Message, "#7");
        var expected = GraphSearchScanner.Scan(query, graph, 0, 1_000_000).Matches;

        var matches = new List<int>();
        var next = 0;
        while (next < graph.Count)
        {
            var slice = GraphSearchScanner.Scan(query, graph, next, 64);
            matches.AddRange(slice.Matches);
            next = slice.NextIndex;
        }

        Assert.Equal(expected, matches);
        Assert.NotEmpty(matches);
    }

    [Fact]
    public void GivenStartAtTheEnd_WhenScanning_ThenNothingIsFoundAndTheIndexStays()
    {
        var graph = SearchTestGraph.Numbered(5);

        var result = GraphSearchScanner.Scan(new SearchQuery(SearchField.Message, "Commit"), graph, 5, 10);

        Assert.Empty(result.Matches);
        Assert.Equal(5, result.NextIndex);
    }
}
