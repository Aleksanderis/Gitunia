using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Tabs;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tabs;

public class TabSessionTests
{
    [Fact]
    public void GivenCutLog_WhenLoadedExpandedAndRefreshed_ThenMarkerItemFollowsTheCutState()
    {
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        var store = new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero);
        using var session = new TerminalTabSession(new TabSessionParts(new TabId(1), store), GraphGlyphSet.Unicode);
        session.ExpandRequested += () => { };

        session.Truncate(500);
        Assert.False(session.Source.ShowsCutMarker);
        session.Complete();
        Assert.True(session.Source.ShowsCutMarker);
        Assert.Equal(1, session.Source.Count);
        Assert.Equal(session.Graph.Count, GraphListSource.CommitIndexOf(session.Source.Count - 1));

        session.RequestExpand(true);
        Assert.False(session.Source.ShowsCutMarker);
        session.CompleteExpansion();
        Assert.False(session.Source.ShowsCutMarker);
        Assert.Equal(0, session.Source.Count);

        session.ReplaceGraph([], 500);
        Assert.True(session.Source.ShowsCutMarker);
        session.ReplaceGraph([]);
        Assert.False(session.Source.ShowsCutMarker);
    }

    [Fact]
    public void GivenWorkingDiff_WhenStatusReadUnchanged_ThenTheWorkingDiffIsInvalidated()
    {
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        var details = new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero);
        var workingDiffStore = new WorkingDiffStore(TestGit.Services.Diff, location, InlineUi.Instance);
        using var session = new TerminalTabSession(
            new TabSessionParts(new TabId(1), details, WorkingDiffStore: workingDiffStore),
            GraphGlyphSet.Unicode);
        var generation = session.WorkingDiff!.Generation;

        session.NotifyStatusRead();

        Assert.Equal(generation + 1, session.WorkingDiff.Generation);
    }

    [Fact]
    public void GivenAButtonSourceOnTheFactory_WhenCreatingASession_ThenTheWorkingDiffDrawsItsButtons()
    {
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        var details = new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero);
        var workingDiffStore = new WorkingDiffStore(TestGit.Services.Diff, location, InlineUi.Instance);
        var buttons = new FixedGroupButtons(WorkingTreeButtons.Unstaged);
        var factory = new TerminalTabSessionFactory(GraphGlyphSet.Unicode, buttons);

        using var session = factory.Create(
            new TabSessionParts(new TabId(1), details, WorkingDiffStore: workingDiffStore));

        Assert.Same(buttons, session.WorkingDiff!.Source.GroupButtons);
    }

    [Fact]
    public void GivenTerminalSession_WhenDisposed_ThenTheBaseCancellationIsCancelled()
    {
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        var details = new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero);
        var diffStore = new DiffStore(TestGit.Services.Diff, location, InlineUi.Instance);
        var session = new TerminalTabSession(
            new TabSessionParts(new TabId(1), details, diffStore),
            GraphGlyphSet.Unicode);
        var token = session.Cancellation.Token;

        session.Dispose();

        Assert.True(token.IsCancellationRequested);
    }
}
