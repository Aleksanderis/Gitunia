using System.Collections.Immutable;
using Gitunia.Core.Persistence;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Core.Tests.Persistence;

public class SessionDocumentMapperTests
{
    private const string CommitHex = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void GivenWorkspaceWithViewState_WhenRoundTripped_ThenTabsActiveRecentsPinsAndViewAreRestored()
    {
        var workspace = SessionFixtures.Open(Workspace.Empty, "alpha", "beta", "gamma");
        workspace = WorkspaceReducer.Reduce(workspace, new TogglePinCommand(SessionFixtures.AbsolutePath("beta"))).Workspace;
        var betaId = workspace.FindTabByRootPath(SessionFixtures.AbsolutePath("beta"))!.Id;
        Assert.True(ObjectId.TryParse(CommitHex, out var commit));
        var view = new RepoViewState(commit, 42, "fix", PaneKind.Detail);
        workspace = workspace with
        {
            ActiveTabId = betaId,
            Tabs = workspace.Tabs.Select(tab => tab.Id == betaId ? tab with { View = view } : tab).ToImmutableArray(),
        };

        var restored = SessionDocumentMapper.ToWorkspace(SessionDocumentMapper.ToDocument(workspace), _ => true);

        Assert.Equal(["alpha", "beta", "gamma"], restored.Workspace.Tabs.Select(tab => tab.DisplayName));
        var active = restored.Workspace.ActiveTab!;
        Assert.Equal("beta", active.DisplayName);
        Assert.Equal(view, active.View);
        Assert.Equal(workspace.Recents, restored.Workspace.Recents);
        Assert.Equal(workspace.Pins, restored.Workspace.Pins);
        Assert.Equal(4, restored.Workspace.NextTabNumber);
        Assert.Equal(3, restored.Effects.Length);
    }

    [Fact]
    public void GivenARenamedTab_WhenRoundTripped_ThenTheCustomNameSurvivesAndABlankOneIsDropped()
    {
        var workspace = SessionFixtures.Open(Workspace.Empty, "alpha", "beta");
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(workspace.Tabs[0].Id, new SetTabNameCommand(" Billing "))).Workspace;
        var document = SessionDocumentMapper.ToDocument(workspace);

        var restored = SessionDocumentMapper.ToWorkspace(document, _ => true).Workspace;
        var blank = SessionDocumentMapper.ToWorkspace(document with { Tabs = [document.Tabs![0] with { CustomName = "   " }, document.Tabs[1]] }, _ => true).Workspace;

        Assert.Equal("Billing", document.Tabs![0].CustomName);
        Assert.Null(document.Tabs[1].CustomName);
        Assert.Equal(["Billing", "beta"], restored.Tabs.Select(tab => tab.Title));
        Assert.Equal(["alpha", "beta"], blank.Tabs.Select(tab => tab.Title));
    }

    [Fact]
    public void GivenTabWhoseDirectoryIsGone_WhenRestoring_ThenTabIsKeptAsMissingWithoutLoadEffect()
    {
        var workspace = SessionFixtures.Open(Workspace.Empty, "alpha", "beta");
        var missingPath = SessionFixtures.Location("beta").CommandPath;

        var restored = SessionDocumentMapper.ToWorkspace(SessionDocumentMapper.ToDocument(workspace), path => path != missingPath);

        Assert.Equal(2, restored.Workspace.Tabs.Length);
        Assert.True(restored.Workspace.Tabs[1].IsMissing);
        Assert.False(restored.Workspace.Tabs[0].IsMissing);
        var effect = Assert.IsType<LoadRepositoryEffect>(Assert.Single(restored.Effects));
        Assert.Equal(restored.Workspace.Tabs[0].Id, effect.TabId);
    }

    [Fact]
    public void GivenBadEntries_WhenRestoring_ThenTheyAreSkippedOrDefaulted()
    {
        var good = SessionFixtures.Location("good");
        var document = new SessionDocument
        {
            Version = SessionDocument.CurrentVersion,
            ActiveTabIndex = 99,
            Tabs =
            [
                new SessionTabDocument { GitDirectory = "relative/.git", WorkingTreePath = "relative" },
                new SessionTabDocument { GitDirectory = null },
                new SessionTabDocument
                {
                    WorkingTreePath = good.WorkingTreePath,
                    GitDirectory = good.GitDirectory,
                    SelectedCommit = "not-a-hash",
                    ScrollOffset = -5,
                    FocusedPane = "Nonsense",
                },
                new SessionTabDocument { WorkingTreePath = good.WorkingTreePath, GitDirectory = good.GitDirectory },
            ],
            Recents = ["relative", SessionFixtures.AbsolutePath("a"), SessionFixtures.AbsolutePath("a")],
            Pins = null,
        };

        var restored = SessionDocumentMapper.ToWorkspace(document, _ => true).Workspace;

        var tab = Assert.Single(restored.Tabs);
        Assert.Equal(RepoViewState.Initial, tab.View);
        Assert.Equal(tab.Id, restored.ActiveTabId);
        Assert.Equal([SessionFixtures.AbsolutePath("a")], restored.Recents);
        Assert.Empty(restored.Pins);
    }

    [Theory]
    [InlineData(BottomTab.Details)]
    [InlineData(BottomTab.Changes)]
    [InlineData(BottomTab.Files)]
    public void GivenABottomTab_WhenRoundTripped_ThenTheTabIsRestored(BottomTab tab)
    {
        var workspace = SessionFixtures.Open(Workspace.Empty, "alpha");
        var id = workspace.ActiveTabId!.Value;
        workspace = WorkspaceReducer.Reduce(workspace, new TabCommand(id, new SetBottomTabCommand(tab))).Workspace;

        var restored = SessionDocumentMapper.ToWorkspace(SessionDocumentMapper.ToDocument(workspace), _ => true).Workspace;

        Assert.Equal(tab, restored.ActiveTab!.View.BottomTab);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nonsense")]
    [InlineData("99")]
    public void GivenAnOldFileOrABadBottomTab_WhenRestoring_ThenTheTabIsChanges(string? name)
    {
        var location = SessionFixtures.Location("alpha");
        var document = new SessionDocument
        {
            Version = SessionDocument.CurrentVersion,
            Tabs = [new SessionTabDocument { WorkingTreePath = location.WorkingTreePath, GitDirectory = location.GitDirectory, BottomTab = name }],
        };

        var restored = SessionDocumentMapper.ToWorkspace(document, _ => true).Workspace;

        Assert.Equal(BottomTab.Changes, restored.ActiveTab!.View.BottomTab);
    }

    [Fact]
    public void GivenNoTabs_WhenRestoring_ThenWorkspaceHasNoActiveTab()
    {
        var restored = SessionDocumentMapper.ToWorkspace(new SessionDocument { Version = SessionDocument.CurrentVersion }, _ => true);

        Assert.Empty(restored.Workspace.Tabs);
        Assert.Null(restored.Workspace.ActiveTabId);
        Assert.Empty(restored.Effects);
    }
}
