using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Workspaces.Tabs;

public class TabRenameReducerTests
{
    [Fact]
    public void GivenATab_WhenRenameIsRequested_ThenThePromptStartsWithTheTitleAndNamesTheTab()
    {
        var tab = Tab("alpha") with { CustomName = "work" };

        var update = TabRenameReducer.Reduce(tab, new TabRenameRequested(tab.Id));

        Assert.Same(tab, update.Tab);
        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal("Rename tab", request.Title);
        Assert.Equal(TabRenameReducer.PromptContext, request.Context);
        Assert.Equal("save", request.AcceptLabel);
        Assert.Equal("work", request.InitialText);
        Assert.Equal(tab.Id, request.Owner);
        Assert.Equal(new TabRenameAnswer(tab.Id), request.OnAccepted);
    }

    [Fact]
    public void GivenTheAnswer_WhenTheTextIsTurnedIntoACommand_ThenItCarriesTheTabAndTheText()
    {
        var command = new TabRenameAnswer(new TabId(3)).Answered(new PromptAnswer("new", null));

        Assert.Equal(new TabRenameAnswered(new TabId(3), "new"), command);
    }

    [Fact]
    public void GivenAName_WhenAnswered_ThenTheTabTitleChangesAndTheStatusSaysSo()
    {
        var tab = Tab("alpha");

        var update = TabRenameReducer.Reduce(tab, new TabRenameAnswered(tab.Id, " alpha-2 "));

        Assert.Equal("alpha-2", update.Tab.Title);
        Assert.Equal("alpha", update.Tab.DisplayName);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal("tab is now alpha-2", post.Text);
        Assert.Equal(StatusSeverity.Info, post.Severity);
    }

    [Fact]
    public void GivenARenamedTab_WhenAnsweredWithAnEmptyName_ThenTheRepositoryNameIsBack()
    {
        var tab = Tab("alpha") with { CustomName = "work" };

        var update = TabRenameReducer.Reduce(tab, new TabRenameAnswered(tab.Id, "  "));

        Assert.Null(update.Tab.CustomName);
        Assert.Equal("alpha", update.Tab.Title);
        Assert.Equal("tab is now alpha", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    private static RepositoryState Tab(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-rename", name);
        var gitDirectory = Path.Combine(root, ".git");

        return RepositoryState.Create(new TabId(1), new RepositoryLocation(root, gitDirectory, gitDirectory, false, false));
    }
}
