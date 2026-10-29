using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class UpstreamGoneFrameTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenBranchDeletedOnRemoteAndPruned_WhenPrintingUnicode_ThenSidebarAndGraphMarkOnlyThatBranch()
    {
        using var repository = RepositoryWithGoneBranch(prune: true);

        var text = await PrintAsync(repository, GraphGlyphSet.Unicode);

        Assert.Equal(1, CountOf(text, "topic ✗"));
        Assert.Equal(1, CountOf(text, "topic  ✗"));
        Assert.Equal(2, CountOf(text, "✗"));
    }

    [Fact]
    public async Task GivenBranchDeletedOnRemoteAndPruned_WhenPrintingAscii_ThenTheAsciiMarkerShows()
    {
        using var repository = RepositoryWithGoneBranch(prune: true);

        var text = await PrintAsync(repository, GraphGlyphSet.Ascii);

        Assert.Equal(1, CountOf(text, "topic (x)"));
        Assert.Equal(1, CountOf(text, "topic  (x)"));
        Assert.Equal(2, CountOf(text, "(x)"));
        Assert.DoesNotContain("✗", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenBranchDeletedOnRemoteButNotFetched_WhenPrinting_ThenNoMarkerShows()
    {
        using var repository = RepositoryWithGoneBranch(prune: false);

        var text = await PrintAsync(repository, GraphGlyphSet.Unicode);

        Assert.DoesNotContain("✗", text, StringComparison.Ordinal);
    }

    private static TestRepository RepositoryWithGoneBranch(bool prune)
    {
        var repository = TestRepository.Create();
        repository.Commit("first", new TestFile("a.txt", "one\n"));
        repository.Branch("topic");
        repository.Branch("kept");
        repository.Commit("second", new TestFile("a.txt", "two\n"));
        repository.AddOrigin().PushWithUpstream("topic").PushWithUpstream("kept");
        repository.GitIn(repository.OriginPath, ["branch", "-D", "topic"]);
        if (prune)
        {
            repository.Git("fetch", "-q", "--prune", "origin");
        }

        return repository;
    }

    private static int CountOf(string text, string needle) => text.Split(needle).Length - 1;

    private static async Task<string> PrintAsync(TestRepository repository, GraphGlyphSet glyphs)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        using var writer = new StringWriter();
        await new FramePrinter(TestGit.Services)
            .PrintAsync(location, 100, 30, glyphs, writer, null, BottomTab.Details, null, Token);

        return writer.ToString();
    }
}
