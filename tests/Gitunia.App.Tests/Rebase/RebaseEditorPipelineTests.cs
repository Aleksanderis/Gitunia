using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Rebase;
using Gitunia.Core.Running;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Operations;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Rebase;

// The interactive rebase editor end to end: command -> reducer -> effect -> runner -> handler -> real git -> dialog.
public class RebaseEditorPipelineTests
{
    [Fact]
    public async Task GivenABranchAheadOfTheTarget_WhenOpening_ThenTheTodoListsTheCommitsAndTheTabIsFreeAgain()
    {
        using var repository = ThreeCommits();
        using var pipeline = await FeaturePipeline.OpenAsync(Services(), repository);

        pipeline.Dispatch(Open(pipeline, MainTip(repository)));
        Assert.True(IsRebaseRunning(pipeline));
        await pipeline.UntilAsync(() => Editor(pipeline) is not null);

        var state = Editor(pipeline)!;
        Assert.Equal(["add a", "add b", "add c"], state.Rows.Select(row => row.Subject));
        Assert.Equal(MainTip(repository)[..7], state.TargetLabel);
        Assert.Equal(repository.Head, state.OldTip);
        Assert.False(pipeline.IsBusy);
        Assert.False(pipeline.Host.State.Shell.RebaseEditor.IsReading);
    }

    [Fact]
    public async Task GivenASidebarBranch_WhenOpening_ThenTheTodoNamesTheBranch()
    {
        using var repository = ThreeCommits();
        using var pipeline = await FeaturePipeline.OpenAsync(Services(), repository);

        pipeline.Dispatch(Open(pipeline, new RebaseSelection(SidebarRowKind.LocalBranch, "main", MainTip(repository))));
        await pipeline.UntilAsync(() => Editor(pipeline) is not null);

        Assert.Equal("main", Editor(pipeline)!.TargetLabel);
    }

    [Fact]
    public async Task GivenReorderFixupAndReword_WhenConfirmed_ThenGitReplaysTheTodoAndTheStatusNamesTheUndo()
    {
        using var repository = ThreeCommits().Commit("add d", new TestFile("d.txt", "d\n"));
        var oldTip = repository.Head;
        using var pipeline = await FeaturePipeline.OpenAsync(Services(), repository);
        await OpenEditorAsync(pipeline, MainTip(repository));

        // Rows: a, b, c, d. Move c above b, fix d up into b, reword a.
        Press(pipeline, ActionIds.ListDown, 2);
        Press(pipeline, ActionIds.RebaseEditorMoveUp);
        Press(pipeline, ActionIds.ListBottom);
        Press(pipeline, ActionIds.RebaseEditorFixup);
        Press(pipeline, ActionIds.ListTop);
        await RewordAsync(pipeline, "a reworded\n\nwith a body");
        Press(pipeline, ActionIds.RebaseEditorStart);
        Assert.Equal("Interactive rebase", pipeline.Confirm!.Prompt.Title);
        pipeline.Answer(ConfirmAnswer.Yes);
        Assert.True(IsRebaseRunning(pipeline));
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.Null(Editor(pipeline));
        Assert.Equal(
            ["add b", "add c", "a reworded"],
            repository.Git("log", "--format=%s", "main..HEAD").Trim().Split('\n'));
        Assert.Equal("with a body", repository.Git("log", "-1", "--format=%b", "HEAD~2").Trim());
        var latest = pipeline.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Info, latest.Severity);
        Assert.DoesNotContain("git reset", latest.Text, StringComparison.Ordinal);
        Assert.Equal($"Undo: git reset --keep {oldTip} (or ORIG_HEAD, the reflog)", latest.Detail);
        Assert.Equal(RepositoryOperation.None, new RepositoryOperationDetector().Detect(LocationOf(pipeline)));
    }

    [Fact]
    public async Task GivenNoSelection_WhenOpeningTheRootEditor_ThenTheWholeHistoryIsListedAndRewordingTheRootWorks()
    {
        using var repository = ThreeCommits();
        using var pipeline = await FeaturePipeline.OpenAsync(Services(), repository);

        pipeline.Dispatch(new RebaseEditorOpenRequested(pipeline.TabId, new RebaseSelection(null, null, null), true));
        await pipeline.UntilAsync(() => Editor(pipeline) is not null);

        Assert.True(Editor(pipeline)!.Base.IsRoot);
        Assert.Equal(["Initial", "add a", "add b", "add c"], Editor(pipeline)!.Rows.Select(row => row.Subject));
        await RewordAsync(pipeline, "root reworded");
        Press(pipeline, ActionIds.RebaseEditorStart);
        Assert.Contains(
            "Rebase topic onto the root with this todo?",
            pipeline.Confirm!.Prompt.Lines,
            StringComparer.Ordinal);
        pipeline.Answer(ConfirmAnswer.Yes);
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.Equal(
            ["root reworded", "add a", "add b", "add c"],
            repository.Git("log", "--format=%s", "--reverse").Trim().Split('\n'));
        Assert.Equal(StatusSeverity.Info, pipeline.Statuses.Log.Latest!.Severity);
    }

    [Fact]
    public async Task GivenADroppedCommit_WhenStarting_ThenTheConfirmNamesItAndNothingChangesBeforeTheAnswer()
    {
        using var repository = ThreeCommits();
        var oldTip = repository.Head;
        using var pipeline = await FeaturePipeline.OpenAsync(Services(), repository);
        await OpenEditorAsync(pipeline, MainTip(repository));
        Press(pipeline, ActionIds.ListDown);
        Press(pipeline, ActionIds.RebaseEditorDrop);

        Press(pipeline, ActionIds.RebaseEditorStart);

        var prompt = pipeline.Confirm!.Prompt;
        Assert.Contains("2 of 3 commits kept, 1 dropped", prompt.Lines, StringComparer.Ordinal);
        Assert.Contains(prompt.Lines, line => line.Contains("add b", StringComparison.Ordinal));
        Assert.Equal(oldTip, repository.Head);
        Assert.False(pipeline.IsBusy);
    }

    [Fact]
    public async Task GivenAConflictingReorder_WhenStarting_ThenARebaseIsInProgressAndTheStatusNamesTheKeys()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("f.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("first", new TestFile("f.txt", "first\n"))
            .Commit("second", new TestFile("f.txt", "second\n"));
        var oldTip = repository.Head;
        using var pipeline = await FeaturePipeline.OpenAsync(Services(), repository);
        await OpenEditorAsync(pipeline, MainTip(repository));
        Press(pipeline, ActionIds.ListDown);
        Press(pipeline, ActionIds.RebaseEditorMoveUp);

        Press(pipeline, ActionIds.RebaseEditorStart);
        pipeline.Answer(ConfirmAnswer.Yes);
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.Equal(RepositoryOperation.Rebase, new RepositoryOperationDetector().Detect(LocationOf(pipeline)));
        var latest = pipeline.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("<space>gc", latest.Text, StringComparison.Ordinal);
        Assert.Contains(oldTip[..7], latest.Text, StringComparison.Ordinal);
        Assert.Null(Editor(pipeline));
    }

    [Fact]
    public async Task GivenGitSequenceEditorIsSet_WhenStarting_ThenTheStatusExplainsAndTheTodoComesBack()
    {
        using var repository = ThreeCommits();
        var oldTip = repository.Head;
        using var pipeline = await FeaturePipeline.OpenAsync(Services("vim"), repository);
        await OpenEditorAsync(pipeline, MainTip(repository));
        Press(pipeline, ActionIds.RebaseEditorDrop);
        Press(pipeline, ActionIds.RebaseEditorStart);

        pipeline.Answer(ConfirmAnswer.Yes);
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        var latest = pipeline.Statuses.Log.Latest!;
        Assert.Contains("GIT_SEQUENCE_EDITOR", latest.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, latest.Severity);
        Assert.Equal(RebaseTodoAction.Drop, Editor(pipeline)!.Rows[0].Action);
        Assert.Equal(oldTip, repository.Head);
    }

    [Fact]
    public async Task GivenTheMessageEditor_WhenTypingAndSaving_ThenTheKeyModeFollowsAndTheCommitIsAReword()
    {
        using var repository = ThreeCommits();
        using var pipeline = await FeaturePipeline.OpenAsync(Services(), repository);
        await OpenEditorAsync(pipeline, MainTip(repository));

        Press(pipeline, ActionIds.RebaseEditorReword);
        Assert.True(pipeline.Host.State.Shell.RebaseEditor.IsReading);
        await pipeline.UntilAsync(() => Editor(pipeline)!.Edit is not null);

        Assert.Equal(KeyMode.Insert, pipeline.Mode);
        Assert.Equal("add a", Editor(pipeline)!.Edit!.Editor.Text);
        pipeline.Dispatch(new RebaseEditorKeyTyped(pipeline.TabId, new KeyEvent("!")));
        Press(pipeline, ActionIds.RebaseEditorMessageSave);

        Assert.Equal(KeyMode.Normal, pipeline.Mode);
        Assert.Equal("add a!", Editor(pipeline)!.Rows[0].Message);
        Assert.Equal(RebaseTodoAction.Reword, Editor(pipeline)!.Rows[0].Action);
    }

    [Fact]
    public async Task GivenTheRangeIsBeingRead_WhenTheTabIsClosed_ThenNothingOpensAndTheNextOpenIsNotRefused()
    {
        using var repository = ThreeCommits();
        using var pipeline = await FeaturePipeline.OpenAsync(Services(), repository);
        pipeline.Dispatch(Open(pipeline, MainTip(repository)));
        pipeline.Dispatch(new CloseTabCommand(pipeline.TabId));

        await pipeline.UntilAsync(() => !pipeline.Host.State.Shell.RebaseEditor.IsReading);

        Assert.Null(Editor(pipeline));
    }

    private static string MainTip(TestRepository repository) => repository.Git("rev-parse", "main").Trim();

    private static RebaseEditorState? Editor(FeaturePipeline pipeline) =>
        pipeline.Host.State.Shell.RebaseEditor.Editor;

    private static RebaseEditorOpenRequested Open(FeaturePipeline pipeline, string commit) =>
        Open(pipeline, new RebaseSelection(null, null, commit));

    private static RebaseEditorOpenRequested Open(FeaturePipeline pipeline, RebaseSelection selection) =>
        new(pipeline.TabId, selection, false);

    private static void Press(FeaturePipeline pipeline, ActionId action, int? count = null) =>
        pipeline.Dispatch(new RebaseEditorActionPressed(pipeline.TabId, action, count, 10));

    private static async Task OpenEditorAsync(FeaturePipeline pipeline, string target)
    {
        pipeline.Dispatch(Open(pipeline, target));
        await pipeline.UntilAsync(() => Editor(pipeline) is not null || pipeline.Statuses.Log.Latest is not null);
        Assert.NotNull(Editor(pipeline));
    }

    // Rewords the highlighted commit: reads its message, then replaces the text and keeps it.
    private static async Task RewordAsync(FeaturePipeline pipeline, string message)
    {
        Press(pipeline, ActionIds.RebaseEditorReword);
        await pipeline.UntilAsync(() => Editor(pipeline)!.Edit is not null);
        foreach (var _ in Editor(pipeline)!.Edit!.Editor.Text)
        {
            pipeline.Dispatch(new RebaseEditorKeyTyped(pipeline.TabId, new KeyEvent(KeyNames.Backspace)));
        }

        foreach (var character in message)
        {
            var key = character == '\n' ? KeyNames.Enter : character.ToString();
            pipeline.Dispatch(new RebaseEditorKeyTyped(pipeline.TabId, new KeyEvent(key)));
        }

        Press(pipeline, ActionIds.RebaseEditorMessageSave);
    }

    private static RepositoryLocation LocationOf(FeaturePipeline pipeline) =>
        pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Location;

    private static bool IsRebaseRunning(FeaturePipeline pipeline) =>
        pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Running.IsRunning(RunningOperationKind.Rebase);

    // The engine writes its todo through a copy command instead of the app binary, which tests do not have.
    private static GitServices Services(string? sequenceEditor = null)
    {
        var runner = IsolatedGit.CreateRunner();
        var detector = new RepositoryOperationDetector();
        var interactive = new InteractiveRebaseWriter(
            new OperationRunner(runner, detector),
            runner,
            detector,
            new SequenceEditorCommand(["cp"]),
            name => name == "GIT_SEQUENCE_EDITOR" ? sequenceEditor : null);

        return TestGit.Services with { InteractiveRebase = interactive };
    }

    // main has "Initial"; topic adds a.txt, b.txt, c.txt in three commits.
    private static TestRepository ThreeCommits() =>
        TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("add a", new TestFile("a.txt", "a\n"))
            .Commit("add b", new TestFile("b.txt", "b\n"))
            .Commit("add c", new TestFile("c.txt", "c\n"));
}
