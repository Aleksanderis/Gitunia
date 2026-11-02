using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Overlays;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Tests.Views;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.Overlays;

public class RowMenuTargetsFileTests
{
    [Fact]
    public void GivenAFileSelectedInUnstaged_WhenOpeningTheRowMenu_ThenTheFileMenuIsOffered()
    {
        using var fixture = new MainViewFixture();
        Show(fixture, selectUnstaged: 0, Tree(Modified("a.cs")));

        var target = TargetOf(fixture, DefaultKeymap.WorkingUnstagedView);

        Assert.NotNull(target);
        Assert.Equal(RowActions.OfFileRow, target.Actions);
        Assert.Equal(RowActions.FileRowTitle, target.Title);
    }

    [Fact]
    public void GivenAFolderRowSelected_WhenOpeningTheRowMenu_ThenNoMenuIsOffered()
    {
        using var fixture = new MainViewFixture();
        Show(fixture, selectUnstaged: 0, Tree(Modified("dir/a.cs")) with { PathStyle = FilePathStyle.Tree });

        Assert.Null(TargetOf(fixture, DefaultKeymap.WorkingUnstagedView));
    }

    [Fact]
    public void GivenAnEmptyList_WhenOpeningTheRowMenu_ThenNoMenuIsOffered()
    {
        using var fixture = new MainViewFixture();
        Show(fixture, selectUnstaged: null, Tree());

        Assert.Null(TargetOf(fixture, DefaultKeymap.WorkingUnstagedView));
    }

    [Fact]
    public void GivenAFileOnlyInUnstaged_WhenOpeningTheStagedMenu_ThenTheStagedListDecides()
    {
        using var fixture = new MainViewFixture();
        Show(fixture, selectUnstaged: 0, Tree(Modified("a.cs")));

        Assert.Null(TargetOf(fixture, DefaultKeymap.WorkingStagedView));
        Assert.NotNull(TargetOf(fixture, DefaultKeymap.WorkingUnstagedView));
    }

    [Fact]
    public void GivenAStagedFileSelected_WhenOpeningTheStagedMenu_ThenTheFileMenuIsOffered()
    {
        using var fixture = new MainViewFixture();
        fixture.Panes.StagedSource.Update(Tree(StagedAdded("b.cs")));
        fixture.Panes.WorkingStaged.SelectedItem = 0;

        Assert.NotNull(TargetOf(fixture, DefaultKeymap.WorkingStagedView));
    }

    [Fact]
    public void GivenADetailViewWithNoRows_WhenOpeningTheRowMenu_ThenTheDetailListDecidesAndNoMenuIsOffered()
    {
        using var fixture = new MainViewFixture();
        Show(fixture, selectUnstaged: 0, Tree(Modified("a.cs")));

        Assert.Null(TargetOf(fixture, DefaultKeymap.DetailChangesView));
    }

    private static void Show(MainViewFixture fixture, int? selectUnstaged, WorkingTreeState tree)
    {
        fixture.Panes.UnstagedSource.Update(tree);
        fixture.Panes.WorkingUnstaged.SelectedItem = selectUnstaged;
    }

    private static RowMenuTarget? TargetOf(MainViewFixture fixture, string view) =>
        new RowMenuTargets(fixture.Panes, null!, null!, null!).TargetOf(view, ActionIds.MenuOpenForRow);
}
