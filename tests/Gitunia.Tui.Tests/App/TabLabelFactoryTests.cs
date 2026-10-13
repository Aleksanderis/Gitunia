using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class TabLabelFactoryTests
{
    private static string Root(params string[] segments) => Path.Combine([Path.GetTempPath(), "label-tests", .. segments]);

    private static Workspace Open(params string[][] repositories)
    {
        var workspace = Workspace.Empty;
        foreach (var segments in repositories)
        {
            var root = Root(segments);
            var location = new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);
            workspace = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(location)).Workspace;
        }

        return workspace;
    }

    [Fact]
    public void GivenTwoTabsWithTheSameName_WhenLabelling_ThenTheParentDirectoryDisambiguates()
    {
        var workspace = Open(["work", "app"], ["home", "app"], ["other"]);

        var labels = TabLabelFactory.Create(workspace, _ => false);

        Assert.Equal(["app (work)", "app (home)", "other"], labels.Select(label => label.Name));
        Assert.Equal([1, 2, 3], labels.Select(label => label.Number));
    }

    [Fact]
    public void GivenStatusAndHead_WhenLabelling_ThenMarkersAndBranchComeFromTheTabState()
    {
        var workspace = Open(["a"]);
        var id = workspace.Tabs[0].Id;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new HeadLoadedCommand(new HeadState(HeadKind.Branch, "feature", null)))).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new StatusLoadedCommand(new RepoStatusSummary(true, 2, 5)))).Workspace;

        var label = Assert.Single(TabLabelFactory.Create(workspace, _ => false, showBranch: true));

        Assert.Equal("feature", label.Branch);
        Assert.True(label.IsDirty);
        Assert.Equal((2, 5), (label.Ahead, label.Behind));
        Assert.True(label.IsActive);
    }

    [Fact]
    public void GivenABranch_WhenLabellingByDefault_ThenTheTabHasNoBranch()
    {
        var workspace = Open(["a"]);
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(workspace.Tabs[0].Id, new HeadLoadedCommand(new HeadState(HeadKind.Branch, "feature", null)))).Workspace;

        Assert.Null(Assert.Single(TabLabelFactory.Create(workspace, _ => false)).Branch);
    }

    [Fact]
    public void GivenARenamedTab_WhenLabelling_ThenTheCustomNameIsUsedEvenWhenAnotherTabHasTheRepositoryName()
    {
        var workspace = Open(["work", "app"], ["home", "app"]);
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(workspace.Tabs[0].Id, new SetTabNameCommand("  Billing  "))).Workspace;

        var labels = TabLabelFactory.Create(workspace, _ => false);

        Assert.Equal(["Billing", "app"], labels.Select(label => label.Name));
    }

    [Fact]
    public void GivenMissingTab_WhenLabelling_ThenItIsNeverShownAsLoading()
    {
        var workspace = Open(["a"]);
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(workspace.Tabs[0].Id, new SetMissingCommand(true))).Workspace;

        var label = Assert.Single(TabLabelFactory.Create(workspace, _ => true));

        Assert.True(label.IsMissing);
        Assert.False(label.IsLoading);
    }
}
