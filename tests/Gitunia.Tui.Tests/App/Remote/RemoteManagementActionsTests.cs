using Gitunia.App.Status;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Remote;

public class RemoteManagementActionsTests
{
    [Fact]
    public async Task GivenANameAndAUrl_WhenAdding_ThenTheRemoteIsConfiguredAndTheTabRefreshed()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteAdd);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        Assert.Contains("Name of the new remote", PopupText.Of(harness.Actions), StringComparison.Ordinal);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("backup");
        harness.Run(ActionIds.InputAccept);
        Assert.Contains("URL of backup", PopupText.Of(harness.Actions), StringComparison.Ordinal);
        harness.Type("https://example.com/backup.git");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("https://example.com/backup.git", repository.Git("config", "remote.backup.url").Trim());
        Assert.Equal(1, harness.Refreshes);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Contains("Added remote backup", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnExistingName_WhenAdding_ThenItIsRefusedBeforeAskingForTheUrl()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteAdd);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Type("origin");
        harness.Run(ActionIds.InputAccept);

        Assert.Equal("Remote origin already exists", harness.Actions.Notice);
        Assert.False(harness.HasPopup);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenNoUrl_WhenAdding_ThenNothingIsAdded()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteAdd);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Type("backup");
        harness.Run(ActionIds.InputAccept);
        harness.Run(ActionIds.InputAccept);

        Assert.Equal("No URL: nothing was added", harness.Actions.Notice);
        Assert.Equal(string.Empty, repository.Git("remote").Trim());
    }

    [Fact]
    public async Task GivenARemote_WhenRemoving_ThenTheConfirmNamesTheTrackingBranchesAndNothingChangesUntilYes()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteRemove);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        Assert.Contains("Remote to remove", PopupText.Of(harness.Actions), StringComparison.Ordinal);
        harness.Run(ActionIds.InputAccept);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("origin", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains(repository.OriginPath, prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("2 remote-tracking branches disappear", prompt.Lines[1], StringComparison.Ordinal);
        Assert.Contains("origin/main, origin/topic", prompt.Lines[1], StringComparison.Ordinal);
        Assert.Contains($"git remote add origin {repository.OriginPath}", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal("origin", repository.Git("remote").Trim());

        harness.Answer();
        await harness.FinishAsync();

        Assert.Equal(string.Empty, repository.Git("remote").Trim());
        Assert.Equal(string.Empty, repository.Git("branch", "-r").Trim());
        Assert.Equal(1, harness.Refreshes);
        Assert.Contains("Removed remote origin", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARemoteBranchRowIsSelected_WhenRemoving_ThenItsRemoteIsConfirmedWithoutAPicker()
    {
        using var repository = TestRepositories.WithOrigin();
        repository.Git("remote", "add", "other", "somewhere");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.RemoteBranch, "origin/main", repository.Head));

        harness.Run(ActionIds.RemoteRemove);
        await harness.PumpUntilAsync(() => harness.Prompts.Count > 0);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("Remove the remote origin", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("1 remote-tracking branch disappears: origin/main", prompt.Lines[1], StringComparison.Ordinal);
        Assert.False(harness.HasPopup);
    }

    [Fact]
    public async Task GivenNoAnswer_WhenRemoving_ThenTheRemoteStays()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteRemove);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Run(ActionIds.InputAccept);

        Assert.Single(harness.Prompts);
        Assert.Equal("origin", repository.Git("remote").Trim());
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenNoRemote_WhenRemoving_ThenItSaysSoWithoutAPrompt()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteRemove);
        await harness.PumpUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Equal("There is no remote: add one first", harness.Statuses.Log.Latest!.Text);
        Assert.False(harness.HasPopup);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenARemote_WhenRenaming_ThenGitMovesItsTrackingBranches()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteRename);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Run(ActionIds.InputAccept);
        Assert.Contains("New name for origin", PopupText.Of(harness.Actions), StringComparison.Ordinal);
        harness.Type("upstream");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("upstream", repository.Git("remote").Trim());
        Assert.Contains("upstream/topic", repository.Git("branch", "-r"), StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenTheNameOfAnotherRemote_WhenRenaming_ThenItIsRefused()
    {
        using var repository = TestRepositories.WithOrigin();
        repository.Git("remote", "add", "other", "somewhere");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.RemoteBranch, "origin/main", repository.Head));

        harness.Run(ActionIds.RemoteRename);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Type("other");
        harness.Run(ActionIds.InputAccept);

        Assert.Equal("Remote other already exists", harness.Actions.Notice);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenARemote_WhenSettingTheUrl_ThenThePromptStartsWithTheCurrentUrlAndTheNewOneIsStored()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteSetUrl);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Run(ActionIds.InputAccept);
        Assert.Contains($"> {repository.OriginPath}_", PopupText.Of(harness.Actions), StringComparison.Ordinal);
        harness.Type("2");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal($"{repository.OriginPath}2", repository.Git("config", "remote.origin.url").Trim());
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenTheSameUrl_WhenSettingIt_ThenNothingRuns()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteSetUrl);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Run(ActionIds.InputAccept);
        harness.Run(ActionIds.InputAccept);

        Assert.Equal("Same or empty URL: nothing was changed", harness.Actions.Notice);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenAFetchRuns_WhenManagingRemotes_ThenItIsRefusedAndNothingChanges()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = await Harness.LoadAsync(repository);
        harness.RemoteRuns = true;

        harness.Run(ActionIds.RemoteRemove);

        Assert.Contains("still running", harness.Actions.Notice, StringComparison.Ordinal);
        Assert.False(harness.HasPopup);
        Assert.Equal("origin", repository.Git("remote").Trim());
    }

    [Fact]
    public async Task GivenAnOpenPrompt_WhenEscIsPressed_ThenItClosesAndNothingChanges()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteAdd);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Type("x");
        harness.Run(ActionIds.Cancel);

        Assert.False(harness.HasPopup);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal("origin", repository.Git("remote").Trim());
    }

    [Fact]
    public async Task GivenGitRefusesTheName_WhenAdding_ThenTheFailureIsReportedAndTheTabRefreshed()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteAdd);
        await harness.PumpUntilAsync(() => harness.HasPopup);
        harness.Type("bad name");
        harness.Run(ActionIds.InputAccept);
        harness.Type("x");
        harness.Run(ActionIds.InputAccept);
        await harness.PumpUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("Could not add the remote", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("remote").Trim());
    }

    [Fact]
    public async Task GivenAnotherAction_WhenNoPromptIsOpen_ThenTheManagementActionsDoNotHandleIt()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);

        Assert.False(harness.Actions.TryExecute(ActionIds.InputAccept, null));
        Assert.False(harness.Actions.TryExecute(ActionIds.RemoteFetch, null));
    }

    private sealed class Harness : IDisposable
    {
        private readonly TestShell _shell = new();
        private readonly List<TerminalTabSession> _sessions = [];
        private BranchSelection _selection = new(null, null, null);

        private Harness()
        {
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Actions = new RemoteManagementActions(
                _shell.Host,
                TestGit.Services.RemoteManager,
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                new TabOperationGate(),
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                () => { },
                () => _selection,
                _ => RemoteRuns);
        }

        public RemoteManagementActions Actions { get; }

        public StatusReporter Statuses => _shell.Statuses;

        public List<ConfirmPrompt> Prompts => _shell.Prompts;

        public bool RemoteRuns { get; set; }

        public int Refreshes { get; private set; }

        public KeyMode Mode => _shell.Mode;

        public bool HasPopup => Actions.Popup is not null;

        private UiPump Pump { get; }

        public static async Task<Harness> LoadAsync(TestRepository repository)
        {
            var harness = new Harness();
            await harness.OpenTabAsync(repository);
            return harness;
        }

        public void Select(BranchSelection selection) => _selection = selection;

        public void Run(ActionId action) => Assert.True(Actions.TryExecute(action, null), action.Value);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Actions.HandleTextKey(new KeyEvent(character.ToString())));
            }
        }

        /// <summary>Presses <c>y</c> in the open dialog.</summary>
        public void Answer() => _shell.Answer();


        public Task FinishAsync() => PumpUntilAsync(() => Statuses.Log.Latest is not null && Refreshes > 0
            || Statuses.Log.Latest?.Severity == StatusSeverity.Error);

        public Task PumpUntilAsync(Func<bool> condition) => Pump.RunUntilAsync(condition);

        public void Dispose()
        {
            foreach (var session in _sessions)
            {
                session.Dispose();
            }
        }

        private async Task OpenTabAsync(TestRepository repository)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            _shell.Host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = _shell.Host.Workspace.ActiveTabId!.Value;
            var session = new TerminalTabSession(
                new TabSessionParts(
                    tabId,
                    new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero)),
                GraphGlyphSet.Unicode);
            _sessions.Add(session);
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                session,
                _shell.Host,
                new StatusReporter(_shell.Host),
                InlineUi.Post,
                session.Cancellation.Token);
        }
    }
}
