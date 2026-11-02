using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Commits;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Persistence;
using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Commits;

// The commit cycle end to end on a real temporary repository, driven the way the keys drive it: action, reducer, effect,
// runner, handler, real git, result command, status read.
public class CommitPipelineTests
{
    [Fact]
    public async Task GivenAStagedFile_WhenWritingAndCommitting_ThenHeadHasTheMessageAndTheTreeIsClean()
    {
        using var repository = Initial();
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        using var pipeline = await OpenAsync(repository);

        Run(pipeline, ActionIds.WorkingTreeWriteMessage);
        Type(pipeline, "Change a");
        Run(pipeline, ActionIds.WorkingTreeMessageNewLine);
        Run(pipeline, ActionIds.WorkingTreeMessageNewLine);
        Type(pipeline, "Because it needed changing.");
        Run(pipeline, ActionIds.WorkingTreeToggleSignOff);
        Run(pipeline, ActionIds.WorkingTreeCommit);
        Assert.True(IsCommitting(pipeline));
        await pipeline.UntilAsync(() => IsFinished(pipeline));

        var message = repository.Git("log", "-1", "--format=%B").TrimEnd();
        Assert.StartsWith("Change a\n\nBecause it needed changing.\n\nSigned-off-by: ", message, StringComparison.Ordinal);
        Assert.Equal("2", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Empty(repository.Git("status", "--porcelain"));
        Assert.StartsWith("Committed ", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.True(Draft(pipeline).Editor.IsBlank);
        Assert.True(Draft(pipeline).SignOff);
        Assert.Null(pipeline.Host.State.Shell.Output);
        Assert.Equal(KeyMode.Normal, pipeline.Mode);
    }

    [Fact]
    public async Task GivenAStagedFile_WhenAmendingWithANewMessage_ThenTheLastCommitIsReplacedAndTheOldOneIsNamed()
    {
        using var repository = Initial();
        var before = repository.Head;
        repository.WriteFile("b.txt", "b\n");
        repository.Git("add", "b.txt");
        using var pipeline = await OpenAsync(repository);

        Run(pipeline, ActionIds.WorkingTreeToggleAmend);
        await pipeline.UntilAsync(() => Draft(pipeline).Editor.Text == "Initial");
        pipeline.Dispatch(new CommitDraftRestored(pipeline.TabId, new SessionDraft("Initial, with b", false, false)));
        Run(pipeline, ActionIds.WorkingTreeCommit);
        await pipeline.UntilAsync(() => IsFinished(pipeline));

        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Equal("Initial, with b", repository.Git("log", "-1", "--format=%B").TrimEnd());
        var text = pipeline.Statuses.Log.Latest!.Text;
        Assert.Contains(before[..7], text, StringComparison.Ordinal);
        Assert.Contains($"git reset --soft {before[..7]}", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFailingPreCommitHook_WhenCommitting_ThenItsOutputIsShownAndTheDraftAndIndexSurvive()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = Initial();
        WriteHook(repository, "echo 'pre-commit: forbidden word in a.txt' >&2\nexit 1\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var before = repository.Head;
        using var pipeline = await OpenAsync(repository);
        Seed(pipeline, "Will be blocked");

        Run(pipeline, ActionIds.WorkingTreeCommit);
        await pipeline.UntilAsync(() => IsFinished(pipeline));

        Assert.Equal(before, repository.Head);
        Assert.Equal("Will be blocked", Draft(pipeline).Editor.Text);
        var output = pipeline.Host.State.Shell.Output!;
        Assert.Equal(CommitFailureReport.Title, output.Title);
        Assert.Contains("pre-commit: forbidden word in a.txt", output.Lines);
        Assert.Contains("M  a.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
    }

    [Fact]
    public async Task GivenAFailingHookAndNoVerify_WhenCommitting_ThenTheCommitGoesThrough()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = Initial();
        WriteHook(repository, "exit 1\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        using var pipeline = await OpenAsync(repository);
        Seed(pipeline, "Skip the hook");

        Run(pipeline, ActionIds.WorkingTreeToggleNoVerify);
        Run(pipeline, ActionIds.WorkingTreeCommit);
        await pipeline.UntilAsync(() => IsFinished(pipeline));

        Assert.Equal("Skip the hook", repository.Git("log", "-1", "--format=%B").TrimEnd());
        Assert.Null(pipeline.Host.State.Shell.Output);
    }

    [Fact]
    public async Task GivenAHookThatPrompts_WhenCommitting_ThenTheFailureOutputIsShownWithTheHint()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = Initial();
        WriteHook(
            repository,
            "printf 'Really commit? [y/N] ' >&2\nif ! read -r answer; then echo 'no answer: stdin is closed' >&2; exit 1; fi\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        using var pipeline = await OpenAsync(repository);
        Seed(pipeline, "Prompted");

        Run(pipeline, ActionIds.WorkingTreeCommit);
        await pipeline.UntilAsync(() => IsFinished(pipeline));

        var output = string.Join('\n', pipeline.Host.State.Shell.Output!.Lines);
        Assert.Contains("Really commit? [y/N]", output, StringComparison.Ordinal);
        Assert.Contains("no answer: stdin is closed", output, StringComparison.Ordinal);
        Assert.Contains("<space>ec", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNothingStaged_WhenCommitting_ThenTheDialogAsksAndNothingRunsYet()
    {
        using var repository = Initial();
        repository.WriteFile("a.txt", "changed\n");
        repository.WriteFile("new.txt", "new\n");
        using var pipeline = await OpenAsync(repository);
        Seed(pipeline, "Everything");

        Run(pipeline, ActionIds.WorkingTreeCommit);

        var prompt = pipeline.Confirm!.Prompt;
        Assert.Contains("Nothing is staged. Stage all changes and commit?", prompt.Lines);
        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Empty(repository.Git("diff", "--cached", "--name-only"));
    }

    [Fact]
    public async Task GivenTheDialog_WhenAnsweringYes_ThenEverythingIsStagedAndCommitted()
    {
        using var repository = Initial();
        repository.WriteFile("a.txt", "changed\n");
        repository.WriteFile("new.txt", "new\n");
        using var pipeline = await OpenAsync(repository);
        Seed(pipeline, "Everything");
        Run(pipeline, ActionIds.WorkingTreeCommit);

        pipeline.Answer(ConfirmAnswer.Yes);
        await pipeline.UntilAsync(() => Draft(pipeline).Editor.IsBlank && IsFinished(pipeline));

        Assert.Equal("Everything", repository.Git("log", "-1", "--format=%B").Trim());
        Assert.Equal("a.txt\nnew.txt", repository.Git("show", "--name-only", "--format=", "HEAD").Trim());
        Assert.Empty(repository.Git("status", "--porcelain"));
    }

    [Fact]
    public async Task GivenTheDialog_WhenAnsweringNo_ThenTheDraftAndTheIndexAreUntouched()
    {
        using var repository = Initial();
        repository.WriteFile("a.txt", "changed\n");
        using var pipeline = await OpenAsync(repository);
        Seed(pipeline, "Keep me");
        Run(pipeline, ActionIds.WorkingTreeCommit);

        pipeline.Answer(ConfirmAnswer.No);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Keep me", Draft(pipeline).Editor.Text);
        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Empty(repository.Git("diff", "--cached", "--name-only"));
        Assert.False(IsCommitting(pipeline));
    }

    [Fact]
    public async Task GivenNothingStagedAndAmendOn_WhenCommitting_ThenThereIsNoDialog()
    {
        using var repository = Initial();
        repository.WriteFile("a.txt", "changed\n");
        using var pipeline = await OpenAsync(repository);
        Run(pipeline, ActionIds.WorkingTreeToggleAmend);
        await pipeline.UntilAsync(() => Draft(pipeline).Editor.Text == "Initial");
        Seed(pipeline, "Reworded");

        Run(pipeline, ActionIds.WorkingTreeCommit);
        await pipeline.UntilAsync(() => IsFinished(pipeline));

        Assert.Null(pipeline.Confirm);
        Assert.Equal("Reworded", repository.Git("log", "-1", "--format=%B").Trim());
        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
    }

    [Fact]
    public async Task GivenNothingToCommitAtAll_WhenCommitting_ThenTheNothingStagedNoticeShowsWithoutADialog()
    {
        using var repository = Initial();
        using var pipeline = await OpenAsync(repository);
        Seed(pipeline, "Empty");

        Run(pipeline, ActionIds.WorkingTreeCommit);

        Assert.Null(pipeline.Confirm);
        Assert.Equal(CommitCheck.NothingStaged, pipeline.Statuses.Log.Latest!.Text);
    }

    private static TestRepository Initial() =>
        TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));

    private static CommitDraft Draft(FeaturePipeline pipeline) =>
        pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Commit.Draft ?? new CommitDraft();

    private static bool IsCommitting(FeaturePipeline pipeline) =>
        pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Running.IsRunning(RunningOperationKind.Commit);

    // The result is reduced and the status read again: nothing runs and the lists show what git has.
    private static bool IsFinished(FeaturePipeline pipeline) =>
        !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null;

    private static void Seed(FeaturePipeline pipeline, string text) =>
        pipeline.Dispatch(new CommitDraftRestored(
            pipeline.TabId,
            new SessionDraft(text, Draft(pipeline).SignOff, Draft(pipeline).NoVerify)));

    private static void Run(FeaturePipeline pipeline, ActionId action)
    {
        var state = pipeline.Host.State;
        var tab = pipeline.Host.Workspace.FindTab(pipeline.TabId)!;
        Assert.True(CommitRules.Takes(state, tab, action));
        pipeline.Dispatch(new CommitActionPressed(pipeline.TabId, action));
    }

    private static void Type(FeaturePipeline pipeline, string text)
    {
        foreach (var character in text)
        {
            pipeline.Dispatch(new CommitKeyTyped(pipeline.TabId, new KeyEvent(character.ToString())));
        }
    }

    private static void WriteHook(TestRepository repository, string script)
    {
        var path = Path.Combine(repository.WorkingTreePath, ".git", "hooks", "pre-commit");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"#!/bin/sh\n{script}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static async Task<FeaturePipeline> OpenAsync(TestRepository repository)
    {
        var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new OpenWorkingTreeCommand()));
        await pipeline.UntilAsync(() => pipeline.Host.Workspace.FindTab(pipeline.TabId)!.WorkingTree is not null);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new FocusPaneCommand(PaneKind.WorkingUnstaged)));

        return pipeline;
    }
}
