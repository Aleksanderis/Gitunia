using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.App.Tests.Tabs;

public class TabSessionTests
{
    [Fact]
    public void GivenFailedLoad_WhenTheGraphIsReplaced_ThenTheLoadErrorIsCleared()
    {
        using var session = NewSession();
        session.Fail("boom");
        Assert.Equal("boom", session.LoadError);

        session.ReplaceGraph([]);

        Assert.Equal(string.Empty, session.LoadError);
    }

    [Fact]
    public void GivenCutLog_WhenLoadedExpandedAndRefreshed_ThenCutMarkerFollowsTheCutState()
    {
        using var session = NewSession();
        session.ExpandRequested += () => { };

        session.Truncate(500);
        Assert.False(session.ShowsCutMarker);
        session.Complete();
        Assert.True(session.ShowsCutMarker);

        session.RequestExpand(true);
        Assert.False(session.ShowsCutMarker);
        session.CompleteExpansion();
        Assert.False(session.ShowsCutMarker);

        session.ReplaceGraph([], 500);
        Assert.True(session.ShowsCutMarker);
        session.ReplaceGraph([]);
        Assert.False(session.ShowsCutMarker);
    }

    [Fact]
    public void GivenSubclass_WhenRowsAppendedOrCutMarkerChanges_ThenTheRowsHookRuns()
    {
        using var session = new CountingSession(NewParts());

        session.Append([]);
        Assert.Equal(1, session.RowsChanges);
        session.Truncate(10);
        session.Complete();
        Assert.Equal(2, session.RowsChanges);
        session.Complete();
        Assert.Equal(2, session.RowsChanges);
        session.ReplaceGraph([]);
        Assert.Equal(3, session.RowsChanges);
    }

    [Fact]
    public void GivenSubclass_WhenStatusReadUnchanged_ThenTheHookAndTheEventRun()
    {
        using var session = new CountingSession(NewParts());
        var raised = 0;
        session.StatusRead += () => raised++;

        session.NotifyStatusRead();

        Assert.Equal(1, session.StatusReads);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void GivenSession_WhenDisposed_ThenItsCancellationIsCancelled()
    {
        var session = NewSession();
        var token = session.Cancellation.Token;

        session.Dispose();

        Assert.True(token.IsCancellationRequested);
    }

    private static TabSessionParts NewParts()
    {
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        var store = new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero);

        return new TabSessionParts(new TabId(1), store);
    }

    private static TabSession NewSession() => new TabSessionFactory().Create(NewParts());

    private sealed class CountingSession(TabSessionParts parts) : TabSession(parts)
    {
        public int RowsChanges { get; private set; }

        public int StatusReads { get; private set; }

        protected override void OnGraphRowsChanged() => RowsChanges++;

        protected override void OnStatusRead() => StatusReads++;
    }
}
