using Gitunia.Core;
using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Repositories.Models;
using Gitunia.App.Compare;
using Gitunia.Core.Compare;
using Gitunia.Core.Effects;
using Gitunia.Tui.App.Compare;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Compare;

public sealed class CompareInputTests : IDisposable, ICompareSource, ITabSessions
{
    private readonly FakeCommitDetailsReader _reader = FakeCommitDetailsReader.Chain(3);
    private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly TerminalTabSession _session;
    private readonly List<string> _notices = [];
    private readonly CompareEffectHandler _handler;
    private ObjectId? _selected;

    public CompareInputTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-compare-tests");
        var gitDirectory = Path.Combine(root, ".git");
        _host.Dispatch(
            new OpenRepositoryCommand(new RepositoryLocation(root, gitDirectory, gitDirectory, false, false)));
        _session = new TerminalTabSession(
            new TabSessionParts(
                _host.Workspace.ActiveTabId!.Value,
                new CommitDetailsStore(
                _reader,
                FakeCommitDetailsReader.Location,
                _pump.Post,
                TimeSpan.FromMilliseconds(1))),
            GraphGlyphSet.Unicode);
        _handler = new CompareEffectHandler(this, _host);
        _host.EffectRaised += Run;
        _session.Graph.AddRange(
            [.. Newest.Select(id => new GraphEntry(
                id,
                "x",
                "A",
                DateTimeOffset.UnixEpoch,
                [],
                new GraphRow(id, 0, 1, [])))]);
    }

    private IReadOnlyList<ObjectId> Newest => _reader.Commits;

    private string Notice => _notices.LastOrDefault() ?? string.Empty;

    [Fact]
    public async Task GivenNoMark_WhenPressingTheKey_ThenTheCommitUnderTheCursorIsMarkedInTheTab()
    {
        var actions = await CreateAsync(Newest[1]);

        Assert.True(actions.TryExecute(ActionIds.GraphCompare));

        Assert.Equal(Newest[1], _host.Workspace.ActiveTab!.View.CompareMark);
        Assert.Contains("Marked", Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMarkNewerThanTheCursor_WhenComparing_ThenTheDiffRunsFromTheOlderToTheNewerCommit()
    {
        var actions = await CreateAsync(Newest[0]);
        actions.TryExecute(ActionIds.GraphCompare);
        _reader.SetFiles(Newest[0], Newest[2], "changed.txt");
        await SelectAsync(Newest[2]);

        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        var details = _session.Details.Current.Details!;
        Assert.Equal(Newest[2], details.CompareOlder);
        Assert.Equal(Newest[0], details.CompareNewer);
        Assert.Equal(Newest[2], _session.Details.Current.Commit);
        Assert.Equal(["changed.txt"], details.Files.Select(file => file.Path));
        Assert.Equal(BottomTab.Changes, _host.Workspace.ActiveTab!.View.BottomTab);
    }

    [Fact]
    public async Task GivenAMarkOlderThanTheCursor_WhenComparing_ThenTheOrderIsTheSame()
    {
        var actions = await CreateAsync(Newest[2]);
        actions.TryExecute(ActionIds.GraphCompare);
        _reader.SetFiles(Newest[0], Newest[2], "changed.txt");
        await SelectAsync(Newest[0]);

        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        Assert.Equal(Newest[2], _session.Details.Current.Details!.CompareOlder);
        Assert.Equal(Newest[0], _session.Details.Current.Details!.CompareNewer);
    }

    [Fact]
    public async Task GivenAComparison_WhenMovingToAThirdCommit_ThenItIsComparedWithTheSameMark()
    {
        var actions = await CreateAsync(Newest[0]);
        actions.TryExecute(ActionIds.GraphCompare);
        await SelectAsync(Newest[2]);
        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        await SelectAsync(Newest[1]);
        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        Assert.Equal(Newest[1], _session.Details.Current.Details!.CompareOlder);
        Assert.Equal(Newest[0], _session.Details.Current.Details!.CompareNewer);
        Assert.Equal(Newest[0], _host.Workspace.ActiveTab!.View.CompareMark);
    }

    [Fact]
    public async Task GivenCachedCommits_WhenTheCursorMovesQuickly_ThenOnlyTheLastOneStartsAComparison()
    {
        var actions = await CreateAsync(Newest[0]);
        _session.Details.Select(Newest[1], [Newest[2]]);
        await _pump.RunUntilAsync(() => _session.Details.CachedCount >= 3);
        _session.Details.Select(Newest[0]);
        actions.TryExecute(ActionIds.GraphCompare);
        var before = _reader.ReadFilesCount;

        _selected = Newest[1];
        _session.Details.Select(Newest[1]);
        _selected = Newest[2];
        _session.Details.Select(Newest[2]);
        await _pump.RunUntilAsync(() => _session.Details.Current.Details?.IsComparison == true);

        Assert.Equal(Newest[2], _session.Details.Current.Details!.CompareOlder);
        Assert.Equal(before + 1, _reader.ReadFilesCount);
    }

    [Fact]
    public async Task GivenAComparison_WhenMovingBackToTheMarkedCommit_ThenItsOwnChangesShow()
    {
        var actions = await CreateAsync(Newest[0]);
        actions.TryExecute(ActionIds.GraphCompare);
        await SelectAsync(Newest[2]);
        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        await SelectAsync(Newest[0]);

        Assert.False(_session.Details.Current.Details!.IsComparison);
        Assert.Equal(Newest[0], _host.Workspace.ActiveTab!.View.CompareMark);
    }

    [Fact]
    public async Task GivenAComparison_WhenPressingTheKeyOnAnotherCommit_ThenTheMarkMovesThereWithoutComparing()
    {
        var actions = await CreateAsync(Newest[0]);
        actions.TryExecute(ActionIds.GraphCompare);
        await SelectAsync(Newest[2]);
        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        actions.TryExecute(ActionIds.GraphCompare);
        await _pump.RunUntilAsync(() => _session.Details.Current.Status == CommitDetailsStatus.Loaded);

        Assert.Equal(Newest[2], _host.Workspace.ActiveTab!.View.CompareMark);
        Assert.False(_session.Details.Current.Details!.IsComparison);
        Assert.Contains("moved", Notice, StringComparison.Ordinal);

        await SelectAsync(Newest[1]);
        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        Assert.Equal(Newest[2], _session.Details.Current.Details!.CompareOlder);
        Assert.Equal(Newest[1], _session.Details.Current.Details!.CompareNewer);
    }

    [Fact]
    public async Task GivenAMark_WhenTheCursorMovesSeveralTimesQuickly_ThenOnlyTheLastCommitIsCompared()
    {
        var actions = await CreateAsync(Newest[0]);
        actions.TryExecute(ActionIds.GraphCompare);
        var before = _reader.ReadFilesCount;

        _selected = Newest[1];
        _session.Details.Select(Newest[1]);
        _selected = Newest[2];
        _session.Details.Select(Newest[2]);
        await _pump.RunUntilAsync(() => _session.Details.Current.Details?.IsComparison == true);

        Assert.Equal(Newest[2], _session.Details.Current.Commit);
        Assert.Equal(before + 1, _reader.ReadFilesCount);
    }

    [Fact]
    public async Task GivenAComparison_WhenPressingEscape_ThenTheMarkAndTheComparisonAreGone()
    {
        var actions = await CreateAsync(Newest[0]);
        actions.TryExecute(ActionIds.GraphCompare);
        await SelectAsync(Newest[2]);
        await _pump.RunUntilAsync(() => _session.Details.Current.Details!.IsComparison);

        Assert.True(actions.TryExecute(ActionIds.Cancel));
        await _pump.RunUntilAsync(() => _session.Details.Current.Status == CommitDetailsStatus.Loaded);

        Assert.Null(_host.Workspace.ActiveTab!.View.CompareMark);
        Assert.False(_session.Details.Current.Details!.IsComparison);
        Assert.Equal(Newest[2], _session.Details.Current.Commit);
    }

    [Fact]
    public async Task GivenNoMark_WhenPressingEscape_ThenItIsNotClaimed()
    {
        var actions = await CreateAsync(Newest[0]);

        Assert.False(actions.TryExecute(ActionIds.Cancel));
    }

    [Fact]
    public async Task GivenTheMarkedCommit_WhenPressingTheKeyOnIt_ThenTheMarkIsCleared()
    {
        var actions = await CreateAsync(Newest[1]);
        actions.TryExecute(ActionIds.GraphCompare);

        actions.TryExecute(ActionIds.GraphCompare);

        Assert.Null(_host.Workspace.ActiveTab!.View.CompareMark);
    }

    [Fact]
    public async Task GivenAMarkNotInTheGraph_WhenPressingTheKey_ThenTheMarkMovesToTheCursor()
    {
        var actions = await CreateAsync(Newest[1]);
        _host.Dispatch(
            new TabCommand(_session.TabId, new SetCompareMarkCommand(FakeCommitDetailsReader.MissingId())));

        actions.TryExecute(ActionIds.GraphCompare);

        Assert.Equal(Newest[1], _host.Workspace.ActiveTab!.View.CompareMark);
    }

    [Fact]
    public async Task GivenEscapeInAnotherPane_WhenMarked_ThenTheMarkStays()
    {
        var actions = await CreateAsync(Newest[0]);
        actions.TryExecute(ActionIds.GraphCompare);
        _host.Dispatch(new TabCommand(_session.TabId, new FocusPaneCommand(PaneKind.Detail)));

        Assert.False(actions.TryExecute(ActionIds.Cancel));

        Assert.Equal(Newest[0], _host.Workspace.ActiveTab!.View.CompareMark);
    }

    [Fact]
    public async Task GivenTwoTabs_WhenMarkingInOne_ThenTheOtherHasNoMark()
    {
        var actions = await CreateAsync(Newest[0]);
        var first = _session.TabId;
        actions.TryExecute(ActionIds.GraphCompare);
        var root = Path.Combine(Path.GetTempPath(), "gitunia-compare-tests-second");
        var gitDirectory = Path.Combine(root, ".git");
        _host.Dispatch(
            new OpenRepositoryCommand(new RepositoryLocation(root, gitDirectory, gitDirectory, false, false)));

        var second = _host.Workspace.Tabs.Single(tab => tab.Id != first);

        Assert.Equal(Newest[0], _host.Workspace.Tabs.Single(tab => tab.Id == first).View.CompareMark);
        Assert.Null(second.View.CompareMark);
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    public TerminalTabSession? ActiveSession() => _session;

    public ObjectId? SelectedCommitId() => _selected;

    public TabSession? Find(TabId tabId) => _session;

    private void Run(AppEffect effect)
    {
        if (effect is CompareEffect compare)
        {
            _handler.Handle(compare);
        }
        else if (effect is PostStatusEffect post)
        {
            _notices.Add(post.Text);
        }
    }

    private async Task<CompareInput> CreateAsync(ObjectId selected)
    {
        await SelectAsync(selected);

        var actions = new CompareInput(_host, this);
        _session.Details.Changed += actions.Follow;

        return actions;
    }

    private async Task SelectAsync(ObjectId commit)
    {
        _selected = commit;
        _session.Details.Select(commit);
        await _pump.RunUntilAsync(
            () => _session.Details.Current.Commit == commit
                && _session.Details.Current.Status == CommitDetailsStatus.Loaded);
    }
}
