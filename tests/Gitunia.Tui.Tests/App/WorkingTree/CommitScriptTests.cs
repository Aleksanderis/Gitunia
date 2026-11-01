using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.WorkingTree;

/// <summary>The acceptance of #32: the commit cycle on a real temporary repository, driven the way the keys drive it.</summary>
public class CommitScriptTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAnEditedFile_WhenStagingWritingAndCommitting_ThenHeadHasTheMessageAndTheTreeIsClean()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        var session = await Session.OpenAsync(repository);

        await session.StageAllAsync();
        session.Run(ActionIds.WorkingTreeWriteMessage);
        session.Type("Change a");
        session.Run(ActionIds.WorkingTreeMessageNewLine);
        session.Run(ActionIds.WorkingTreeMessageNewLine);
        session.Type("Because it needed changing.");
        session.Run(ActionIds.WorkingTreeToggleSignOff);
        session.Run(ActionIds.WorkingTreeCommit);
        await session.WaitForCommitAsync();

        Assert.Equal("Change a\n\nBecause it needed changing.\n\nSigned-off-by: Test <test@example.com>", repository.Git("log", "-1", "--format=%B").TrimEnd());
        Assert.Equal("2", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Empty(repository.Git("status", "--porcelain"));
        Assert.StartsWith("Committed ", session.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.True(session.Draft.Editor.IsBlank);
        Assert.Empty(session.Outputs);
    }

    [Fact]
    public async Task GivenAStagedFile_WhenAmendingWithANewMessage_ThenTheLastCommitIsReplacedAndTheOldOneIsNamed()
    {
        using var repository = TestRepository.Create().Commit("First", new TestFile("a.txt", "a\n"));
        var before = repository.Head;
        repository.WriteFile("b.txt", "b\n");
        repository.Git("add", "b.txt");
        var session = await Session.OpenAsync(repository);

        session.Run(ActionIds.WorkingTreeToggleAmend);
        await session.WaitForHeadMessageAsync();
        Assert.Equal("First", session.Draft.Editor.Text);
        session.Draft.Editor.SetText("First, with b");
        session.Run(ActionIds.WorkingTreeCommit);
        await session.WaitForCommitAsync();

        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Equal("First, with b", repository.Git("log", "-1", "--format=%B").TrimEnd());
        Assert.Contains(before[..7], session.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Contains($"git reset --soft {before[..7]}", session.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFailingPreCommitHook_WhenCommitting_ThenItsOutputIsShownAndTheDraftAndIndexSurvive()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        WriteHook(repository, "echo 'pre-commit: forbidden word in a.txt' >&2\nexit 1\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var before = repository.Head;
        var session = await Session.OpenAsync(repository);
        session.Draft.Editor.SetText("Will be blocked");

        session.Run(ActionIds.WorkingTreeCommit);
        await session.WaitForCommitAsync();

        Assert.Equal(before, repository.Head);
        Assert.Equal("Will be blocked", session.Draft.Editor.Text);
        var output = Assert.Single(session.Outputs);
        Assert.Contains("pre-commit: forbidden word in a.txt", output, StringComparison.Ordinal);
        Assert.Contains("M  a.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, session.Statuses.Log.Latest!.Severity);
    }

    [Fact]
    public async Task GivenAFailingHookAndNoVerify_WhenCommitting_ThenTheCommitGoesThrough()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        WriteHook(repository, "exit 1\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var session = await Session.OpenAsync(repository);
        session.Draft.Editor.SetText("Skip the hook");

        session.Run(ActionIds.WorkingTreeToggleNoVerify);
        session.Run(ActionIds.WorkingTreeCommit);
        await session.WaitForCommitAsync();

        Assert.Equal("Skip the hook", repository.Git("log", "-1", "--format=%B").TrimEnd());
        Assert.Empty(session.Outputs);
    }

    [Fact]
    public async Task GivenAHookThatPromptsAndSigningThatNeedsATerminal_WhenCommitting_ThenTheFailureOutputIsShownWithTheHint()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The hook is a shell script.");
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        WriteHook(repository, "printf 'Really commit? [y/N] ' >&2\nif ! read -r answer; then echo 'no answer: stdin is closed' >&2; exit 1; fi\n");
        repository.WriteFile("a.txt", "changed\n");
        repository.Git("add", "a.txt");
        var session = await Session.OpenAsync(repository);
        session.Draft.Editor.SetText("Prompted");

        session.Run(ActionIds.WorkingTreeCommit);
        await session.WaitForCommitAsync();

        var output = Assert.Single(session.Outputs);
        Assert.Contains("Really commit? [y/N]", output, StringComparison.Ordinal);
        Assert.Contains("no answer: stdin is closed", output, StringComparison.Ordinal);
        Assert.Contains("<space>ec", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNothingStaged_WhenCommitting_ThenTheDialogAsksAndNothingRunsYet()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        repository.WriteFile("new.txt", "new\n");
        var session = await Session.OpenAsync(repository);
        session.Draft.Editor.SetText("Everything");

        session.Run(ActionIds.WorkingTreeCommit);

        var prompt = Assert.Single(session.Prompts);
        Assert.Contains("Nothing is staged. Stage all changes and commit?", prompt.Lines);
        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Empty(repository.Git("diff", "--cached", "--name-only"));
    }

    [Fact]
    public async Task GivenTheDialog_WhenAnsweringYes_ThenEverythingIsStagedAndCommitted()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        repository.WriteFile("new.txt", "new\n");
        var session = await Session.OpenAsync(repository);
        session.Draft.Editor.SetText("Everything");

        session.Run(ActionIds.WorkingTreeCommit);
        session.Confirm();
        await session.Pump.RunUntilAsync(() => session.Draft.Editor.IsBlank);

        Assert.Equal("Everything", repository.Git("log", "-1", "--format=%B").Trim());
        Assert.Equal("a.txt\nnew.txt", repository.Git("show", "--name-only", "--format=", "HEAD").Trim());
        Assert.Empty(repository.Git("status", "--porcelain"));
        Assert.True(session.Draft.Editor.IsBlank);
    }

    [Fact]
    public async Task GivenTheDialog_WhenAnsweringNo_ThenTheDraftAndTheIndexAreUntouched()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        var session = await Session.OpenAsync(repository);
        session.Draft.Editor.SetText("Keep me");

        session.Run(ActionIds.WorkingTreeCommit);
        session.Dismiss();

        Assert.Equal("Keep me", session.Draft.Editor.Text);
        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
        Assert.Empty(repository.Git("diff", "--cached", "--name-only"));
        Assert.False(session.Draft.IsCommitting);
    }

    [Fact]
    public async Task GivenNothingStagedAndAmendOn_WhenCommitting_ThenThereIsNoDialog()
    {
        using var repository = TestRepository.Create().Commit("First", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        var session = await Session.OpenAsync(repository);
        session.Run(ActionIds.WorkingTreeToggleAmend);
        await session.WaitForHeadMessageAsync();
        session.Draft.Editor.SetText("Reworded");

        session.Run(ActionIds.WorkingTreeCommit);
        await session.WaitForCommitAsync();

        Assert.Empty(session.Prompts);
        Assert.Equal("Reworded", repository.Git("log", "-1", "--format=%B").Trim());
        Assert.Equal("1", repository.Git("rev-list", "--count", "HEAD").Trim());
    }

    [Fact]
    public async Task GivenTheDialogDoesNotFit_WhenCommitting_ThenNothingStagedShowsAndNothingStarts()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        var session = await Session.OpenAsync(repository);
        session.DialogFits = false;
        session.Draft.Editor.SetText("Msg");

        session.Run(ActionIds.WorkingTreeCommit);

        Assert.Equal(CommitCheck.NothingStaged, session.Commits.Notice);
        Assert.Empty(repository.Git("diff", "--cached", "--name-only"));
    }

    [Fact]
    public async Task GivenNothingToCommitAtAll_WhenCommitting_ThenTheNothingStagedNoticeShowsWithoutADialog()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        var session = await Session.OpenAsync(repository);
        session.Draft.Editor.SetText("Empty");

        session.Run(ActionIds.WorkingTreeCommit);

        Assert.Empty(session.Prompts);
        Assert.Equal(CommitCheck.NothingStaged, session.Commits.Notice);
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

    private sealed class Session
    {
        private readonly TestShell _shell = new();
        private readonly GitRunner _runner;
        private readonly RepositoryLocation _location;

        private Session(GitRunner runner, RepositoryLocation location)
        {
            _runner = runner;
            _location = location;
            Pump = new UiPump(Token);
            Commits = new CommitActions(
                _shell.Host,
                new CommitWriter(runner),
                new RepositoryOperationDetector(),
                Statuses,
                Pump.Post,
                RefreshStatus,
                _ => Token,
                new TabOperationGate(),
                () => _shell.Mode,
                (_, _) => { },
                () => { });
            var indexWriter = new IndexWriter(runner);
            var staging = new StagingActions(
                _shell.Host,
                indexWriter,
                new PatchStager(new FakeDiffReader(), indexWriter),
                Statuses,
                Pump.Post,
                RefreshStatus,
                _ => Token);
            Commits.StageAllThen = staging.StageAllThen;
        }

        public bool DialogFits
        {
            get => _shell.CanShowDialog;
            set => _shell.CanShowDialog = value;
        }

        public List<ConfirmPrompt> Prompts => _shell.Prompts;

        public void Confirm() => Assert.True(_shell.Answer());

        public void Dismiss() => _shell.Decline();

        public UiPump Pump { get; }

        public StatusReporter Statuses => _shell.Statuses;

        public CommitActions Commits { get; }

        public List<string> Outputs => [.. _shell.Outputs.Select(output => string.Join('\n', output.Lines))];

        public CommitDraft Draft => Commits.DraftFor(Tab.Id);

        private RepositoryState Tab => _shell.Host.Workspace.ActiveTab!;

        public static async Task<Session> OpenAsync(TestRepository repository)
        {
            var runner = repository.CreateIsolatedRunner();
            var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
            var session = new Session(runner, location);
            session._shell.Host.Dispatch(new OpenRepositoryCommand(location));
            await session.ReloadStatusAsync();
            session.Send(new OpenWorkingTreeCommand());
            return session;
        }

        public async Task StageAllAsync()
        {
            var paths = Tab.WorkingTree!.Unstaged.Select(entry => entry.Path).ToList();
            await new IndexWriter(_runner).StageAsync(_location, paths, Token);
            await ReloadStatusAsync();
        }

        public void Run(ActionId action) => Commits.TryExecute(Tab, action);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Commits.HandleTextKey(new KeyEvent(character.ToString())));
            }
        }

        public Task WaitForCommitAsync() => Pump.RunUntilAsync(() => !Draft.IsCommitting && Statuses.Log.Latest is not null);

        public Task WaitForHeadMessageAsync() => Pump.RunPostsAsync(1);

        private async Task ReloadStatusAsync()
        {
            var snapshot = await new StatusReader(_runner).ReadStatusAsync(_location, false, UntrackedFilesMode.All, Token);
            Send(new WorkingTreeLoadedCommand(snapshot));
        }

        // Stands in for the tab refresher: reads the status of the real repository, then reports back on the "UI thread".
        private bool RefreshStatus(TabId tabId, Action done)
        {
            _ = Task.Run(async () =>
            {
                var snapshot = await new StatusReader(_runner).ReadStatusAsync(_location, false, UntrackedFilesMode.All, Token);
                Pump.Post(() =>
                {
                    Send(new WorkingTreeLoadedCommand(snapshot));
                    done();
                });
            }, Token);
            return true;
        }

        private void Send(RepositoryCommand command) =>
            _shell.Host.Dispatch(new TabCommand(_shell.Host.Workspace.ActiveTabId!.Value, command));
    }
}
