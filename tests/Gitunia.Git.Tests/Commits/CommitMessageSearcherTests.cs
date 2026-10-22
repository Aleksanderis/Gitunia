using Gitunia.Git.Commits;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitMessageSearcherTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly CommitMessageSearcher Searcher = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenTextOnlyInABody_WhenSearching_ThenOnlyThatCommitIsFoundIgnoringCase()
    {
        using var repository = TestRepository.Create();
        repository.Commit("First", new TestFile("a.txt", "1\n"));
        repository.Commit("Second\n\nRefs ISSUE-42 and more", new TestFile("a.txt", "2\n"));
        var withBody = repository.Head;
        repository.Commit("Third", new TestFile("a.txt", "3\n"));
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        var hits = await Searcher.SearchAsync(location, "issue-42", Token);

        Assert.Equal([withBody], hits.Select(id => id.ToString()));
    }

    [Fact]
    public async Task GivenPatternCharactersAndALeadingDash_WhenSearching_ThenTheyAreTakenLiterally()
    {
        using var repository = TestRepository.Create();
        repository.Commit("Plain", new TestFile("a.txt", "1\n"));
        repository.Commit("Dotted\n\nfix a.b and --all (x)", new TestFile("a.txt", "2\n"));
        var dotted = repository.Head;
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        Assert.Equal([dotted], (await Searcher.SearchAsync(location, "a.b", Token)).Select(id => id.ToString()));
        Assert.Empty(await Searcher.SearchAsync(location, "a.*b", Token));
        Assert.Equal([dotted], (await Searcher.SearchAsync(location, "--all (x)", Token)).Select(id => id.ToString()));
    }

    [Fact]
    public async Task GivenCommitOnAnotherBranch_WhenSearching_ThenItIsFoundButAStashIsNot()
    {
        using var repository = TestRepository.Create();
        repository.Commit("Base", new TestFile("a.txt", "1\n"));
        repository.Git("checkout", "-q", "-b", "topic");
        repository.Commit("Topic\n\nneedle on topic", new TestFile("b.txt", "1\n"));
        var topic = repository.Head;
        repository.Git("checkout", "-q", "main");
        repository.WriteFile("a.txt", "dirty\n");
        repository.Git("stash", "push", "-q", "-m", "needle in a stash");
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        var hits = await Searcher.SearchAsync(location, "needle", Token);

        Assert.Equal([topic], hits.Select(id => id.ToString()));
    }
}
