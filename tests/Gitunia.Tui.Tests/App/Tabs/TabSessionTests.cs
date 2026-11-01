using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
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
        var store = new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero);
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
}
