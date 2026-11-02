using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Views.Lists;

public class WorkingTreeListSourceRowMenuTests
{
    [Fact]
    public void GivenNoStatusLoaded_WhenAskingAboutARow_ThenNoRowHasAMenu()
    {
        var source = new WorkingTreeListSource(WorkingTreeSection.Unstaged, GraphGlyphSet.Unicode);

        Assert.False(source.HasRowMenu(0));
    }

    [Fact]
    public void GivenTheFlatStyle_WhenAskingAboutRows_ThenFileRowsHaveAMenuAndOutOfRangeRowsDoNot()
    {
        var source = new WorkingTreeListSource(WorkingTreeSection.Unstaged, GraphGlyphSet.Unicode);
        source.Update(Tree(Modified("a/one.cs"), Modified("two.cs")));

        Assert.True(source.HasRowMenu(0));
        Assert.True(source.HasRowMenu(1));
        Assert.False(source.HasRowMenu(-1));
        Assert.False(source.HasRowMenu(2));
    }

    [Fact]
    public void GivenTheTreeStyle_WhenAskingAboutRows_ThenFolderRowsHaveNoMenuButTheirFilesHave()
    {
        var source = new WorkingTreeListSource(WorkingTreeSection.Unstaged, GraphGlyphSet.Unicode);
        source.Update(Tree(Modified("dir/one.cs"), Modified("top.cs")) with { PathStyle = FilePathStyle.Tree });

        Assert.Equal(3, source.Count);
        Assert.False(source.HasRowMenu(0));
        Assert.True(source.HasRowMenu(1));
        Assert.True(source.HasRowMenu(2));
    }

    [Fact]
    public void GivenTheStagedList_WhenAskingAboutRows_ThenItAnswersForItsOwnRowsOnly()
    {
        var staged = new WorkingTreeListSource(WorkingTreeSection.Staged, GraphGlyphSet.Unicode);
        staged.Update(Tree(Modified("a.cs"), StagedAdded("b.cs")));

        Assert.True(staged.HasRowMenu(0));
        Assert.False(staged.HasRowMenu(1));
    }
}
