using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Discard;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Tests.Fakes;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.WorkingTree;

public class DiscardActionsTests
{
    [Fact]
    public void GivenAModifiedFile_WhenDiscarding_ThenTheDialogNamesWhatIsLostAndNothingRunsYet()
    {
        using var harness = new Harness(Modified("a.txt"));

        var notice = harness.Execute();

        Assert.Equal(string.Empty, notice);
        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Discard changes", prompt.Title);
        Assert.Equal(["Discard all unstaged changes to a.txt?"], prompt.Lines);
        Assert.Contains("cannot be undone", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(0, harness.Discarder.CallCount);
    }

    [Fact]
    public async Task GivenTheDialogIsAnswered_WhenYes_ThenTheFileIsRestoredAndTheStatusIsReRead()
    {
        using var harness = new Harness(Modified("a.txt"));
        var refreshed = false;
        harness.OnRefresh = () => refreshed = true;
        harness.Execute();

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["a.txt"], Assert.Single(harness.Discarder.Restored));
        Assert.Empty(harness.Discarder.Deleted);
        Assert.True(refreshed);
        Assert.Equal("Discarded all changes to a.txt", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenTheDialogIsDropped_WhenNobodyAnswersYes_ThenNothingIsDiscarded()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Execute();

        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.Discarder.CallCount);
        Assert.Null(harness.Statuses.Log.Latest);
    }

    [Fact]
    public void GivenAnUntrackedFile_WhenDiscarding_ThenTheDialogSaysItIsDeletedFromDisk()
    {
        using var harness = new Harness(Untracked("new.txt"));

        harness.Execute();

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Delete untracked file", prompt.Title);
        Assert.Equal(["Delete the untracked file new.txt?"], prompt.Lines);
        Assert.Contains("removed from disk", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal("Delete file", prompt.YesLabel);
    }

    [Fact]
    public async Task GivenAnUntrackedFile_WhenConfirmed_ThenItIsDeletedAndNotRestored()
    {
        using var harness = new Harness(Untracked("new.txt"));
        harness.Execute();

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["new.txt"], Assert.Single(harness.Discarder.Deleted));
        Assert.Empty(harness.Discarder.Restored);
        Assert.Equal("Deleted untracked new.txt", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public void GivenAnUntrackedDirectory_WhenDiscarding_ThenTheDialogNamesEverythingInIt()
    {
        using var harness = new Harness(Untracked("build/"));

        harness.Execute();

        Assert.Equal(["Delete the untracked directory build/ and everything in it?"], Assert.Single(harness.Prompts).Lines);
    }

    [Fact]
    public void GivenADeletedFileWithStagedChanges_WhenDiscarding_ThenTheDialogSaysWhatComesBackAndWhatStays()
    {
        using var harness = new Harness(Deleted("gone.txt") with { IndexChange = FileChangeKind.Modified });

        harness.Execute();

        var lines = Assert.Single(harness.Prompts).Lines;
        Assert.Contains("The deleted file is restored from the index.", lines);
        Assert.Contains("Its staged changes are kept.", lines);
    }

    [Fact]
    public void GivenAConflictedFile_WhenDiscarding_ThenItIsRefusedWithoutADialog()
    {
        using var harness = new Harness(Conflict("c.txt"));

        var notice = harness.Execute();

        Assert.Contains("conflicted", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public void GivenAnUntrackedNameThatIsNotUtf8OnAnOsThatCannotDeleteIt_WhenDiscarding_ThenItIsRefusedWithoutADialog()
    {
        using var harness = new Harness(Untracked($"bad{(char)0xDCE9}.txt"));
        harness.Discarder.CanDeleteInvalidNames = false;

        var notice = harness.Execute();

        Assert.Contains("not valid UTF-8", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public void GivenAnUntrackedNameThatIsNotUtf8OnLinux_WhenDiscarding_ThenTheDialogOpens()
    {
        using var harness = new Harness(Untracked($"bad{(char)0xDCE9}.txt"));

        var notice = harness.Execute();

        Assert.Equal(string.Empty, notice);
        Assert.Single(harness.Prompts);
    }

    [Fact]
    public void GivenTheStagedList_WhenDiscarding_ThenItSaysToUnstageFirst()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Focus(PaneKind.WorkingStaged);

        var notice = harness.Execute();

        Assert.Contains("unstage", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public void GivenADialogThatCannotBeShown_WhenDiscarding_ThenTheUserIsToldAndNothingRuns()
    {
        using var harness = new Harness(Modified("a.txt")) { CanShowDialog = false };

        var notice = harness.Execute();

        Assert.Contains("too small", notice, StringComparison.Ordinal);
        Assert.Equal(0, harness.Discarder.CallCount);
    }

    [Fact]
    public async Task GivenGitFails_WhenConfirmed_ThenTheErrorIsShownAndTheStatusIsReRead()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Discarder.Failure = new InvalidOperationException("index.lock exists");
        var refreshed = false;
        harness.OnRefresh = () => refreshed = true;
        harness.Execute();

        harness.Answer();
        await harness.WaitAsync();

        Assert.True(refreshed);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("Could not discard: index.lock exists", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenADiscardInProgress_WhenAnotherIsAsked_ThenItIsRefusedAsBusy()
    {
        using var harness = new Harness(Modified("a.txt"), Modified("b.txt"));
        harness.Discarder.IsHeld = true;
        harness.Execute();
        harness.Answer();

        var notice = harness.Execute();
        harness.Discarder.Release();
        await harness.WaitAsync();

        Assert.Contains("Busy", notice, StringComparison.Ordinal);
        Assert.Single(harness.Prompts);
    }

    [Fact]
    public void GivenAHunk_WhenDiscardingIt_ThenTheDialogCountsItsLines()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));

        var notice = harness.ExecuteSelection([Changed(2, "TWO"), Changed(4, "four")], false);

        Assert.Equal(string.Empty, notice);
        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Discard hunk", prompt.Title);
        Assert.Equal(["Discard this hunk (2 lines) of a.txt?"], prompt.Lines);
        Assert.Equal(0, harness.Discarder.CallCount);
    }

    [Fact]
    public async Task GivenAHunkConfirmed_WhenDiscarding_ThenAReversePatchOfJustThoseLinesGoesToTheWorkingTree()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));
        var discarded = false;
        harness.ExecuteSelection([Changed(2, "TWO")], true, () => discarded = true);

        harness.Answer();
        await harness.WaitAsync();

        var patch = Assert.Single(harness.Discarder.ReversedPatches);
        Assert.Equal(
            "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,3 +1,4 @@\n one\n+TWO\n three\n four\n",
            patch);
        Assert.True(discarded);
        Assert.Equal("Discarded 1 line of a.txt", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenALineThatIsNoLongerInTheFile_WhenConfirmed_ThenNothingIsDiscardedAndAWarningShows()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));
        harness.ExecuteSelection([Changed(2, "something else")], true);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(0, harness.Discarder.CallCount);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("changed since the diff was read", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileWithInvalidText_WhenConfirmingAHunk_ThenTheWholeFileIsNeverDiscardedBehindTheDialog()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt") with { HasInvalidText = true });
        harness.ExecuteSelection([Changed(2, "TWO")], false);

        Assert.Equal("Discard hunk", Assert.Single(harness.Prompts).Title);
        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(0, harness.Discarder.CallCount);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("nothing was discarded", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenLinesPickedInAnUntrackedFile_WhenDiscarding_ThenItIsRefusedWithoutADialog()
    {
        using var harness = new Harness(Untracked("new.txt"));

        var notice = harness.ExecuteSelection([Changed(1, "x")], true, entry: Untracked("new.txt"));

        Assert.Contains("cannot be split into lines", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenAHunkOfAnUntrackedFile_WhenConfirmed_ThenTheWholeFileIsDeleted()
    {
        using var harness = new Harness(Untracked("new.txt"));
        harness.ExecuteSelection([Changed(1, "x")], false, entry: Untracked("new.txt"));

        Assert.Equal("Delete untracked file", Assert.Single(harness.Prompts).Title);
        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["new.txt"], Assert.Single(harness.Discarder.Deleted));
    }

    [Fact]
    public void GivenTheStagedDiff_WhenDiscarding_ThenItIsRefused()
    {
        using var harness = new Harness(StagedModified("a.txt"));

        var notice = harness.ExecuteSelection([Changed(2, "TWO")], false, side: WorkingDiffSide.Staged);

        Assert.Contains("Unstaged diff", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public void GivenAVisualRange_WhenDiscarding_ThenTheDialogCountsTheSelectedLines()
    {
        using var harness = new Harness(Modified("a.txt"));

        harness.ExecuteSelection([Changed(2, "TWO"), Changed(4, "four"), Changed(5, "five")], true);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Discard lines", prompt.Title);
        Assert.Equal(["Discard the 3 selected lines of a.txt?"], prompt.Lines);
    }

    private static ChangedLine Changed(int line, string text) => new(DiffLineKind.Added, line, text);

    // One hunk: "two" becomes "TWO" and a line "four" is added at the end.
    private static DiffFile EditedFile(string path)
    {
        DiffLine[] lines =
        [
            new(DiffLineKind.Context, "one", 1, 1, false),
            new(DiffLineKind.Removed, "two", 2, null, false),
            new(DiffLineKind.Added, "TWO", null, 2, false),
            new(DiffLineKind.Context, "three", 3, 3, false),
            new(DiffLineKind.Added, "four", null, 4, false),
        ];
        var hunk = new DiffHunk("@@ -1,3 +1,5 @@", 1, 3, 1, 5, string.Empty, lines);
        string[] header = [$"diff --git a/{path} b/{path}", $"--- a/{path}", $"+++ b/{path}"];
        return new DiffFile(path, path, DiffFileStatus.Modified, null, null, null, false, header, [hunk]);
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];

        public Harness(params StatusEntry[] entries)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-discard-actions-tests");
            var location = new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);
            _host.Dispatch(new OpenRepositoryCommand(location));
            Send(new WorkingTreeLoadedCommand(Snapshot(entries)));
            Send(new OpenWorkingTreeCommand());
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Discard = new DiscardActions(
                _host,
                new PatchDiscarder(Reader, Discarder),
                Statuses,
                Pump.Post,
                RefreshStatus,
                _ => TestContext.Current.CancellationToken,
                Confirm,
                Gate);
        }

        public UiPump Pump { get; }

        public TabOperationGate Gate { get; } = new();

        public FakeWorkingTreeDiscarder Discarder { get; } = new();

        public FakeDiffReader Reader { get; } = new();

        public StatusChannel Statuses { get; } = new();

        public DiscardActions Discard { get; }

        public List<ConfirmPrompt> Prompts { get; } = [];

        public bool CanShowDialog { get; init; } = true;

        /// <summary>Stands in for the tab refresher: runs when a status read is requested, before the completion callback.</summary>
        public Action OnRefresh { get; set; } = () => { };

        private RepositoryState Tab => _host.Workspace.ActiveTab!;

        public string Execute() => Discard.Execute(Tab);

        public string ExecuteSelection(
            IReadOnlyList<ChangedLine> lines,
            bool isRange,
            Action? onDiscarded = null,
            StatusEntry? entry = null,
            WorkingDiffSide side = WorkingDiffSide.Unstaged)
        {
            var shown = entry ?? Tab.WorkingTree!.Unstaged.Concat(Tab.WorkingTree.Staged).First();
            var request = new DiffStagingRequest(new WorkingDiffRequest(side, shown), lines, isRange);
            return Discard.ExecuteSelection(Tab, request, null, onDiscarded);
        }

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

        public void Focus(PaneKind pane) => Send(new FocusPaneCommand(pane));

        public void Dispose() => _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new LeaveWorkingTreeCommand()));

        private bool Confirm(ConfirmPrompt prompt, Action onYes)
        {
            if (!CanShowDialog)
            {
                return false;
            }

            Prompts.Add(prompt);
            _answers.Add(onYes);
            return true;
        }

        private bool RefreshStatus(TabId tabId, Action done)
        {
            Pump.Post(() =>
            {
                OnRefresh();
                done();
            });
            return true;
        }

        private void Send(RepositoryCommand command) =>
            _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, command));
    }
}
