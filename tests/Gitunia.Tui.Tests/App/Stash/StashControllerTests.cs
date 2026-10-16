using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Stash;

public class StashControllerTests
{
    [Fact]
    public void GivenAStashRow_WhenDropping_ThenTheDialogNamesItAndTheUndoCommandAndNothingRunsYet()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.StashDrop);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Drop stash", prompt.Title);
        Assert.Equal(["Drop stash@{0}: On main: \"quoted\" work?"], prompt.Lines);
        Assert.Equal("Undo: git stash store -m \"On main: \\\"quoted\\\" work\" abc123", prompt.Consequence);
        Assert.Empty(harness.Writer.Calls);
    }

    [Fact]
    public void GivenTheDropDialogIsNotAnswered_WhenLooking_ThenNothingWasDropped()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.StashDrop);

        Assert.Empty(harness.Writer.Calls);
        Assert.False(harness.Gate.IsAnyBusy);
    }

    [Fact]
    public async Task GivenTheDropIsConfirmed_WhenItFinishes_ThenTheMessageHasHashAndRestoreCommandAndTheTabRefreshes()
    {
        using var harness = new Harness();
        harness.Execute(ActionIds.StashDrop);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["drop|stash@{0}"], harness.Writer.Calls);
        var message = harness.Statuses.Log.Latest!.Text;
        Assert.Contains("Dropped stash@{0} (abc123)", message, StringComparison.Ordinal);
        Assert.Contains("git stash store -m", message, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Detail)]
    public void GivenFocusOutsideTheSidebar_WhenApplyingPoppingOrDropping_ThenItIsRefusedWithoutGit(PaneKind pane)
    {
        using var harness = new Harness();
        harness.Focus(pane);

        foreach (var action in new[] { ActionIds.StashApply, ActionIds.StashPop, ActionIds.StashDrop })
        {
            Assert.True(harness.Controller.TryExecute(action, null));
            Assert.Equal("Select a stash in the sidebar first", harness.Controller.Notice);
        }

        Assert.Empty(harness.Writer.Calls);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public void GivenANonStashRow_WhenPopping_ThenItIsRefused()
    {
        var branch = new SidebarRow(
            SidebarRowKind.LocalBranch, SidebarSection.Branches, "main", string.Empty, "1", true, false, 0, null, null);
        using var harness = new Harness { Row = branch };

        harness.Execute(ActionIds.StashPop);

        Assert.Equal("Select a stash in the sidebar first", harness.Controller.Notice);
        Assert.Empty(harness.Writer.Calls);
    }

    [Fact]
    public async Task GivenAConflictingPop_WhenItFinishes_ThenTheWarningSaysTheStashIsKept()
    {
        using var harness = new Harness();
        harness.Writer.Outcome = StashOutcome.Conflicted;

        harness.Execute(ActionIds.StashPop);
        await harness.WaitAsync();

        Assert.Equal(["pop|stash@{0}"], harness.Writer.Calls);
        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("with conflicts", latest.Text, StringComparison.Ordinal);
        Assert.Contains("stash is kept", latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACleanPop_WhenItFinishes_ThenTheMessageCarriesTheRestoreCommand()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.StashPop);
        await harness.WaitAsync();

        var message = harness.Statuses.Log.Latest!.Text;
        Assert.StartsWith("Popped stash@{0}; undo the drop: git stash store", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenApply_WhenItFinishes_ThenItSaysTheStashIsKept()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.StashApply);
        await harness.WaitAsync();

        Assert.Equal(["apply|stash@{0}"], harness.Writer.Calls);
        Assert.Equal("Applied stash@{0}; the stash is kept", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenGitRefuses_WhenApplying_ThenTheErrorShowsGitsReasonAndTheTabRefreshes()
    {
        using var harness = new Harness();
        harness.Writer.Failure = new GitCommandFailedException(
            ["stash", "apply"],
            1,
            "error: Your local changes would be overwritten\nmore");

        harness.Execute(ActionIds.StashApply);
        await harness.WaitAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Error, latest.Severity);
        Assert.Equal("Could not apply the stash: error: Your local changes would be overwritten", latest.Text);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenTheEntryIsGone_WhenDropping_ThenItWarnsAndSaysNothingChanged()
    {
        using var harness = new Harness();
        harness.Writer.Outcome = StashOutcome.Missing;
        harness.Execute(ActionIds.StashDrop);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("no longer in the stash list", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheMessageIsTyped_WhenPushing_ThenGitGetsItWithoutUntrackedFiles()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.StashPush);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("wip");
        Assert.Contains("wip", harness.Controller.StatusLine, StringComparison.Ordinal);
        Assert.Contains("tracked changes", harness.Controller.StatusLine, StringComparison.Ordinal);
        harness.Execute(ActionIds.InputAccept);
        await harness.WaitAsync();

        Assert.Equal(["push|wip|False"], harness.Writer.Calls);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Controller.StatusLine);
        Assert.StartsWith("Stashed \"wip\" as 01234567", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheUntrackedKey_WhenPushing_ThenGitIsAskedToIncludeUntrackedFiles()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.StashPushUntracked);
        Assert.Contains("untracked", harness.Controller.StatusLine, StringComparison.Ordinal);
        harness.Execute(ActionIds.InputAccept);
        await harness.WaitAsync();

        Assert.Equal(["push||True"], harness.Writer.Calls);
    }

    [Fact]
    public void GivenThePromptIsOpen_WhenCancelling_ThenNothingRuns()
    {
        using var harness = new Harness();
        harness.Execute(ActionIds.StashPush);
        harness.Type("wip");

        harness.Execute(ActionIds.Cancel);

        Assert.Empty(harness.Writer.Calls);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Controller.StatusLine);
    }

    [Fact]
    public async Task GivenNothingToStash_WhenPushingWithoutUntracked_ThenTheWarningPointsAtTheUntrackedKey()
    {
        using var harness = new Harness();
        harness.Writer.Outcome = StashOutcome.NothingToStash;
        harness.Execute(ActionIds.StashPush);
        harness.Execute(ActionIds.InputAccept);

        await harness.WaitAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("Nothing to stash", latest.Text, StringComparison.Ordinal);
        Assert.Contains("<space>Su", latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnotherOperationRuns_WhenStashing_ThenItIsRefusedBusy()
    {
        using var harness = new Harness();
        harness.Gate.Begin(harness.TabId);

        harness.Execute(ActionIds.StashPush);

        Assert.StartsWith("Busy", harness.Controller.Notice, StringComparison.Ordinal);
        Assert.Equal(string.Empty, harness.Controller.StatusLine);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenTheControllerLeavesItAlone()
    {
        using var harness = new Harness();

        Assert.False(harness.Controller.TryExecute(ActionIds.ListDown, null));
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];

        public Harness()
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-stash-controller-tests");
            var gitDirectory = Path.Combine(root, ".git");
            var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
            _host.Dispatch(new OpenRepositoryCommand(location));
            TabId = _host.Workspace.ActiveTabId!.Value;
            Entry = new StashEntry(0, "stash@{0}", "abc123", "On main: \"quoted\" work");
            Send(new StashesLoadedCommand([Entry]));
            Send(new FocusPaneCommand(PaneKind.Sidebar));
            Row = new SidebarRow(
                SidebarRowKind.Stash,
                SidebarSection.Stashes,
                Entry.Name,
                Entry.Message,
                Entry.CommitHash,
                false,
                false,
                0,
                null,
                null);
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Controller = new StashController(
                _host,
                Writer,
                Statuses,
                Pump.Post,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                _ => TestContext.Current.CancellationToken,
                Confirm,
                _ => Row,
                mode => Mode = mode,
                () => { },
                Gate);
        }

        public TabId TabId { get; }

        public StashEntry Entry { get; }

        public UiPump Pump { get; }

        public FakeStashWriter Writer { get; } = new();

        public StatusChannel Statuses { get; } = new();

        public TabOperationGate Gate { get; } = new();

        public StashController Controller { get; }

        public List<ConfirmPrompt> Prompts { get; } = [];

        public SidebarRow? Row { get; init; }

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        public int Refreshes { get; private set; }

        public void Execute(ActionId action) => Controller.TryExecute(action, null);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Controller.HandleTextKey(new KeyEvent(character.ToString()));
            }
        }

        public void Focus(PaneKind pane) => Send(new FocusPaneCommand(pane));

        /// <summary>Presses <c>y</c> in the open dialog.</summary>
        public void Answer()
        {
            foreach (var answer in _answers)
            {
                answer();
            }

            _answers.Clear();
        }

        public Task WaitAsync() => Pump.RunUntilAsync(() => !Gate.IsAnyBusy && Statuses.Log.Latest is not null);

        public void Dispose()
        {
        }

        private bool Confirm(ConfirmPrompt prompt, Action onConfirmed)
        {
            Prompts.Add(prompt);
            _answers.Add(onConfirmed);
            return true;
        }

        private void Send(RepositoryCommand command) => _host.Dispatch(new TabCommand(TabId, command));
    }
}
