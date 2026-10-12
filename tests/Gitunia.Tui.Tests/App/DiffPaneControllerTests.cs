using Gitunia.Core.Clipboard;
using Gitunia.Core.Status;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class DiffPaneControllerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAFileRow_WhenOpeningTheDiff_ThenTheDiffOpensFocusedAtThatFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.SelectFile("b.txt");

        harness.Execute(ActionIds.DetailOpenDiff);
        await harness.WaitForDiffAsync();

        Assert.True(harness.Session.Diff!.IsOpen);
        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAnInfoRow_WhenOpeningTheDiff_ThenNothingOpensAndANoticeSaysWhy()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.DetailList.SelectedItem = 0;

        harness.Execute(ActionIds.DetailOpenDiff);

        Assert.False(harness.Session.Diff!.IsOpen);
        Assert.Equal("Select a file to see its diff", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenJumpingBetweenHunksAndFiles_ThenTheCursorLandsOnTheirHeaders()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var document = harness.Session.Diff!.Source.Document;
        Assert.True(document.HunkRows.Count >= 2);
        Assert.True(document.FileRows.Count >= 2);

        harness.Execute(ActionIds.DiffNextHunk);
        var firstAfterStart = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffNextHunk);
        harness.Execute(ActionIds.DiffPreviousHunk);
        var back = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffNextFile);

        Assert.Contains(firstAfterStart!.Value, document.HunkRows);
        Assert.Equal(firstAfterStart, back);
        Assert.Equal(document.FileRows[1], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenTheLastHunk_WhenJumpingForward_ThenTheCursorStaysAndANoticeSaysSo()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.ListBottom);

        harness.Execute(ActionIds.DiffNextHunk);

        Assert.Equal(harness.Session.Diff!.Source.Count - 1, harness.DiffList.SelectedItem);
        Assert.Equal("No next hunk", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenSearching_ThenMatchesAreHighlightedAndNStepsThroughThem()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffSearchOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("line");
        Assert.StartsWith("/line_", harness.Controller.StatusLine, StringComparison.Ordinal);
        harness.Execute(ActionIds.InputAccept);
        var first = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffSearchNext);
        var second = harness.DiffList.SelectedItem;

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal("line", harness.Session.Diff!.Source.Highlight);
        Assert.True(second > first);
    }

    [Fact]
    public async Task GivenAnActiveSearch_WhenGoingBack_ThenTheSearchClearsBeforeThePaneCloses()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("line");
        harness.Execute(ActionIds.InputAccept);

        harness.Execute(ActionIds.NavigateBack);

        Assert.True(harness.Session.Diff!.IsOpen);
        Assert.Null(harness.Session.Diff.Source.Highlight);
        harness.Execute(ActionIds.NavigateBack);
        Assert.False(harness.Session.Diff.IsOpen);
        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
        Assert.Empty(harness.Session.Diff.Source.Document.Rows);
    }

    [Fact]
    public async Task GivenAnOpenSearchBox_WhenCancelling_ThenTheModeIsNormalAgain()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("x");

        harness.Execute(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Controller.StatusLine);
        Assert.True(harness.Session.Diff!.IsOpen);
    }

    [Fact]
    public async Task GivenAFocusedPane_WhenZooming_ThenItFillsTheScreenUntilZoomedAgainOrFocusLeaves()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.PaneZoom);
        Assert.Equal(PaneKind.Diff, harness.Controller.Arrangement().Zoomed);
        harness.Execute(ActionIds.PaneZoom);
        Assert.Null(harness.Controller.Arrangement().Zoomed);
        harness.Execute(ActionIds.PaneZoom);
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.Null(harness.Controller.Arrangement().Zoomed);
        Assert.True(harness.Controller.Arrangement().IsDiffOpen);
    }

    [Fact]
    public async Task GivenTheSidebarIsFocused_WhenZooming_ThenANoticeSaysItCannot()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.Send(new FocusPaneCommand(PaneKind.Sidebar));

        harness.Execute(ActionIds.PaneZoom);

        Assert.Equal("The sidebar cannot be zoomed", harness.Controller.Notice);
        Assert.Null(harness.Controller.Arrangement().Zoomed);
    }

    [Fact]
    public async Task GivenAMergeCommitSelected_WhenOpeningAFile_ThenTheFirstParentDiffShows()
    {
        using var repository = TestRepository.Create()
            .Commit("root", ("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", ("side.txt", "side\n"))
            .Checkout("main")
            .Commit("main work", ("main.txt", "main\n"))
            .Merge("side");
        using var harness = await DiffHarness.LoadAsync(repository);
        harness.SelectFile("side.txt");

        harness.Execute(ActionIds.DetailOpenDiff);
        await harness.WaitForDiffAsync();

        Assert.Equal(["side.txt"], harness.Session.Diff!.Source.Document.FilePaths.Select(names => names[0]));
    }

    [Fact]
    public async Task GivenTwoTabs_WhenSwitchingAway_ThenTheDiffCursorIsRestored()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffNextHunk);
        var row = harness.DiffList.SelectedItem;

        harness.Controller.OnTabSwitched(harness.Session, null);
        Assert.Null(harness.DiffList.Source);
        harness.Controller.OnTabSwitched(null, harness.Session);

        Assert.Equal(row, harness.DiffList.SelectedItem);
        Assert.Same(harness.Session.Diff!.Source, harness.DiffList.Source);
    }

    [Fact]
    public async Task GivenADiffThatLoadsWhileAnotherTabIsShown_WhenSwitchingBack_ThenItShowsTheLoadedDiffAtTheFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.SelectFile("b.txt");
        harness.Execute(ActionIds.DetailOpenDiff);
        harness.Controller.OnTabSwitched(harness.Session, null);
        await harness.WaitForDiffAsync();

        harness.Controller.OnTabSwitched(null, harness.Session);

        var document = harness.Session.Diff!.Source.Document;
        Assert.Equal(DiffStatus.Loaded, harness.Session.Diff.Store.Status);
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenALongLine_WhenTogglingWrap_ThenItTakesMoreLinesAndTheCursorKeepsItsRow()
    {
        using var repository = TestRepository.Create().Commit("long", ("a.txt", new string('x', 150) + "\n"));
        using var harness = await DiffHarness.LoadAsync(repository);
        await harness.OpenAsync("a.txt");
        var source = harness.Session.Diff!.Source;
        var rows = source.Document.Rows.Count;
        harness.Execute(ActionIds.ListBottom);
        var row = source.RowOf(harness.DiffList.SelectedItem!.Value);

        harness.Execute(ActionIds.DiffToggleWrap);

        Assert.True(source.IsWrapped);
        Assert.Equal(rows + 2, source.Count);
        Assert.Equal(row, source.RowOf(harness.DiffList.SelectedItem!.Value));
        Assert.Equal("Word wrap on", harness.Controller.Notice);
        harness.Execute(ActionIds.DiffToggleWrap);
        Assert.False(source.IsWrapped);
        Assert.Equal(rows, source.Count);
        Assert.Equal(row, harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenWrapOn_WhenThePaneWidthChanges_ThenTheCursorStaysOnItsRow()
    {
        using var repository = TestRepository.Create().Commit("long", ("a.txt", new string('x', 150) + "\nshort\n"));
        using var harness = await DiffHarness.LoadAsync(repository);
        await harness.OpenAsync("a.txt");
        var source = harness.Session.Diff!.Source;
        harness.Execute(ActionIds.DiffToggleWrap);
        harness.Execute(ActionIds.ListBottom);
        var row = source.RowOf(harness.DiffList.SelectedItem!.Value);

        harness.DiffList.Width = 30;
        harness.DiffList.SetRelativeLayout(new System.Drawing.Size(80, 24));
        harness.Execute(ActionIds.ListUp);
        harness.Execute(ActionIds.ListDown);

        Assert.Equal(row, source.RowOf(harness.DiffList.SelectedItem!.Value));
        Assert.True(source.Count > source.Document.Rows.Count + 4);
    }

    [Fact]
    public async Task GivenTwoQuickSelectionChanges_WhenFollowing_ThenTheRememberedFileSurvives()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("b.txt");
        var store = harness.Session.Diff!.Store;

        await harness.SelectCommitAsync(harness.Session.Graph[1].Id);
        harness.Controller.FollowSelection();
        await harness.SelectCommitAsync(harness.Session.Graph[0].Id);
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();

        Assert.Equal(harness.Session.Graph[0].Id, store.Commit);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenACutDiff_WhenLoadingAnyway_ThenTheWholeDiffLoadsAndTheFileKeepsTheCursor()
    {
        using var harness = await DiffHarness.LoadAsync(rowLimit: 8);
        await harness.OpenAsync("a.txt");
        var cutRows = harness.Session.Diff!.Source.Document.Rows.Count;
        Assert.True(harness.Session.Diff.Store.IsCut);

        harness.Execute(ActionIds.DiffLoadAnyway);
        await harness.WaitForDiffAsync();

        Assert.False(harness.Session.Diff.Store.IsCut);
        Assert.True(harness.Session.Diff.Source.Document.Rows.Count > cutRows);
        Assert.Equal(0, harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAnUncutDiff_WhenLoadingAnyway_ThenANoticeSaysNothingWasCut()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffLoadAnyway);

        Assert.Equal("Nothing was cut short", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenTheSelectedCommitChanges_ThenTheDiffFollowsAndKeepsTheFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("b.txt");
        var shown = harness.Session.Diff!.Store.Commit;

        await harness.SelectCommitAsync(harness.Session.Graph[1].Id);
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();

        Assert.NotEqual(shown, harness.Session.Diff.Store.Commit);
        Assert.Equal(harness.Session.Graph[1].Id, harness.Session.Diff.Store.Commit);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAClosedDiff_WhenTheSelectedCommitChanges_ThenNothingLoads()
    {
        using var harness = await DiffHarness.LoadAsync();

        await harness.SelectCommitAsync(harness.Session.Graph[1].Id);
        harness.Controller.FollowSelection();

        Assert.Equal(DiffStatus.None, harness.Session.Diff!.Store.Status);
    }

    [Fact]
    public async Task GivenAnAddedRow_WhenCopyingTheLine_ThenItGoesToTheClipboardWithItsPlusPrefix()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.SelectFirstRowOfKind(DiffRowKind.Added);

        harness.Execute(ActionIds.DiffCopyLine);

        Assert.Equal("+line 3 changed", harness.CopiedText());
    }

    [Fact]
    public async Task GivenACursorInAFile_WhenCopyingTheFile_ThenOnlyThatFilesRowsAreCopied()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var document = harness.Session.Diff!.Source.Document;
        harness.DiffList.SelectedItem = document.FileRows[document.FileIndexOf("b.txt")];

        harness.Execute(ActionIds.DiffCopyFile);

        var copied = harness.CopiedText();
        Assert.Contains("-b\n", copied, StringComparison.Ordinal);
        Assert.Contains("+b changed\n", copied, StringComparison.Ordinal);
        Assert.DoesNotContain("line 3", copied, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACursorAboveTheFirstFile_WhenCopyingTheFile_ThenANoticeSaysItIsNotInAFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.ShowWarningOnly("parser warning");

        harness.Execute(ActionIds.DiffCopyFile);

        Assert.Equal("The cursor is not in a file", harness.Controller.Notice);
        Assert.Equal(string.Empty, harness.Clipboard.ToString());
    }

    [Fact]
    public async Task GivenAWarningRow_WhenCopyingTheLine_ThenNothingIsCopied()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.ShowWarningOnly("parser warning");

        harness.Execute(ActionIds.DiffCopyLine);

        Assert.Equal("Nothing to copy", harness.Controller.Notice);
        Assert.Equal(string.Empty, harness.Clipboard.ToString());
    }

    [Fact]
    public async Task GivenWrapOnAndTheCursorOnAContinuationLine_WhenCopyingTheLine_ThenTheWholeRowIsCopied()
    {
        using var repository = TestRepository.Create().Commit("long", ("a.txt", new string('x', 150) + "\n"));
        using var harness = await DiffHarness.LoadAsync(repository);
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffToggleWrap);
        harness.Execute(ActionIds.ListBottom);
        var source = harness.Session.Diff!.Source;
        Assert.True(harness.DiffList.SelectedItem > source.LineOf(source.RowOf(harness.DiffList.SelectedItem!.Value)));

        harness.Execute(ActionIds.DiffCopyLine);

        Assert.Equal("+" + new string('x', 150), harness.CopiedText());
    }

    private sealed class DiffHarness : IDisposable
    {
        private readonly TestRepository? ownedRepository;
        private readonly KeyHolder keys = new();

        private DiffHarness(WorkspaceHost host, TabSession session, TestRepository? ownedRepository)
        {
            Host = host;
            Session = session;
            this.ownedRepository = ownedRepository;
            DetailList = new ListView { Source = session.DetailSource };
            DiffList = new ListView { X = 0, Y = 0, Width = 60, Height = 20 };
            DiffList.SetRelativeLayout(new System.Drawing.Size(80, 24));
            Controller = new DiffPaneController(host, () => Session, DetailList, DiffList, mode => keys.Mode = mode, () => 40, () => { }, () => { }, new ClipboardCopier(new ClipboardWriter(Clipboard, new Osc52Options(Osc52Mode.Direct, 100_000)), new StatusChannel()));
            Controller.OnTabSwitched(null, session);
        }

        public WorkspaceHost Host { get; }

        public TabSession Session { get; }

        public ListView DetailList { get; }

        public ListView DiffList { get; }

        public DiffPaneController Controller { get; }

        /// <summary>What the controller wrote to the terminal (OSC 52 sequences).</summary>
        public StringWriter Clipboard { get; } = new();

        public KeyMode Mode => keys.Mode;

        public RepoState Tab => Host.Workspace.ActiveTab!;

        public static async Task<DiffHarness> LoadAsync(TestRepository? repository = null, int rowLimit = DiffStore.DefaultRowLimit)
        {
            var owned = repository is null ? CreateDefaultRepository() : null;
            var path = (repository ?? owned)!.Path;
            var runner = new GitRunner();
            var location = await RepositoryDiscovery.DiscoverAsync(runner, path, Token);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var repo = new Repo(runner, location);
            var session = new TabSession(
                tabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(repo, action => action(), TimeSpan.Zero),
                new DiffStore(repo, action => action(), rowLimit));
            await RepoLoader.RunAsync(runner, location, session, host, new StatusChannel(), InlineUi.Post, session.Cancellation.Token);
            var harness = new DiffHarness(host, session, owned);
            await harness.ShowDetailsOfAsync(session.Graph[0].Id);
            return harness;
        }

        /// <summary>The text of the last OSC 52 copy, decoded.</summary>
        public string CopiedText()
        {
            var sequence = Clipboard.ToString();
            const string prefix = "\u001b]52;c;";
            Assert.StartsWith(prefix, sequence, StringComparison.Ordinal);
            var payload = sequence[prefix.Length..sequence.IndexOf('\u001b', prefix.Length)];
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }

        public void SelectFirstRowOfKind(DiffRowKind kind)
        {
            var rows = Session.Diff!.Source.Document.Rows;
            DiffList.SelectedItem = rows.ToList().FindIndex(row => row.Kind == kind);
        }

        /// <summary>Swaps the shown document for one that only holds a parser warning row (the rows above the first file).</summary>
        public void ShowWarningOnly(string warning)
        {
            Session.Diff!.Source.Update(new DiffDocumentBuilder().Build([warning]));
            DiffList.SelectedItem = 0;
        }

        public void Send(RepoCommand command) => Host.Dispatch(new TabCommand(Tab.Id, command));

        public void Execute(ActionId action, int? count = null) => Controller.TryExecute(action, count);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Controller.HandleTextKey(new KeyEvent(character.ToString(), KeyModifiers.None));
            }
        }

        public void SelectFile(string path)
        {
            Send(new FocusPaneCommand(PaneKind.Detail));
            var content = Session.DetailSource.Content;
            var index = content.Files.ToList().FindIndex(file => file.Path == path);
            Assert.True(index >= 0, $"{path} is not in the file list");
            DetailList.SelectedItem = content.FirstFileRow + index;
        }

        public async Task OpenAsync(string path)
        {
            SelectFile(path);
            Execute(ActionIds.DetailOpenDiff);
            await WaitForDiffAsync();
        }

        public async Task WaitForDiffAsync()
        {
            await WaitForAsync(handler => Session.Diff!.Store.Changed += handler, handler => Session.Diff!.Store.Changed -= handler, () => Session.Diff!.Store.Status == DiffStatus.Loaded, "the diff did not load");
        }

        public async Task SelectCommitAsync(ObjectId commit)
        {
            Session.Details.Select(commit);
            await WaitForAsync(handler => Session.Details.Changed += handler, handler => Session.Details.Changed -= handler, () => Session.Details.Current.Status == CommitDetailsStatus.Loaded && Session.Details.Current.Commit == commit, "the details did not load");
        }

        public void Dispose()
        {
            Session.Dispose();
            ownedRepository?.Dispose();
        }

        private static TestRepository CreateDefaultRepository() => TestRepository.Create()
            .Commit("root", ("a.txt", string.Concat(Enumerable.Range(1, 40).Select(index => $"line {index}\n"))), ("b.txt", "b\n"))
            .Commit(
                "edit",
                ("a.txt", string.Concat(Enumerable.Range(1, 40).Select(index => index is 3 or 38 ? $"line {index} changed\n" : $"line {index}\n"))),
                ("b.txt", "b changed\n"));

        private static async Task WaitForAsync(Action<Action> subscribe, Action<Action> unsubscribe, Func<bool> condition, string message)
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnChanged()
            {
                if (condition())
                {
                    signal.TrySetResult();
                }
            }

            subscribe(OnChanged);
            try
            {
                OnChanged();
                await signal.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            }
            catch (TimeoutException)
            {
                Assert.Fail(message);
            }
            finally
            {
                unsubscribe(OnChanged);
            }
        }

        private async Task ShowDetailsOfAsync(ObjectId commit)
        {
            Session.Details.Select(commit);
            await WaitForAsync(handler => Session.Details.Changed += handler, handler => Session.Details.Changed -= handler, () => Session.Details.Current.Status == CommitDetailsStatus.Loaded, "the details did not load");

            var entry = Session.Graph[0];
            Session.DetailSource.Update(DetailContentBuilder.Build(entry, Session.Details.Current));
            Send(new FocusPaneCommand(PaneKind.Detail));
        }
    }

    private sealed class KeyHolder
    {
        public KeyMode Mode { get; set; } = KeyMode.Normal;
    }
}
