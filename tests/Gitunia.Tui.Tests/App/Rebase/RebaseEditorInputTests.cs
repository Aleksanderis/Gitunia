using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Rebase;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Rebase;

// The view side of the editor over the real dispatcher: it turns keys and clicks into commands and draws the session.
public class RebaseEditorInputTests
{
    private const string Tip = "1111111111111111111111111111111111111111";
    private const string Target = "2222222222222222222222222222222222222222";
    private const string First = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Second = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Third = "cccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public void GivenTheOpenAction_WhenExecuting_ThenItIsConsumedAndTheRangeIsReadForTheSelection()
    {
        var harness = new Harness();

        var consumed = harness.Input.TryExecute(ActionIds.RebaseInteractive, null);

        Assert.True(consumed);
        var read = Assert.Single(harness.Effects.OfType<ReadRebaseRangeEffect>());
        Assert.Equal(Target, read.TargetHash);
        Assert.Equal("develop", read.Label);
    }

    [Fact]
    public void GivenTheRootAction_WhenExecuting_ThenTheWholeHistoryIsRead()
    {
        var harness = new Harness();

        Assert.True(harness.Input.TryExecute(ActionIds.RebaseInteractiveRoot, null));

        Assert.Null(Assert.Single(harness.Effects.OfType<ReadRebaseRangeEffect>()).TargetHash);
    }

    [Fact]
    public void GivenAnotherAction_WhenTheEditorIsClosed_ThenItIsNotConsumed()
    {
        var harness = new Harness();

        Assert.False(harness.Input.TryExecute(ActionIds.HelpShow, null));
        Assert.Empty(harness.Effects);
    }

    [Fact]
    public void GivenAnotherOverlayOpen_WhenAskingForTheEditor_ThenItDoesNotOpenAndNothingIsRead()
    {
        var harness = new Harness();
        harness.Cover.IsOpen = true;

        Assert.True(harness.Input.TryExecute(ActionIds.RebaseInteractive, null));

        Assert.Empty(harness.Effects);
        Assert.False(harness.Input.IsOpen);
    }

    [Fact]
    public void GivenTheEditorOpen_WhenAnotherActionArrives_ThenItIsConsumedAndNothingBehindMoves()
    {
        var harness = new Harness();
        harness.OpenEditor();

        Assert.True(harness.Input.TryExecute(ActionIds.HelpShow, null));
        Assert.True(harness.Input.TryExecute(ActionIds.RebaseEditorDrop, null));

        Assert.True(harness.Input.IsOpen);
        Assert.Equal(RebaseTodoAction.Drop, harness.Input.State!.Rows[0].Action);
    }

    [Fact]
    public void GivenACountAndTheEditorOpen_WhenMovingDown_ThenTheCommitMovesThatManyRows()
    {
        var harness = new Harness();
        harness.OpenEditor();

        harness.Input.TryExecute(ActionIds.RebaseEditorMoveDown, 2);

        Assert.Equal([Second, Third, First], harness.Input.State!.Rows.Select(row => row.Hash));
    }

    [Fact]
    public void GivenADialogOverTheEditor_WhenActionsAndFramesAreRequested_ThenTheEditorStepsAside()
    {
        var harness = new Harness();
        harness.OpenEditor();
        harness.Cover.IsOpen = true;

        Assert.False(harness.Input.TryExecute(ActionIds.ConfirmYes, null));
        Assert.Null(harness.Input.CurrentFrame());
        Assert.Null(harness.Input.OnLineClicked(2, 5, out var handled));
        Assert.False(handled);

        harness.Cover.IsOpen = false;
        Assert.NotNull(harness.Input.CurrentFrame());
        Assert.True(harness.Input.IsOpen);
    }

    [Fact]
    public void GivenTheTabIsClosed_WhenAnActionArrives_ThenTheEditorIsGoneAndDoesNotConsumeIt()
    {
        var harness = new Harness();
        harness.OpenEditor();

        harness.Host.Dispatch(new CloseTabCommand(harness.TabId));

        Assert.False(harness.Input.IsOpen);
        Assert.False(harness.Input.TryExecute(ActionIds.RebaseEditorDrop, null));
    }

    [Fact]
    public void GivenTextKeysWithoutAMessageEditor_WhenHandlingThem_ThenTheyAreNotTaken()
    {
        var harness = new Harness();
        Assert.False(harness.Input.HandleTextKey(new KeyEvent("a")));
        harness.OpenEditor();

        Assert.False(harness.Input.HandleTextKey(new KeyEvent("a")));
    }

    [Fact]
    public void GivenTheMessageEditor_WhenTextKeysArrive_ThenTheyAreTakenAndTyped()
    {
        var harness = new Harness();
        harness.OpenEditor();
        harness.Host.Dispatch(new RebaseEditorMessageRead(harness.TabId, harness.Generation, First, "one"));
        Assert.True(harness.Input.IsEditingMessage);

        Assert.True(harness.Input.HandleTextKey(new KeyEvent("!")));

        Assert.Equal("one!", harness.Input.State!.Edit!.Editor.Text);
    }

    [Fact]
    public void GivenClicks_WhenClickingACommitRowAndAChip_ThenTheRowIsHighlightedAndTheChipNamesItsView()
    {
        var harness = new Harness();
        harness.OpenEditor();

        Assert.Null(harness.Input.OnLineClicked(4, 10, out var rowHandled));
        Assert.True(rowHandled);
        Assert.Equal(2, harness.Input.State!.SelectedIndex);

        var frame = harness.Input.CurrentFrame()!;
        var chipLine = frame.Lines.ToList().FindIndex(line => line.Text.Contains("p pick", StringComparison.Ordinal));
        var column = frame.Lines[chipLine].Text.IndexOf("d drop", StringComparison.Ordinal);
        var view = harness.Input.OnLineClicked(chipLine, column, out var chipHandled);
        Assert.Equal(DefaultMouseMap.RebaseDropButton, view);
        Assert.True(chipHandled);

        Assert.Null(harness.Input.OnLineClicked(0, 3, out var titleHandled));
        Assert.False(titleHandled);
    }

    [Fact]
    public void GivenTheMessageEditorOpen_WhenClicking_ThenTheClickIsNotHandled()
    {
        var harness = new Harness();
        harness.OpenEditor();
        harness.Host.Dispatch(new RebaseEditorMessageRead(harness.TabId, harness.Generation, First, "one"));

        Assert.Null(harness.Input.OnLineClicked(3, 10, out var handled));
        Assert.False(handled);
    }

    private sealed class Harness
    {
        private readonly Selection _selection = new();

        public Harness()
        {
            Host = new Dispatcher(AppState.Empty);
            Host.EffectRaised += Effects.Add;
            Host.Dispatch(new ScreenResizedCommand(new ScreenExtent(100, 30), Host.Clock.GetUtcNow()));
            Host.Dispatch(new OpenRepositoryCommand(Location()));
            TabId = Host.Workspace.ActiveTabId!.Value;
            Host.Dispatch(new TabCommand(TabId, new HeadLoadedCommand(new HeadState(HeadKind.Branch, "main", Tip))));
            Input = new RebaseEditorInput(Host, _selection, Cover, GraphGlyphSet.Unicode);
            Effects.Clear();
        }

        public Dispatcher Host { get; }

        public TabId TabId { get; }

        public RebaseEditorInput Input { get; }

        public Cover Cover { get; } = new();

        public List<AppEffect> Effects { get; } = [];

        public long Generation => Host.State.Shell.RebaseEditor.Generation;

        // The editor opens the way the app opens it: the open request, then the range the handler would read.
        public void OpenEditor()
        {
            Input.TryExecute(ActionIds.RebaseInteractive, null);
            var todo = RebaseEditorState.Create(
                TabId,
                "main",
                "develop",
                RebaseBase.OntoCommit(Target),
                Tip,
                [new(First, "one"), new(Second, "two"), new(Third, "three")]);
            Host.Dispatch(new RebaseEditorRangeRead(TabId, Generation, todo, Target));
            Effects.Clear();
            Assert.True(Input.IsOpen);
        }

        private static RepositoryLocation Location()
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-rebase-editor-input-tests");
            var gitDirectory = Path.Combine(root, ".git");

            return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        }
    }

    private sealed class Selection : IBranchSelectionSource
    {
        public BranchSelection Current() => new(SidebarRowKind.LocalBranch, "develop", Target);
    }

    private sealed class Cover : IOverlayCover
    {
        public bool IsOpen { get; set; }

        public bool IsOtherOverlayOpen() => IsOpen;
    }
}
