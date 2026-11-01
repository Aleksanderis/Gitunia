using Gitunia.App.Compare;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Tags;
using Gitunia.Core.Compare;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Xunit;

namespace Gitunia.App.Tests.Compare;

public sealed class CompareEffectHandlerTests : IDisposable, ITabSessions
{
    private static readonly TabId Tab = new(1);

    private readonly FakeCommitDetailsReader _reader = FakeCommitDetailsReader.Chain(3);
    private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly TabSession _session;

    public CompareEffectHandlerTests()
    {
        _session = new TabSession(
            new TabSessionParts(
                Tab,
                new CommitDetailsStore(
                    _reader,
                    FakeCommitDetailsReader.Location,
                    _pump.Post,
                    TimeSpan.FromMilliseconds(1))));
    }

    [Fact]
    public async Task GivenTheSelectedCommitIsLoaded_WhenStartingTheComparison_ThenTheStoreComparesAndStartedIsPosted()
    {
        var older = _reader.Commits[2];
        var newer = _reader.Commits[0];
        _reader.SetFiles(newer, older, "changed.txt");
        await SelectAsync(older);
        var handler = new CompareEffectHandler(this, _dispatcher);

        handler.Handle(new StartComparisonEffect(Tab, older, newer, older));
        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        var answer = await _dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new CompareStarted(Tab, older), answer);
        Assert.Equal(older, _session.Details.Current.Details!.CompareOlder);
        Assert.Equal(newer, _session.Details.Current.Details!.CompareNewer);
    }

    [Fact]
    public async Task GivenTheDetailsAreOfAnotherCommit_WhenStartingTheComparison_ThenNothingIsStartedOrPosted()
    {
        await SelectAsync(_reader.Commits[1]);
        var handler = new CompareEffectHandler(this, _dispatcher);

        handler.Handle(new StartComparisonEffect(Tab, _reader.Commits[2], _reader.Commits[0], _reader.Commits[2]));

        Assert.False(_session.Details.Current.Details!.IsComparison);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        wait.CancelAfter(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _dispatcher.NextAsync(wait.Token));
    }

    [Fact]
    public async Task GivenAComparisonIsShown_WhenClearing_ThenTheCommitsOwnChangesShowAgain()
    {
        var older = _reader.Commits[2];
        var newer = _reader.Commits[0];
        await SelectAsync(older);
        var handler = new CompareEffectHandler(this, _dispatcher);
        handler.Handle(new StartComparisonEffect(Tab, older, newer, older));
        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        handler.Handle(new ClearComparisonEffect(Tab));
        await _pump.RunUntilAsync(() => _session.Details.Current.Details is { IsComparison: false });

        Assert.Equal(older, _session.Details.Current.Commit);
    }

    [Fact]
    public void GivenATabWithoutASession_WhenHandlingAnEffect_ThenNothingHappens()
    {
        var handler = new CompareEffectHandler(new NoSessions(), _dispatcher);

        handler.Handle(new ClearComparisonEffect(Tab));
        handler.Handle(new StartComparisonEffect(Tab, _reader.Commits[2], _reader.Commits[0], _reader.Commits[2]));
    }

    public TabSession? Find(TabId tabId) => _session;

    public void Dispose() => _session.Dispose();

    private async Task SelectAsync(ObjectId commit)
    {
        _session.Details.Select(commit);
        await _pump.RunUntilAsync(
            () => _session.Details.Current.Commit == commit
                && _session.Details.Current.Status == CommitDetailsStatus.Loaded);
    }

    private sealed class NoSessions : ITabSessions
    {
        public TabSession? Find(TabId tabId) => null;
    }
}
