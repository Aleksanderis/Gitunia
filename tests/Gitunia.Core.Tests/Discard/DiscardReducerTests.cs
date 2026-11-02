using System.Collections.Immutable;
using Gitunia.Core.Discard;
using Gitunia.Core.Effects;
using Gitunia.Core.Lists;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Discard;

// The pure part of discard: what the key names, the dialog texts, the refusals and the reports. Real git is the handler's.
[Trait("Os", "Independent")]
public class DiscardReducerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly RepositoryLocation Location = new("/r", "/r/.git", "/r/.git", false, false);

    public static TheoryData<string, DiscardCommand, DiscardContext, string> Refusals()
    {
        var staged = Context(Tree(StagedModified("a.txt")), PaneKind.WorkingStaged);

        return new TheoryData<string, DiscardCommand, DiscardContext, string>
        {
            { "a conflicted file", new DiscardRequested(Tab), Context(Tree(Conflict("c.txt"))), "conflicted" },
            {
                "only conflicted files in a range",
                new DiscardRequested(Tab, new RowRange(0, 1)),
                Context(Tree(Conflict("c.txt"), Conflict("d.txt"))),
                "conflicted"
            },
            {
                "an invalid name where the OS cannot delete it",
                new DiscardRequested(Tab),
                Context(Tree(Untracked($"bad{(char)0xDCE9}.txt")), canDeleteInvalidNames: false),
                "not valid UTF-8"
            },
            { "the staged list", new DiscardRequested(Tab), staged, "unstage" },
            {
                "a range of staged files",
                new DiscardRequested(Tab, new RowRange(0, 0)),
                staged,
                "Discard works in the Unstaged list: unstage the files first"
            },
            {
                "no file list focused",
                new DiscardRequested(Tab),
                Context(Tree(Modified("a.txt")), PaneKind.Graph),
                "Focus a file list first"
            },
            { "no status yet", new DiscardRequested(Tab), Context(null), "The status is not loaded yet" },
            {
                "a discard running",
                new DiscardRequested(Tab),
                Context(Tree(Modified("a.txt")), isBusy: true),
                DiscardReducer.BusyNotice
            },
            {
                "another operation writing the tree",
                new DiscardRequested(Tab),
                Context(Tree(Modified("a.txt")), isBlocked: true),
                DiscardReducer.BlockedNotice
            },
        };
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public void GivenSomethingThatCannotBeDiscarded_WhenDiscarding_ThenOnlyTheReasonIsPostedWithoutADialog(
        string situation,
        DiscardCommand command,
        DiscardContext context,
        string expectedText)
    {
        var update = Reduce(command, context);

        Assert.NotEmpty(situation);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update));
        Assert.Contains(expectedText, post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAModifiedFile_WhenDiscarding_ThenTheDialogNamesWhatIsLostAndNothingRunsYet()
    {
        var update = Reduce(new DiscardRequested(Tab), Context(Tree(Modified("a.txt"))));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update));
        Assert.Equal("Discard changes", confirm.Prompt.Title);
        Assert.Equal(["Discard all unstaged changes to a.txt?"], confirm.Prompt.Lines);
        Assert.Contains("cannot be undone", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal("The screen is too small to ask: nothing was discarded", confirm.UnavailableNotice);
        var accepted = Assert.IsType<DiscardConfirmed>(confirm.OnAccepted);
        Assert.Equal(new DiscardFile(Modified("a.txt")), accepted.Target);
        Assert.False(accepted.EndsRange);
    }

    [Fact]
    public void GivenAFolderRow_WhenDiscarding_ThenTheDialogNamesTheFolderAndCountsEveryFileUnderIt()
    {
        var tree = Select(
            TreeOf(
                FilePathStyle.Tree,
                Modified("src/a.cs"),
                Modified("src/b.cs"),
                Modified("top.txt"),
                Untracked("src/n.cs")),
            WorkingTreeSection.Unstaged,
            0);

        var update = Reduce(new DiscardRequested(Tab), Context(tree));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update));
        Assert.Equal("Discard changes to 3 files in src/?", confirm.Prompt.Lines[0]);
        Assert.Contains("src/n.cs", confirm.Prompt.Lines[1], StringComparison.Ordinal);
        var target = Assert.IsType<DiscardFiles>(Assert.IsType<DiscardConfirmed>(confirm.OnAccepted).Target);
        Assert.Equal(["src/a.cs", "src/b.cs", "src/n.cs"], target.Files.Select(entry => entry.Path).Order());
    }

    [Fact]
    public void GivenARangeFromAFolderRowThroughItsFile_WhenDiscarding_ThenTheFilesAreCountedOnce()
    {
        var tree = TreeOf(FilePathStyle.Tree, Modified("src/a.cs"), Modified("src/b.cs"), Modified("top.txt"));

        var update = Reduce(new DiscardRequested(Tab, new RowRange(0, 1)), Context(tree));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update));
        Assert.Equal("Discard changes to 2 files?", confirm.Prompt.Lines[0]);
        Assert.True(Assert.IsType<DiscardConfirmed>(confirm.OnAccepted).EndsRange);
    }

    [Fact]
    public void GivenAnUntrackedFile_WhenDiscarding_ThenTheDialogSaysItIsDeletedFromDisk()
    {
        var update = Reduce(new DiscardRequested(Tab), Context(Tree(Untracked("new.txt"))));

        var prompt = Assert.IsType<ShowConfirmEffect>(Assert.Single(update)).Prompt;
        Assert.Equal("Delete untracked file", prompt.Title);
        Assert.Equal(["Delete the untracked file new.txt?"], prompt.Lines);
        Assert.Contains("removed from disk", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal("Delete file", prompt.YesLabel);
    }

    [Fact]
    public void GivenAnUntrackedDirectory_WhenDiscarding_ThenTheDialogNamesEverythingInIt()
    {
        var update = Reduce(new DiscardRequested(Tab), Context(Tree(Untracked("build/"))));

        var prompt = Assert.IsType<ShowConfirmEffect>(Assert.Single(update)).Prompt;
        Assert.Equal(["Delete the untracked directory build/ and everything in it?"], prompt.Lines);
    }

    [Fact]
    public void GivenADeletedFileWithStagedChanges_WhenDiscarding_ThenTheDialogSaysWhatComesBackAndWhatStays()
    {
        var tree = Tree(Deleted("gone.txt") with { IndexChange = FileChangeKind.Modified });

        var update = Reduce(new DiscardRequested(Tab), Context(tree));

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(update)).Prompt.Lines;
        Assert.Contains("The deleted file is restored from the index.", lines);
        Assert.Contains("Its staged changes are kept.", lines);
    }

    [Fact]
    public void GivenAnUntrackedNameThatIsNotUtf8OnLinux_WhenDiscarding_ThenTheDialogOpens()
    {
        var tree = Tree(Untracked($"bad{(char)0xDCE9}.txt"));

        var update = Reduce(new DiscardRequested(Tab), Context(tree, canDeleteInvalidNames: true));

        Assert.IsType<ShowConfirmEffect>(Assert.Single(update));
    }

    [Fact]
    public void GivenARangeWithAnUntrackedDirectory_WhenDiscarding_ThenTheDialogSaysEverythingInItIsDeleted()
    {
        var tree = Tree(Modified("a.txt"), Untracked("build/"), Modified("b.txt"));

        var update = Reduce(new DiscardRequested(Tab, new RowRange(0, 2)), Context(tree));

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(update)).Prompt.Lines;
        Assert.Equal("Discard changes to 3 files?", lines[0]);
        Assert.Contains("build/", lines[1], StringComparison.Ordinal);
        Assert.Contains("Untracked directories are deleted with everything in them.", lines);
    }

    [Fact]
    public void GivenARangeWithOneUsableFileAndAConflict_WhenDiscarding_ThenTheDialogCountsAndTheConflictIsLeftOut()
    {
        var tree = Tree(Modified("a.txt"), Conflict("c.txt"));

        var update = Reduce(new DiscardRequested(Tab, new RowRange(0, 1)), Context(tree));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update));
        Assert.Equal("Discard changes to 1 file?", confirm.Prompt.Lines[0]);
        Assert.Equal("Discard 1 file", confirm.Prompt.YesLabel);
        Assert.Contains("1 conflicted, submodule or unreadable-name file left out.", confirm.Prompt.Lines);
        var target = Assert.IsType<DiscardFiles>(Assert.IsType<DiscardConfirmed>(confirm.OnAccepted).Target);
        Assert.Equal(["a.txt"], target.Files.Select(entry => entry.Path));
        Assert.Equal(1, target.Skipped);
    }

    [Fact]
    public void GivenAnOperationStartedWhileTheDialogWasOpen_WhenYIsAnswered_ThenNothingIsDiscarded()
    {
        var confirmed = new DiscardConfirmed(Tab, new DiscardFile(Modified("a.txt")), false);

        var update = Reduce(confirmed, Context(Tree(Modified("a.txt")), isBlocked: true));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update));
        Assert.Equal($"{DiscardReducer.BlockedNotice}; nothing was discarded", post.Text);
    }

    [Fact]
    public void GivenTheTargetOfTheDialog_WhenTheCursorMovedBeforeY_ThenTheNamedFileIsTheOneDiscarded()
    {
        var tree = Select(Tree(Modified("a.txt"), Modified("b.txt")), WorkingTreeSection.Unstaged, 1);
        var confirmed = new DiscardConfirmed(Tab, new DiscardFile(Modified("a.txt")), false);

        var update = Reduce(confirmed, Context(tree));

        var effect = Assert.IsType<DiscardSelectionEffect>(Assert.Single(update));
        Assert.Equal("a.txt", effect.Selection.Entry.Path);
        Assert.Empty(effect.Selection.Lines);
        Assert.True(effect.AllowWholeFile);
        Assert.Null(effect.Move);
    }

    [Fact]
    public void GivenAConfirmedBatch_WhenReducing_ThenOneEffectCarriesExactlyTheFilesTheDialogCounted()
    {
        IReadOnlyList<StatusEntry> files = [Modified("a.txt"), Untracked("n.txt")];
        var confirmed = new DiscardConfirmed(Tab, new DiscardFiles(files, 2), true);

        var update = Reduce(confirmed, Context(Tree(Modified("a.txt"))));

        var effect = Assert.IsType<DiscardFilesEffect>(Assert.Single(update));
        Assert.Same(files, effect.Files);
        Assert.Equal(2, effect.Skipped);
        Assert.Equal(Location, effect.Location);
    }

    [Fact]
    public void GivenAHunk_WhenDiscardingIt_ThenTheDialogCountsItsLines()
    {
        var update = Reduce(Selection(Modified("a.txt"), [Changed(2, "TWO"), Changed(4, "four")], false), Context(null));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update));
        Assert.Equal("Discard change", confirm.Prompt.Title);
        Assert.Equal(["Discard this change (2 lines) of a.txt?"], confirm.Prompt.Lines);
        Assert.False(Assert.IsType<DiscardConfirmed>(confirm.OnAccepted).EndsRange);
    }

    [Fact]
    public void GivenAHunkConfirmed_WhenReducing_ThenTheEffectCutsLinesAndNeverWidensToTheWholeFile()
    {
        var selection = new StagingSelection(WorkingDiffSide.Unstaged, Modified("a.txt"), [Changed(2, "TWO")], false);
        var move = new DiffCursorMove(1, -1);

        var update = Reduce(new DiscardConfirmed(Tab, new DiscardLines(selection, move), false), Context(null));

        var effect = Assert.IsType<DiscardSelectionEffect>(Assert.Single(update));
        Assert.False(effect.AllowWholeFile);
        Assert.Equal(move, effect.Move);
        Assert.Same(selection, effect.Selection);
    }

    [Fact]
    public void GivenAHunkOfAnUntrackedFile_WhenConfirmed_ThenTheWholeFileIsDeletedAsTheDialogSaid()
    {
        var selection = new StagingSelection(WorkingDiffSide.Unstaged, Untracked("new.txt"), [Changed(1, "x")], false);

        var asked = Reduce(new DiscardSelectionRequested(Tab, selection, new DiffCursorMove(0, -1)), Context(null));
        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(asked));
        Assert.Equal("Delete untracked file", confirm.Prompt.Title);
        var update = Reduce(Assert.IsType<DiscardConfirmed>(confirm.OnAccepted), Context(null));

        Assert.True(Assert.IsType<DiscardSelectionEffect>(Assert.Single(update)).AllowWholeFile);
    }

    [Fact]
    public void GivenAFileWithoutHunks_WhenDiscardingTheHunk_ThenTheDialogSaysTheWholeFileGoes()
    {
        var update = Reduce(Selection(Deleted("gone.txt"), [Changed(1, "x")], false), Context(null));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update));
        Assert.Equal(["gone.txt is deleted. Discard all unstaged changes to it?"], confirm.Prompt.Lines);
        var effect = Reduce(Assert.IsType<DiscardConfirmed>(confirm.OnAccepted), Context(null));
        Assert.True(Assert.IsType<DiscardSelectionEffect>(Assert.Single(effect)).AllowWholeFile);
    }

    [Fact]
    public void GivenLinesPickedInAnUntrackedFile_WhenDiscarding_ThenItIsRefusedWithoutADialog()
    {
        var update = Reduce(Selection(Untracked("new.txt"), [Changed(1, "x")], true), Context(null));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update));
        Assert.Contains("cannot be split into lines", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheStagedDiff_WhenDiscarding_ThenItIsRefused()
    {
        var selection = new StagingSelection(WorkingDiffSide.Staged, StagedModified("a.txt"), [Changed(2, "TWO")], false);

        var update = Reduce(new DiscardSelectionRequested(Tab, selection, new DiffCursorMove(0, -1)), Context(null));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update));
        Assert.Contains("Unstaged diff", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAStageRuns_WhenDiscardingTheHunk_ThenItIsRefusedAsBusy()
    {
        var update = Reduce(Selection(Modified("a.txt"), [Changed(2, "TWO")], false), Context(null, isBusy: true));

        Assert.Equal(DiscardReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(update)).Text);
    }

    [Fact]
    public void GivenAVisualRange_WhenDiscarding_ThenTheDialogCountsTheSelectedLinesAndTheRangeEndsOnYes()
    {
        var update = Reduce(
            Selection(Modified("a.txt"), [Changed(2, "TWO"), Changed(4, "four"), Changed(5, "five")], true),
            Context(null));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update));
        Assert.Equal("Discard lines", confirm.Prompt.Title);
        Assert.Equal(["Discard the 3 selected lines of a.txt?"], confirm.Prompt.Lines);
        Assert.True(Assert.IsType<DiscardConfirmed>(confirm.OnAccepted).EndsRange);
    }

    [Fact]
    public void GivenTheResultOfAHunk_WhenItArrives_ThenTheTextIsPostedAndTheCursorMoves()
    {
        var move = new DiffCursorMove(1, -1);

        var update = Reduce(new DiscardSucceeded(Tab, "Discarded 1 line of a.txt", StatusSeverity.Info, move), Context(null));

        Assert.Equal(2, update.Length);
        Assert.Equal("Discarded 1 line of a.txt", Assert.IsType<PostStatusEffect>(update[0]).Text);
        Assert.Equal(new MoveDiffCursorEffect(Tab, move), update[1]);
    }

    [Fact]
    public void GivenAFailureAndACancel_WhenTheyArrive_ThenTheFailureIsAnErrorAndTheCancelSaysNothing()
    {
        var failed = Reduce(new DiscardFailed(Tab, "Could not discard: x"), Context(null));
        var cancelled = Reduce(new DiscardCancelled(Tab), Context(null));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(failed));
        Assert.Equal(StatusSeverity.Error, post.Severity);
        Assert.Empty(cancelled);
    }

    [Fact]
    public void GivenTheReports_WhenFormatting_ThenCountsArePluralisedAndSkippedFilesAreSaid()
    {
        IReadOnlyList<StatusEntry> files = [Modified("a.txt"), Untracked("n.txt"), Untracked("m.txt")];

        Assert.Equal("Discarded 3 files (2 untracked deleted, 1 skipped)", DiscardReports.Files(files, 1));
        Assert.Equal("Discarded 1 file", DiscardReports.Files([Modified("a.txt")], 0));
        var selection = new StagingSelection(WorkingDiffSide.Unstaged, Modified("a.txt"), [Changed(2, "TWO")], true);
        Assert.Equal(
            "Discarded 1 line of a.txt",
            DiscardReports.Selection(selection, new PatchStagingResult(PatchStagingOutcome.Applied, 1)));
        Assert.Contains(
            "may already be restored",
            DiscardReports.FilesFailure("index.lock exists"),
            StringComparison.Ordinal);
    }

    private static DiscardSelectionRequested Selection(StatusEntry entry, IReadOnlyList<ChangedLine> lines, bool isRange) =>
        new(Tab, new StagingSelection(WorkingDiffSide.Unstaged, entry, lines, isRange), new DiffCursorMove(0, -1));

    private static ChangedLine Changed(int line, string text) => new(DiffLineKind.Added, line, text);

    private static ImmutableArray<AppEffect> Reduce(DiscardCommand command, DiscardContext context) =>
        DiscardReducer.Reduce(command, context);

    private static DiscardContext Context(
        WorkingTreeState? tree,
        PaneKind focus = PaneKind.WorkingUnstaged,
        bool isBusy = false,
        bool isBlocked = false,
        bool canDeleteInvalidNames = true) =>
        new(Tab, Location, focus, tree, isBusy, isBlocked, canDeleteInvalidNames);

    private static WorkingTreeState TreeOf(FilePathStyle style, params StatusEntry[] entries) =>
        WorkingTreeState.Reconcile(null, Snapshot(entries), style);

    private static WorkingTreeState Select(WorkingTreeState tree, WorkingTreeSection section, int row) =>
        tree with { Selection = tree.SelectionAt(section, row) };
}
