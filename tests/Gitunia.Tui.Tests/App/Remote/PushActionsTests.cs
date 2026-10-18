using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Remote;

public class PushActionsTests
{
    [Fact]
    public async Task GivenABranchWithoutUpstream_WhenPushing_ThenTheRemoteIsAskedAndTheUpstreamIsSet()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemotePush);
        await harness.PumpUntilAsync(() => PopupText.Of(harness.Push).Length > 0);
        Assert.Contains("Remote to push lonely to", PopupText.Of(harness.Push), StringComparison.Ordinal);
        Assert.Contains("origin (1/1)", PopupText.Of(harness.Push), StringComparison.Ordinal);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("origin/lonely", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
        Assert.Equal(repository.Head, repository.GitIn(repository.OriginPath, ["rev-parse", "lonely"]).Trim());
        Assert.Contains("set it as its upstream", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenTheFirstPushPrompt_WhenCancelling_ThenNothingIsPushed()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemotePush);
        await harness.PumpUntilAsync(() => PopupText.Of(harness.Push).Length > 0);
        harness.Run(ActionIds.Cancel);

        Assert.Null(harness.Push.Popup);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.DoesNotContain("lonely", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenABranchWithAnUpstream_WhenPushing_ThenThePlainPushIsLeftToTheRemoteActions()
    {
        using var repository = TestRepositories.WithTrackingBranch(1, 0);
        using var harness = await Harness.LoadAsync(repository);

        Assert.False(harness.Push.TryExecute(ActionIds.RemotePush, null));
        Assert.Null(harness.Push.Popup);
    }

    [Fact]
    public async Task GivenARewrittenBranch_WhenForcePushing_ThenTheConfirmNamesTheRemoteBranchAndTheLeaseOverwritesIt()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        using var harness = await Harness.LoadAsync(repository);
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");

        harness.Run(ActionIds.RemotePushForce);
        await harness.PumpUntilAsync(() => harness.Prompts.Count > 0);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("origin/topic", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains(seen[..7], prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("lease", prompt.Lines[1], StringComparison.Ordinal);
        Assert.Contains($"{seen}:topic", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(seen, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());

        harness.Answer();
        await harness.FinishAsync();

        Assert.Equal(repository.Head, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
        Assert.StartsWith(
            "Force pushed topic to origin/topic",
            harness.Statuses.Log.Latest!.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheRemoteMovedMeanwhile_WhenForcePushing_ThenTheLeaseIsRejectedAndTheirCommitSurvives()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        using var harness = await Harness.LoadAsync(repository);
        var theirs = PushFromAnotherClone(repository, "topic");
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");

        harness.Run(ActionIds.RemotePushForce);
        await harness.PumpUntilAsync(() => harness.Prompts.Count > 0);
        harness.Answer();
        await harness.FinishAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Error, latest.Severity);
        Assert.StartsWith("Push failed", latest.Text, StringComparison.Ordinal);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenABranchWithoutUpstream_WhenForcePushing_ThenItIsRefusedWithoutAConfirm()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        using var harness = await Harness.LoadAsync(repository);

        Assert.True(harness.Push.TryExecute(ActionIds.RemotePushForce, null));

        Assert.Equal("lonely has no upstream: push it first", harness.Push.Notice);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenARemoteBranch_WhenDeletingIt_ThenBothPickersAndTheConfirmComeFirst()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var tip = repository.Git("rev-parse", "origin/topic").Trim();
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteDeleteBranch);
        await harness.PumpUntilAsync(() => PopupText.Of(harness.Push).Length > 0);
        Assert.Contains("Remote to delete a branch from", PopupText.Of(harness.Push), StringComparison.Ordinal);
        harness.Run(ActionIds.InputAccept);
        Assert.Contains("Branch to delete on origin", PopupText.Of(harness.Push), StringComparison.Ordinal);
        harness.Type("topic");
        harness.Run(ActionIds.InputAccept);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("topic", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("origin", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains($"git push origin {tip}:refs/heads/topic", prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);

        harness.Answer();
        await harness.PumpUntilAsync(() => harness.Refreshes > 0);

        Assert.DoesNotContain("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
        Assert.Equal(tip, repository.Git("rev-parse", "topic").Trim());
        Assert.Contains(tip, harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheDeleteConfirmIsRefused_WhenNotAnswered_ThenTheRemoteBranchStays()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        using var harness = await Harness.LoadAsync(repository);

        harness.Run(ActionIds.RemoteDeleteBranch);
        await harness.PumpUntilAsync(() => PopupText.Of(harness.Push).Length > 0);
        harness.Run(ActionIds.InputAccept);
        harness.Type("topic");
        harness.Run(ActionIds.InputAccept);

        Assert.Single(harness.Prompts);
        Assert.Contains("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
        Assert.Equal(0, harness.Refreshes);
    }

    [Theory]
    [InlineData("origin/main", "origin", "main")]
    [InlineData("origin/feature/x", "origin", "feature/x")]
    [InlineData("team/fork/x", "team/fork", "x")]
    public void GivenARemoteBranchName_WhenSplitting_ThenTheLongestRemoteWins(
        string name,
        string remote,
        string branch)
    {
        var split = RemoteBranchName.Split(name, ["origin", "team", "team/fork"]);

        Assert.Equal(new RemoteBranchName(remote, branch), split);
    }

    [Fact]
    public void GivenANameOfNoRemote_WhenSplitting_ThenNothingIsReturned()
    {
        Assert.Null(RemoteBranchName.Split("stranger/main", ["origin"]));
    }

    private static string PushFromAnotherClone(TestRepository repository, string branch)
    {
        var root = Path.GetDirectoryName(repository.OriginPath)!;
        var clone = Path.Combine(root, "other-clone");
        repository.GitIn(root, ["clone", "-q", "--branch", branch, repository.OriginPath, clone]);
        File.WriteAllText(Path.Combine(clone, "theirs.txt"), "t\n");
        repository.GitIn(clone, ["add", "-A"]);
        repository.GitIn(clone, ["commit", "-q", "-m", "Theirs"]);
        repository.GitIn(clone, ["push", "-q", "origin", branch]);
        return repository.GitIn(clone, ["rev-parse", "HEAD"]).Trim();
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];
        private readonly List<TabSession> _sessions = [];

        private Harness()
        {
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Remote = new RemoteActions(
                _host,
                TestGit.Services.Remote,
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                CountRefresh,
                () => { },
                new TabOperationGate(),
                Confirm,
                (_, _, _) => string.Empty);
            Push = new PushActions(
                _host,
                Remote,
                TestGit.Services.Remote,
                TestGit.Services.Tags.ListRemotesAsync,
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                Confirm,
                CountRefresh,
                mode => Mode = mode,
                () => { });
        }

        public RemoteActions Remote { get; }

        public PushActions Push { get; }

        public StatusChannel Statuses { get; } = new();

        public List<ConfirmPrompt> Prompts { get; } = [];

        public int Refreshes { get; private set; }

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        private UiPump Pump { get; }

        public static async Task<Harness> LoadAsync(TestRepository repository)
        {
            var harness = new Harness();
            await harness.OpenTabAsync(repository);
            return harness;
        }

        public void Run(ActionId action) => Assert.True(Push.TryExecute(action, null), action.Value);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Push.HandleTextKey(new KeyEvent(character.ToString())));
            }
        }

        /// <summary>Presses <c>y</c> in the open dialog.</summary>
        public void Answer()
        {
            var answers = _answers.ToList();
            _answers.Clear();
            foreach (var answer in answers)
            {
                answer();
            }
        }

        /// <summary>Waits until a remote operation reported and finished.</summary>
        public Task FinishAsync() => PumpUntilAsync(
            () => Statuses.Log.Latest is not null && !Remote.IsRunning(_host.Workspace.ActiveTabId!.Value));

        public Task PumpUntilAsync(Func<bool> condition) => Pump.RunUntilAsync(condition);

        public void Dispose()
        {
            foreach (var session in _sessions)
            {
                session.Dispose();
            }
        }

        private bool CountRefresh(TabId tabId)
        {
            Refreshes++;
            return true;
        }

        private async Task OpenTabAsync(TestRepository repository)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            _host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = _host.Workspace.ActiveTabId!.Value;
            var session = new TabSession(
                tabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
            _sessions.Add(session);
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                session,
                _host,
                new StatusChannel(),
                InlineUi.Post,
                session.Cancellation.Token);
        }

        private bool Confirm(ConfirmPrompt prompt, Action onYes)
        {
            Prompts.Add(prompt);
            _answers.Add(onYes);
            return true;
        }
    }
}
