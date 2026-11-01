using Gitunia.Core.Effects;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git.ExternalTools;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.ExternalTools;

public class ExternalToolReducerTests
{
    private const string Commit = "1111111111111111111111111111111111111111";

    [Fact]
    public void GivenNoTab_WhenAskingForATool_ThenTheNoticeSaysSoAndNothingLaunches()
    {
        var update = AppReducer.Reduce(AppState.Empty, new ExternalToolRequested(ExternalToolKind.Mergetool, null));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(ExternalToolReducer.NoRepositoryNotice, post.Text);
    }

    [Fact]
    public void GivenNoSelectedCommit_WhenAskingForTheDifftool_ThenTheNoticeSaysSo()
    {
        var state = Opened();

        var update = AppReducer.Reduce(state, new ExternalToolRequested(ExternalToolKind.Difftool, null));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(ExternalToolReducer.NoCommitNotice, post.Text);
        Assert.Equal(StatusSeverity.Info, post.Severity);
    }

    [Fact]
    public void GivenASelectedCommit_WhenAskingForTheDifftool_ThenItLaunchesForTheActiveTabAtThatCommit()
    {
        var state = Opened();

        var update = AppReducer.Reduce(state, new ExternalToolRequested(ExternalToolKind.Difftool, Commit));

        var launch = Assert.IsType<LaunchExternalToolEffect>(Assert.Single(update.Effects));
        Assert.Equal(state.Workspace.ActiveTab!.Id, launch.TabId);
        Assert.Equal(state.Workspace.ActiveTab.Location, launch.Location);
        Assert.Equal(ExternalToolKind.Difftool, launch.Kind);
        Assert.Equal(Commit, launch.Commit);
    }

    [Theory]
    [InlineData(ExternalToolKind.Mergetool)]
    [InlineData(ExternalToolKind.CommitWithEditor)]
    public void GivenNoSelectedCommit_WhenAskingForAToolThatNeedsNone_ThenItLaunches(ExternalToolKind kind)
    {
        var update = AppReducer.Reduce(Opened(), new ExternalToolRequested(kind, null));

        var launch = Assert.IsType<LaunchExternalToolEffect>(Assert.Single(update.Effects));
        Assert.Equal(kind, launch.Kind);
        Assert.Null(launch.Commit);
    }

    private static AppState Opened()
    {
        var root = Path.GetFullPath("alpha");
        var metadata = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, metadata, metadata, false, false);

        return AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
    }
}
