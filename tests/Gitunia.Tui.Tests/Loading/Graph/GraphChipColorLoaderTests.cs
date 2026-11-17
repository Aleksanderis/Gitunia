using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Graph;

// Chip colours are per branch, not per lane (#849), checked on real repositories through the loader and the renderer.
public class GraphChipColorLoaderTests
{
    [Fact]
    public async Task GivenBranchStackedOnMainWithRemote_WhenRendering_ThenChipsDifferAndMainWithOriginMainShareOne()
    {
        using var testRepository = TestRepository.CreateCommitted("root", new TestFile("a.txt", "1"));
        testRepository.Commit("add export", new TestFile("b.txt", "1"));
        testRepository.Git("update-ref", "refs/remotes/origin/main", "HEAD");
        testRepository.CheckoutNewBranch("test").Commit("wip test", new TestFile("c.txt", "1"));
        testRepository.Checkout("main");

        var entries = await LoadAsync(testRepository);

        var top = entries.Single(entry => entry.Subject == "wip test");
        var mid = entries.Single(entry => entry.Subject == "add export");
        Assert.Equal(top.Row.Lane, mid.Row.Lane);
        Assert.Equal(top.Row.NodeColor, mid.Row.NodeColor);
        var testChip = ChipColors(top).Single();
        var mainChip = ChipColors(mid).Single(chip => chip.Text.Contains("main", StringComparison.Ordinal));
        Assert.NotEqual(testChip.Color, mainChip.Color);
        Assert.Contains(GraphGlyphSet.Unicode.Remote, mainChip.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenRemoteBranchesOfTwoRemotesWithOneName_WhenRendering_ThenTheyShareTheColourOfThatName()
    {
        using var testRepository = TestRepository.CreateCommitted("root", new TestFile("a.txt", "1"));
        testRepository.Git("update-ref", "refs/remotes/origin/feature", "HEAD");
        testRepository.Git("update-ref", "refs/remotes/upstream/feature", "HEAD");
        testRepository.Git("update-ref", "refs/remotes/origin/other", "HEAD");

        var entries = await LoadAsync(testRepository);

        var chips = ChipColors(entries.Single()).ToArray();
        var feature = chips.Where(chip => chip.Text.Contains("feature", StringComparison.Ordinal)).ToArray();
        var other = chips.Single(chip => chip.Text.Contains("other", StringComparison.Ordinal));
        Assert.Equal(2, feature.Length);
        Assert.Equal(feature[0].Color, feature[1].Color);
        Assert.NotEqual(feature[0].Color, other.Color);
    }

    [Fact]
    public async Task GivenManyBranchesStackedOnOneLane_WhenRendering_ThenNeighbouringChipsNeverShareAColour()
    {
        using var testRepository = TestRepository.CreateCommitted("root", new TestFile("a.txt", "1"));
        for (var number = 0; number < 8; number++)
        {
            testRepository.Commit($"step {number}", new TestFile($"f{number}.txt", "1"));
            testRepository.Git("branch", $"stack-{number}");
        }

        var entries = await LoadAsync(testRepository);

        var colors = entries.SelectMany(ChipColors).Select(chip => chip.Color).ToArray();
        Assert.Equal(9, colors.Length);
        Assert.Equal(colors.Length, colors.Distinct().Count());
    }

    private static IEnumerable<Chip> ChipColors(GraphEntry entry)
    {
        var options = new GraphRowOptions(160, 1, GraphGlyphSet.Unicode);
        var line = GraphRowRenderer.Render(entry, options, false);

        return line.Segments.Where(segment => segment.Role == SegmentRole.LaneLabel)
            .Select(segment => new Chip(segment.Text, segment.ColorIndex));
    }

    private static async Task<List<GraphEntry>> LoadAsync(TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery
            .DiscoverAsync(testRepository.WorkingTreePath, CancellationToken.None);
        var entries = new List<GraphEntry>();
        await foreach (var batch in TestGit.Services.GraphLoader
            .StreamAsync(location, CommitOrder.Topological, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return entries;
    }

    private readonly record struct Chip(string Text, int Color);
}
