using Gitunia.Core.Workspaces;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tabs;

public class TabSessionTests
{
    [Fact]
    public void GivenFailedLoad_WhenTheGraphIsReplaced_ThenTheLoadErrorIsCleared()
    {
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        using var session = new TabSession(new TabId(1), GraphGlyphSet.Unicode, new CommitDetailsStore(new Repo(new GitRunner(), location), action => action(), TimeSpan.Zero));
        session.Fail("boom");
        Assert.Equal("boom", session.LoadError);

        session.ReplaceGraph([]);

        Assert.Equal(string.Empty, session.LoadError);
    }
}
