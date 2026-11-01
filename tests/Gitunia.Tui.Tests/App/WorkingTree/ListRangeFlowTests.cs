using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Discard;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.WorkingTree;

/// <summary>The Visual range of files in the Changes view lists, from <c>v</c> to the stage, unstage and discard keys and the real confirm dialog.</summary>
public sealed class ListRangeFlowTests : IDisposable
{
    private readonly Flow _flow = new();

    public void Dispose() => _flow.Dispose();

    [Fact]
    public async Task GivenARangeOverFiles_WhenStaging_ThenAllOfThemGoInOneCallAndTheRangeEnds()
    {
        _flow.Load(Modified("a.txt"), Modified("b.txt"), Modified("c.txt"), Modified("d.txt"));
        _flow.Run(ActionIds.ListDown);

        _flow.Run(ActionIds.VisualToggle);
        _flow.Run(ActionIds.ListDown, 2);

        Assert.Equal(KeyMode.Visual, _flow.Keys.Mode);
        Assert.Equal(new RowRange(1, 3), _flow.Source.Range);

        _flow.Run(ActionIds.WorkingTreeStage);
        await _flow.WaitAsync();

        Assert.Equal(["b.txt", "c.txt", "d.txt"], Assert.Single(_flow.Index.Staged));
        Assert.False(_flow.Visual.IsActive);
        Assert.Equal(KeyMode.Normal, _flow.Keys.Mode);
        Assert.Equal("Staged 3 files", _flow.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenARangeBuiltUpwards_WhenUnstaging_ThenTheFilesBetweenTheEndsAreUnstaged()
    {
        _flow.Load(StagedModified("a.txt"), StagedModified("b.txt"), StagedModified("c.txt"));
        _flow.Run(ActionIds.ListBottom);

        _flow.Run(ActionIds.VisualToggle);
        _flow.Run(ActionIds.ListUp);
        _flow.Run(ActionIds.WorkingTreeUnstage);
        await _flow.WaitAsync();

        Assert.Equal(["b.txt", "c.txt"], Assert.Single(_flow.Index.Unstaged));
    }

    [Fact]
    public async Task GivenARangeWithAConflictedFile_WhenStaging_ThenItIsSkippedAndCounted()
    {
        _flow.Load(Modified("a.txt"), Conflict("c.txt"), Modified("d.txt"));

        _flow.Run(ActionIds.VisualToggle);
        _flow.Run(ActionIds.ListBottom);
        _flow.Run(ActionIds.WorkingTreeStage);
        await _flow.WaitAsync();

        Assert.Equal(["a.txt", "d.txt"], Assert.Single(_flow.Index.Staged));
        Assert.Equal("Staged 2 files (1 conflicted skipped)", _flow.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public void GivenARangeOfOnlyConflictedFiles_WhenStaging_ThenNothingRunsAndTheRangeStays()
    {
        _flow.Load(Conflict("c.txt"), Conflict("d.txt"));

        _flow.Run(ActionIds.VisualToggle);
        _flow.Run(ActionIds.ListDown);
        _flow.Run(ActionIds.WorkingTreeStage);

        Assert.Contains("conflicted", _flow.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.True(_flow.Visual.IsActive);
        Assert.Empty(_flow.Index.Staged);
    }

    [Fact]
    public async Task GivenAConflictedFile_WhenStagingIt_ThenAConfirmAsksAndYMarksItResolved()
    {
        _flow.Load(Conflict("c.txt"), Modified("a.txt"));

        _flow.Run(ActionIds.WorkingTreeStage);

        Assert.Equal(OverlayKind.Confirm, _flow.Overlays.State.Kind);
        var prompt = _flow.Overlays.State.Confirm!;
        Assert.Contains("c.txt", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("markers", prompt.Lines[1], StringComparison.Ordinal);
        Assert.Empty(_flow.Index.Staged);

        _flow.Press(new Key('y'));
        await _flow.WaitAsync();

        Assert.Equal(["c.txt"], Assert.Single(_flow.Index.Staged));
        Assert.Equal("Marked c.txt resolved", _flow.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenTheResolveDialog_WhenCancelled_ThenNothingIsStaged()
    {
        _flow.Load(Conflict("c.txt"));
        _flow.Run(ActionIds.WorkingTreeStage);

        _flow.Press(Key.Esc);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(OverlayKind.None, _flow.Overlays.State.Kind);
        Assert.Empty(_flow.Index.Staged);
        Assert.Null(_flow.Statuses.Log.Latest);
    }

    [Fact]
    public void GivenARange_WhenCopying_ThenThePathsAreCopiedOnePerLine()
    {
        _flow.Load(Modified("a.txt"), Modified("b.txt"));
        _flow.Run(ActionIds.VisualToggle);
        _flow.Run(ActionIds.ListDown);

        _flow.Run(ActionIds.VisualCopy);

        Assert.Contains(Convert.ToBase64String("a.txt\nb.txt"u8.ToArray()), _flow.ClipboardOutput.ToString(), StringComparison.Ordinal);
        Assert.False(_flow.Visual.IsActive);
    }

    [Fact]
    public async Task GivenARangeIncludingAnUntrackedFile_WhenDiscarding_ThenTheDialogCountsAndNamesThemAndYDiscardsAll()
    {
        _flow.Load(Modified("a.txt"), Untracked("new.txt"), Modified("b.txt"), Modified("outside.txt"));
        _flow.Run(ActionIds.VisualToggle);
        _flow.Run(ActionIds.ListDown, 2);

        _flow.Run(ActionIds.WorkingTreeDiscard);

        Assert.Equal(OverlayKind.Confirm, _flow.Overlays.State.Kind);
        Assert.Equal(KeyMode.Confirm, _flow.Keys.Mode);
        var prompt = _flow.Overlays.State.Confirm!;
        Assert.Equal("Discard changes to 3 files?", prompt.Lines[0]);
        Assert.Contains("new.txt", prompt.Lines[1], StringComparison.Ordinal);
        Assert.Contains("cannot be undone", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(0, _flow.Discarder.CallCount);

        _flow.Press(new Key('y'));
        await _flow.WaitAsync();

        Assert.Equal([["a.txt", "b.txt"]], _flow.Discarder.Restored);
        Assert.Equal([["new.txt"]], _flow.Discarder.Deleted);
        Assert.Equal(2, _flow.Discarder.CallCount);
        Assert.Equal("Discarded 3 files (1 untracked deleted)", _flow.Statuses.Log.Latest!.Text);
        Assert.False(_flow.Visual.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GivenTheRangeDialog_WhenCancelled_ThenNothingIsDiscardedAndTheRangeSurvives(bool useEscape)
    {
        _flow.Load(Modified("a.txt"), Modified("b.txt"));
        _flow.Run(ActionIds.VisualToggle);
        _flow.Run(ActionIds.ListDown);
        _flow.Run(ActionIds.WorkingTreeDiscard);
        Assert.Equal(OverlayKind.Confirm, _flow.Overlays.State.Kind);

        _flow.Press(useEscape ? Key.Esc : new Key('n'));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(OverlayKind.None, _flow.Overlays.State.Kind);
        Assert.Equal(0, _flow.Discarder.CallCount);
        Assert.Null(_flow.Statuses.Log.Latest);
        Assert.True(_flow.Visual.IsActive);
        Assert.Equal(KeyMode.Visual, _flow.Keys.Mode);
    }

    [Fact]
    public void GivenARangeOfStagedFiles_WhenDiscarding_ThenItSaysToUnstageFirst()
    {
        _flow.Load(StagedModified("a.txt"));
        _flow.Run(ActionIds.VisualToggle);

        _flow.Run(ActionIds.WorkingTreeDiscard);

        Assert.Contains("unstage the files first", _flow.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(OverlayKind.None, _flow.Overlays.State.Kind);
    }

    [Fact]
    public void GivenARange_WhenTheListChanges_ThenTheRangeEnds()
    {
        _flow.Load(Modified("a.txt"), Modified("b.txt"));
        _flow.Run(ActionIds.VisualToggle);
        _flow.Run(ActionIds.ListDown);

        _flow.Load(Modified("a.txt"), Modified("b.txt"), Modified("c.txt"));

        Assert.False(_flow.Visual.IsActive);
        Assert.Null(_flow.Source.Range);
        Assert.Equal(KeyMode.Normal, _flow.Keys.Mode);
    }

    private sealed class Flow : IDisposable
    {
        private readonly TestShell _shell = new();
        private readonly StagingWiring _wiring;
        private readonly ListView _list;
        private readonly ListView _stagedList;
        private readonly WhichKeyTimer _timer = new(TimeSpan.FromHours(1), action => action());

        public Flow()
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-list-range-flow-tests");
            var location = new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);
            _shell.Host.Dispatch(new OpenRepositoryCommand(location));
            Source = new WorkingTreeListSource(WorkingTreeSection.Unstaged, GraphGlyphSet.Unicode);
            _list = new ListView { Source = Source };
            StagedSource = new WorkingTreeListSource(WorkingTreeSection.Staged, GraphGlyphSet.Unicode);
            _stagedList = new ListView { Source = StagedSource };
            Pump = new UiPump(TestContext.Current.CancellationToken);

            var engine = new KeymapEngine(DefaultKeymap.Create());
            Keys = new KeyController(engine, Route, _shell.Host);
            Overlays = new OverlayController(
                engine, Keys, _timer, Route, () => new ScreenSize(100, 30), GraphGlyphSet.Unicode, () => DefaultKeymap.WorkingUnstagedView, Statuses, _shell.Host);

            var staging = new StagingActions(_shell.Host);
            _wiring = new StagingWiring(
                _shell.Host,
                Index,
                new PatchStager(new FakeDiffReader(), Index),
                Pump,
                new PatchDiscarder(new FakeDiffReader(), Discarder));
            var clipboard = new ClipboardWriter(ClipboardOutput, new Osc52Options(Osc52Mode.Direct, 100_000));
            Visual = new VisualRangeInput(_shell.Host, new FakePaneLists(_list, _stagedList));
            _copier = new RangeCopier(
                new ClipboardCopier(clipboard, Statuses),
                () => null,
                () => _shell.Host.Workspace.ActiveTab?.WorkingTree);
            _shell.Host.EffectRaised += CopyRange;
            Handler = new WorkingTreeActionHandler(
                _shell.Host, new FakePaneLists(_list, _stagedList), new ClipboardCopier(clipboard, Statuses), staging, TestCommitActions.Create(_shell.Host, Statuses))
            {
                Visual = Visual,
            };
        }

        private readonly RangeCopier _copier;

        public WorkingTreeListSource Source { get; }

        public WorkingTreeListSource StagedSource { get; }

        public UiPump Pump { get; }

        public KeyController Keys { get; }

        public OverlayController Overlays { get; }

        public VisualRangeInput Visual { get; }

        public WorkingTreeActionHandler Handler { get; }

        public StatusReporter Statuses => _shell.Statuses;

        public FakeIndexWriter Index { get; } = new();

        public FakeWorkingTreeDiscarder Discarder { get; } = new();

        public StringWriter ClipboardOutput { get; } = new();

        private RepositoryState Tab => _shell.Host.Workspace.ActiveTab!;

        public void Load(params StatusEntry[] entries)
        {
            var isFirst = Tab.WorkingTree is null;
            Send(new WorkingTreeLoadedCommand(Snapshot(entries)));
            if (isFirst)
            {
                Send(new OpenWorkingTreeCommand());
            }

            Source.Update(Tab.WorkingTree);
            StagedSource.Update(Tab.WorkingTree);
            Sync();
        }

        /// <summary>Runs an action as the router does: the open dialog first, then the working-tree handler.</summary>
        public void Run(ActionId action, int? count = null) => Route(action, count);

        public void Press(Key key) => Keys.Handle(key, Overlays.ViewName);

        public Task WaitAsync() =>
            Pump.RunUntilAsync(() => !Tab.Running.IsBusy && Statuses.Log.Latest is not null);

        public void Dispose()
        {
            _wiring.Dispose();
            _timer.Dispose();
            _list.Dispose();
            _stagedList.Dispose();
            Source.Dispose();
            StagedSource.Dispose();
        }

        // The terminal layer copies the rows of a range and answers (the application's shell does the same).
        private void CopyRange(AppEffect effect)
        {
            if (effect is CopyVisualRangeEffect copy)
            {
                var result = _copier.Copy(copy.Pane, copy.Range);
                _shell.Host.Dispatch(new VisualCopied(result.Copied, result.Notice));
            }
        }

        private void Route(ActionId action, int? count)
        {
            if (Overlays.Handle(action, count))
            {
                return;
            }

            Handler.TryExecute(action, count);

            // The presenter's work: the state's cursor moves the list's, and the range follows it.
            Sync();
        }

        private void Sync()
        {
            var selection = Tab.WorkingTree?.Selection;
            _list.SelectedItem = selection is { Section: WorkingTreeSection.Unstaged } ? selection.Index : null;
            _stagedList.SelectedItem = selection is { Section: WorkingTreeSection.Staged } ? selection.Index : null;
            Visual.OnCursorMoved();
        }

        private void Send(RepositoryCommand command) =>
            _shell.Host.Dispatch(new TabCommand(_shell.Host.Workspace.ActiveTabId!.Value, command));
    }
}
