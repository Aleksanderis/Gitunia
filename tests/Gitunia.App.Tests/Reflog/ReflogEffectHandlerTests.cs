using Gitunia.App.Reflog;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Reflog;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Reflog;
using Gitunia.Git.Reflog.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Reflog;

// Real git on temporary repositories: the handler is the only place that reads the reflog.
public class ReflogEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenCommitsAmendResetAndCheckout_WhenReadingHead_ThenEveryActionIsListedNewestFirst()
    {
        using var repository = TestRepositories.Linear();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        repository.Git("commit", "--allow-empty", "--amend", "-m", "amended");
        repository.Git("reset", "--hard", first);
        repository.CheckoutNewBranch("topic");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ReadReflogEffect(Tab, location, "HEAD", "HEAD", 7, false), null,
            TestContext.Current.CancellationToken);

        var loaded = Assert.IsType<ReflogLoaded>(result);
        Assert.Equal(Tab, loaded.TabId);
        Assert.Equal(7, loaded.Generation);
        Assert.Equal("HEAD", loaded.Title);
        Assert.False(loaded.Refresh);
        Assert.Equal("checkout", loaded.Entries[0].Action);
        Assert.Equal("reset", loaded.Entries[1].Action);
        Assert.Equal("commit (amend)", loaded.Entries[2].Action);
    }

    [Fact]
    public async Task GivenABranch_WhenReadingItsReflog_ThenTheEntriesAreThatBranchs()
    {
        using var repository = TestRepositories.Linear();
        repository.CheckoutNewBranch("topic");
        repository.Commit("on topic", new TestFile("b.txt", "b"));
        repository.Git("checkout", "-");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ReadReflogEffect(Tab, location, "refs/heads/topic", "topic", 1, true), null,
            TestContext.Current.CancellationToken);

        var loaded = Assert.IsType<ReflogLoaded>(result);
        Assert.Equal("topic", loaded.Title);
        Assert.True(loaded.Refresh);
        Assert.Equal(["commit", "branch"], loaded.Entries.Select(entry => entry.Action).Take(2));
    }

    [Fact]
    public async Task GivenAFailingReader_WhenReading_ThenTheFailureCarriesTheReasonAndNoEntries()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ReadReflogEffect(Tab, location, "HEAD", "HEAD", 1, false), new FailingReader(),
            TestContext.Current.CancellationToken);

        var failed = Assert.IsType<ReflogReadFailed>(result);
        Assert.StartsWith("Could not read the reflog of HEAD:", failed.Text, StringComparison.Ordinal);
        Assert.Contains("boom", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReading_ThenTheResultIsCancellationNotAFailure()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var result = await RunAsync(
            new ReadReflogEffect(Tab, location, "HEAD", "HEAD", 1, false),
            new CancellingReader(),
            source.Token);

        Assert.IsType<ReflogReadCancelled>(result);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunAsync(
        ReadReflogEffect effect,
        IReflogReader? reader,
        CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        new ReflogEffectHandler(reader ?? TestGit.Services.Reflog, dispatcher).Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }

    private sealed class FailingReader : IReflogReader
    {
        public Task<ParseResult<ReflogEntry>> ReadAsync(
            RepositoryLocation location,
            string reference,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromException<ParseResult<ReflogEntry>>(new InvalidOperationException("boom"));
    }

    private sealed class CancellingReader : IReflogReader
    {
        public Task<ParseResult<ReflogEntry>> ReadAsync(
            RepositoryLocation location,
            string reference,
            int limit,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new ParseResult<ReflogEntry>([], []));
        }
    }
}
