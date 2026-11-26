using Gitunia.Core.Effects;
using Gitunia.Core.Ignore;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Ignore;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Ignore;

// The pure part of ignore: what the key offers, the refusals and the reports. Writing the file is the handler's.
[Trait("Os", "Independent")]
public class IgnoreReducerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly RepositoryLocation Location = new("/r", "/r/.git", "/r/.git", false, false);

    public static TheoryData<string, IgnoreContext, string> Refusals() => new()
    {
        {
            "a tracked modified file",
            Context(Select(Tree(Modified("a.txt")))),
            "a.txt is tracked: .gitignore does not apply to it"
        },
        {
            "a folder of tracked files",
            Context(Select(TreeOf(Modified("src/a.cs"), Modified("src/b.cs"), Modified("top.txt")))),
            "src has no untracked files"
        },
        {
            "a name with a line break",
            Context(Select(Tree(Untracked("a\nb.txt")))),
            "cannot be written to .gitignore"
        },
        {
            "a name that is not valid UTF-8",
            Context(Select(Tree(Untracked($"bad{(char)0xDCE9}.txt")))),
            "cannot be written to .gitignore"
        },
        {
            "the staged list",
            Context(Select(Tree(StagedModified("a.txt")), WorkingTreeSection.Staged), PaneKind.WorkingStaged),
            "unstage"
        },
        { "no list focused", Context(Select(Tree(Untracked("a.txt"))), PaneKind.Graph), "Focus a file list first" },
        { "no status yet", Context(null), "The status is not loaded yet" },
        { "nothing selected", Context(Tree(Untracked("a.txt")) with { Selection = null }), "Select an untracked" },
        { "an ignore running", Context(Select(Tree(Untracked("a.txt"))), isBusy: true), IgnoreReducer.BusyNotice },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void GivenSomethingThatCannotBeIgnored_WhenAsking_ThenOnlyTheReasonIsPostedWithoutAPrompt(
        string situation,
        IgnoreContext context,
        string expectedText)
    {
        var update = IgnoreReducer.Reduce(new IgnoreRequested(Tab), context);

        Assert.NotEmpty(situation);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update));
        Assert.Contains(expectedText, post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUntrackedFile_WhenAsking_ThenAOneKeyPromptNamesTheLinesItWouldAdd()
    {
        var update = IgnoreReducer.Reduce(
            new IgnoreRequested(Tab),
            Context(Select(Tree(Untracked("logs/debug.log")))));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(update)).Request;
        Assert.Equal("Add to .gitignore", request.Title);
        Assert.Equal("logs/debug.log", request.Context);
        Assert.Equal(["/logs/debug.log", "*.log", "/logs/"], request.KeyChoices!.Value.Select(choice => choice.Value));
        Assert.Equal(Tab, request.Owner);
    }

    [Fact]
    public void GivenAnUntrackedDirectoryEntry_WhenAsking_ThenTheOptionIsTheFolderPattern()
    {
        var update = IgnoreReducer.Reduce(new IgnoreRequested(Tab), Context(Select(Tree(Untracked("build/")))));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(update)).Request;
        Assert.Equal("/build/", Assert.Single(request.KeyChoices!.Value).Value);
    }

    [Fact]
    public void GivenAFolderRowWithAnUntrackedFile_WhenAsking_ThenTheFolderPatternIsOffered()
    {
        var tree = Select(TreeOf(Modified("src/a.cs"), Untracked("src/n.cs"), Modified("top.txt")));

        var update = IgnoreReducer.Reduce(new IgnoreRequested(Tab), Context(tree));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(update)).Request;
        Assert.Equal("/src/", Assert.Single(request.KeyChoices!.Value).Value);
    }

    [Fact]
    public void GivenTheUnstagedDiffIsFocused_WhenAsking_ThenTheFileOfTheSelectionIsOffered()
    {
        var context = Context(Select(Tree(Untracked("a.txt"))), PaneKind.WorkingUnstagedDiff);

        var update = IgnoreReducer.Reduce(new IgnoreRequested(Tab), context);

        Assert.IsType<ShowPromptEffect>(Assert.Single(update));
    }

    [Fact]
    public void GivenThePromptAnswer_WhenKeyChosen_ThenTheOfferedLineIsTheCommandAndTheTabBusyRuleHolds()
    {
        var answer = new IgnoreAnswer(Tab).Answered(new PromptAnswer(string.Empty, "*.log"));

        var chosen = Assert.IsType<IgnoreChosen>(answer);
        var effect = Assert.IsType<AddIgnorePatternEffect>(Assert.Single(IgnoreReducer.Reduce(chosen, Context(null))));
        Assert.Equal("*.log", effect.Pattern);
        Assert.Equal(Location, effect.Location);
        var busy = IgnoreReducer.Reduce(chosen, Context(null, isBusy: true));
        Assert.Contains("nothing was added", Assert.IsType<PostStatusEffect>(Assert.Single(busy)).Text);
    }

    [Fact]
    public void GivenResults_WhenReducing_ThenTheyArePostedOrIgnored()
    {
        var info = new IgnoreSucceeded(Tab, "Added *.log to .gitignore", StatusSeverity.Info);
        var done = IgnoreReducer.Reduce(info, Context(null));
        var failed = IgnoreReducer.Reduce(new IgnoreFailed(Tab, "Could not update .gitignore: x"), Context(null));
        var cancelled = IgnoreReducer.Reduce(new IgnoreCancelled(Tab), Context(null));

        Assert.Equal(StatusSeverity.Info, Assert.IsType<PostStatusEffect>(Assert.Single(done)).Severity);
        Assert.Equal(StatusSeverity.Error, Assert.IsType<PostStatusEffect>(Assert.Single(failed)).Severity);
        Assert.Empty(cancelled);
    }

    [Fact]
    public void GivenTheOutcomes_WhenReporting_ThenAnExistingLineIsNotClaimedAsAdded()
    {
        Assert.Equal("Added *.log to .gitignore", IgnoreReports.Done("*.log", IgnoreAddOutcome.Added));
        Assert.StartsWith(
            "*.log is already in .gitignore",
            IgnoreReports.Done("*.log", IgnoreAddOutcome.AlreadyListed),
            StringComparison.Ordinal);
    }

    private static IgnoreContext Context(
        WorkingTreeState? tree,
        PaneKind focus = PaneKind.WorkingUnstaged,
        bool isBusy = false) =>
        new(Tab, Location, focus, tree, isBusy);

    private static WorkingTreeState TreeOf(params StatusEntry[] entries) =>
        WorkingTreeState.Reconcile(null, Snapshot(entries), FilePathStyle.Tree);

    private static WorkingTreeState Select(
        WorkingTreeState tree,
        WorkingTreeSection section = WorkingTreeSection.Unstaged) =>
        tree with { Selection = tree.SelectionAt(section, 0) };
}
