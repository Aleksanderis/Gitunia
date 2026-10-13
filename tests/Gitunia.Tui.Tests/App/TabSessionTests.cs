using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.App;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.App;

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
