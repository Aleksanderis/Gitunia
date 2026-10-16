using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Tags;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tags;

public class TagActionsTests
{
    [Fact]
    public async Task GivenAGraphCommit_WhenCreatingWithoutAMessage_ThenALightweightTagIsMadeThere()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.TagCreate);
        Assert.Contains("new tag at", harness.Tags.StatusLine, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("v1");
        harness.Run(ActionIds.InputAccept);
        Assert.Contains("message for v1", harness.Tags.StatusLine, StringComparison.Ordinal);
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("commit", repository.Git("cat-file", "-t", "refs/tags/v1").Trim());
        Assert.Equal(repository.Head, repository.Git("rev-parse", "v1").Trim());
        Assert.Equal(1, harness.Refreshes);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Contains("lightweight tag v1", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMessage_WhenCreating_ThenAnAnnotatedTagCarriesIt()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "main", repository.Head));

        harness.Run(ActionIds.TagCreate);
        harness.Type("v2");
        harness.Run(ActionIds.InputAccept);
        harness.Type("Second release");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("tag", repository.Git("cat-file", "-t", "refs/tags/v2").Trim());
        Assert.Contains("Second release", repository.Git("cat-file", "-p", "refs/tags/v2"), StringComparison.Ordinal);
        Assert.Contains("annotated tag v2", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenATagNameInUse_WhenAccepting_ThenGitIsNotRunAndTheNoticeSaysSo()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.TagCreate);
        harness.Type("v1");
        harness.Run(ActionIds.InputAccept);

        Assert.Equal("Tag v1 already exists", harness.Tags.Notice);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenAnInvalidName_WhenCreating_ThenNothingIsCreatedAndTheErrorIsShown()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.TagCreate);
        harness.Type("a..b");
        harness.Run(ActionIds.InputAccept);
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("not a valid tag name", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenEscInTheMessagePrompt_WhenCancelling_ThenNoTagIsCreated()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.TagCreate);
        harness.Type("v1");
        harness.Run(ActionIds.InputAccept);
        harness.Run(ActionIds.Cancel);

        Assert.Equal(string.Empty, harness.Tags.StatusLine);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenNothingSelected_WhenCreating_ThenNoPromptOpens()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, null));

        harness.Run(ActionIds.TagCreate);

        Assert.Equal("Select a commit or a ref first", harness.Tags.Notice);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenATagRow_WhenDeleting_ThenTheConfirmNamesTheTagAndYDeletesIt()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.Tag, "v1", repository.Head));

        harness.Run(ActionIds.TagDelete);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("v1", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains($"git tag v1 {repository.Head}", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal("v1", repository.Git("tag", "--list").Trim());

        harness.Answer();
        await harness.FinishAsync();

        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
        Assert.Contains("Deleted tag v1", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnAnnotatedTag_WhenDeleting_ThenTheUndoHintUsesTheTagObject()
    {
        using var repository = TestRepositories.Linear();
        repository.Git("tag", "-a", "-m", "msg", "v1");
        var tagObject = repository.Git("rev-parse", "refs/tags/v1").Trim();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.Tag, "v1", repository.Head));

        harness.Run(ActionIds.TagDelete);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains($"git tag v1 {tagObject}", prompt.Consequence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheDeclinedConfirm_WhenDeleting_ThenTheTagStays()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.TagDelete);

        Assert.Single(harness.Prompts);
        Assert.Equal("v1", repository.Git("tag", "--list").Trim());
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenTwoTagsAtTheCommit_WhenDeletingFromTheGraph_ThenNothingIsAsked()
    {
        using var repository = TestRepositories.Linear().Tag("v1").Tag("v1b");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.TagDelete);

        Assert.Equal("2 tags point here: pick one in the sidebar", harness.Tags.Notice);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenACommitWithoutTag_WhenDeleting_ThenTheNoticeSaysToSelectATag()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.TagDelete);

        Assert.Contains("Select a tag", harness.Tags.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenABranchRow_WhenDeleting_ThenItIsRefusedAsNotATag()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "main", repository.Head));

        harness.Run(ActionIds.TagDelete);

        Assert.Equal("Select a tag first", harness.Tags.Notice);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenATag_WhenPushing_ThenOnlyThatTagReachesTheRemote()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1").Tag("v2");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.Tag, "v1", repository.Head));

        harness.Run(ActionIds.TagPush);
        await harness.PumpUntilAsync(() => harness.Tags.StatusLine.Length > 0);
        Assert.Contains("origin (1/1)", harness.Tags.StatusLine, StringComparison.Ordinal);
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("v1", repository.GitIn(repository.OriginPath, ["tag", "--list"]).Trim());
        Assert.Contains("Pushed tag v1 to origin", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNoRemote_WhenPushing_ThenNoPromptOpensAndTheStatusSaysSo()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.Tag, "v1", repository.Head));

        harness.Run(ActionIds.TagPush);
        await harness.PumpUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Contains("no remote", harness.Statuses.Log.Latest!.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, harness.Tags.StatusLine);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenATypedFilter_WhenPushing_ThenOnlyMatchingRemotesAreOffered()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        repository.Git("remote", "add", "backup", repository.OriginPath);
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.Tag, "v1", repository.Head));

        harness.Run(ActionIds.TagPush);
        await harness.PumpUntilAsync(() => harness.Tags.StatusLine.Length > 0);
        Assert.Contains("(1/2)", harness.Tags.StatusLine, StringComparison.Ordinal);
        harness.Type("back");

        Assert.Contains("backup (1/1)", harness.Tags.StatusLine, StringComparison.Ordinal);
        harness.Type("zzz");
        Assert.Contains("no matching remote", harness.Tags.StatusLine, StringComparison.Ordinal);
        harness.Run(ActionIds.InputAccept);
        Assert.Equal("No matching remote", harness.Tags.Notice);
    }

    [Fact]
    public async Task GivenARejectedPush_WhenPushing_ThenGitsErrorIsShownAndNothingIsHidden()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        repository.Git("push", "-q", "origin", "refs/tags/v1");
        repository.Git("tag", "-f", "v1", "HEAD~1");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.Tag, "v1", repository.Git("rev-parse", "HEAD~1").Trim()));

        harness.Run(ActionIds.TagPush);
        await harness.PumpUntilAsync(() => harness.Tags.StatusLine.Length > 0);
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.StartsWith("Could not push the tag:", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAPushedTag_WhenDeletingOnTheRemote_ThenTheConfirmNamesRemoteAndTagAndTheLocalTagStays()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        repository.Git("push", "-q", "origin", "refs/tags/v1");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.Tag, "v1", repository.Head));

        harness.Run(ActionIds.TagDeleteRemote);
        await harness.PumpUntilAsync(() => harness.Tags.StatusLine.Length > 0);
        Assert.Contains("delete from tag v1", harness.Tags.StatusLine, StringComparison.Ordinal);
        harness.Run(ActionIds.InputAccept);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("v1", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("origin", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("git push origin refs/tags/v1", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal("v1", repository.GitIn(repository.OriginPath, ["tag", "--list"]).Trim());

        harness.Answer();
        await harness.FinishAsync();

        Assert.Equal(string.Empty, repository.GitIn(repository.OriginPath, ["tag", "--list"]).Trim());
        Assert.Equal("v1", repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenAnotherAction_WhenNoPromptIsOpen_ThenTheTagActionsDoNotHandleIt()
    {
        using var repository = TestRepositories.Linear();
        using var harness = await Harness.LoadAsync(repository);

        Assert.False(harness.Tags.TryExecute(ActionIds.InputAccept, null));
        Assert.False(harness.Tags.TryExecute(ActionIds.BranchCreate, null));
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];
        private readonly List<TabSession> _sessions = [];
        private BranchSelection _selection = new(null, null, null);

        private Harness()
        {
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Tags = new TagActions(
                _host,
                TestGit.Services.Tags,
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                Confirm,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                mode => Mode = mode,
                () => { },
                () => _selection);
        }

        public TagActions Tags { get; }

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

        public void Select(BranchSelection selection) => _selection = selection;

        public void Run(ActionId action) => Assert.True(Tags.TryExecute(action, null), action.Value);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Tags.HandleTextKey(new KeyEvent(character.ToString())));
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
