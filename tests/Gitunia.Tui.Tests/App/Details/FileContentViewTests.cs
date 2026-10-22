using System.Drawing;
using System.Text;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Diff;
using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Blobs;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Loading.Trees;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Details;

/// <summary>Enter on a file of the Files tab through the real path: key handler, diff pane, stores, git.</summary>
public class FileContentViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAFileRow_WhenPressingEnter_ThenItsLinesShowInTheDiffPaneWhichTakesFocus()
    {
        using var harness = await Harness.LoadAsync();

        harness.SelectFile("a.txt");
        harness.Enter();
        await harness.WaitForContentAsync();

        var diff = harness.Session.Diff!;
        Assert.True(diff.IsOpen);
        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
        Assert.Equal(["one", "two", "three"], diff.Source.Document.Rows.Select(row => row.Text));
        Assert.True(diff.Source.Document.IsFileContent);
        Assert.Equal(string.Empty, harness.FileTreeNotice);
        var title = harness.Controller.TitleLine(80, GraphGlyphSet.Unicode).Text;
        Assert.Contains("File", title, StringComparison.Ordinal);
        Assert.Contains("3 lines", title, StringComparison.Ordinal);
        Assert.Contains("a.txt", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileInAFolder_WhenOpened_ThenTheTitleNamesItsFullPath()
    {
        using var harness = await Harness.LoadAsync();

        harness.SelectFile("docs/guide.md");
        harness.Enter();
        await harness.WaitForContentAsync();

        var title = harness.Controller.TitleLine(80, GraphGlyphSet.Unicode).Text;
        Assert.Contains("docs/guide.md", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenABinaryFile_WhenOpened_ThenOnlyANoticeShows()
    {
        using var harness = await Harness.LoadAsync();

        harness.SelectFile("data.bin");
        harness.Enter();
        await harness.WaitForContentAsync();

        var rows = harness.Session.Diff!.Source.Document.Rows;
        Assert.Equal(DiffRowKind.Note, Assert.Single(rows).Kind);
        Assert.StartsWith("Binary file", rows[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileOverTheCap_WhenOpened_ThenANoticeNamesTheLimit()
    {
        using var harness = await Harness.LoadAsync();

        harness.SelectFile("big.txt");
        harness.Enter();
        await harness.WaitForContentAsync();

        var rows = harness.Session.Diff!.Source.Document.Rows;
        Assert.Equal(DiffRowKind.Note, Assert.Single(rows).Kind);
        Assert.StartsWith("File too large to show", rows[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnEmptyFile_WhenOpened_ThenANoticeSaysSo()
    {
        using var harness = await Harness.LoadAsync();

        harness.SelectFile("empty.txt");
        harness.Enter();
        await harness.WaitForContentAsync();

        Assert.Equal("Empty file", harness.Session.Diff!.Source.Document.Rows[0].Text);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenGoingBack_ThenThePaneClosesAndTheFileListHasFocus()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.NavigateBack);

        var diff = harness.Session.Diff!;
        Assert.False(diff.IsOpen);
        Assert.False(diff.IsShowingContent);
        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
        Assert.Empty(diff.Source.Document.Rows);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenJumpingToTheNextFile_ThenNothingMovesAndNoFileWaitsToBeShown()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var document = harness.Session.Diff!.Source.Document;

        harness.Execute(ActionIds.DiffNextFile);

        Assert.Same(document, harness.Session.Diff.Source.Document);
        Assert.Null(harness.Session.Diff.PendingFilePath);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenAnotherCommitIsSelected_ThenTheContentCloses()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        await harness.SelectCommitAsync(harness.OtherCommit);
        harness.Controller.FollowSelection();

        Assert.False(harness.Session.Diff!.IsOpen);
        Assert.False(harness.Session.Diff.IsShowingContent);
        Assert.Empty(harness.Session.Diff.Source.Document.Rows);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenTheSameCommitStaysSelected_ThenTheContentStays()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Controller.FollowSelection();

        Assert.True(harness.Session.Diff!.IsShowingContent);
        Assert.Equal(3, harness.Session.Diff.Source.Document.Rows.Count);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenSwitchingToTheChangesTab_ThenTheContentCloses()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Send(new SetBottomTabCommand(BottomTab.Changes));

        Assert.False(harness.Session.Diff!.IsShowingContent);
        Assert.False(harness.Session.Diff.IsOpen);
        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenSearching_ThenMatchesOfThatFileAreFound()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("t");

        Assert.Equal(2, harness.Session.Diff!.Search.Matches.Count);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenCopyingTheFile_ThenThePlainTextIsCopiedWithoutDiffMarkers()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffCopyFile);

        Assert.Equal("one\ntwo\nthree\n", harness.CopiedText());
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenCopyingALine_ThenThePlainLineIsCopied()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.ListDown);

        harness.Execute(ActionIds.DiffCopyLine);

        Assert.Equal("two", harness.CopiedText());
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenChangingDiffOptions_ThenANoticeSaysThereAreNoneAndTheContentStays()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var document = harness.Session.Diff!.Source.Document;

        harness.Execute(ActionIds.DiffMoreContext);

        Assert.Equal("The file content has no diff options", harness.Controller.Notice);
        Assert.Same(document, harness.Session.Diff.Source.Document);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenJumpingToAHunk_ThenTheNoticeDoesNotTalkAboutHunks()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffNextHunk);

        Assert.Equal("The file content has no changes to jump to", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenJumpingToAnotherFile_ThenTheNoticeSaysTheContentHasNone()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffNextFile);

        Assert.Equal("The file content has no other files to jump to", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenAsked_ThenThePaneReportsItShowsContent()
    {
        using var harness = await Harness.LoadAsync();
        Assert.False(harness.Controller.IsShowingContent);

        await harness.OpenAsync("a.txt");

        Assert.True(harness.Controller.IsShowingContent);
    }

    [Fact]
    public async Task GivenAnOpenFile_WhenSwitchingSideBySide_ThenTheContentStaysOneColumn()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Session.Diff!.Source.SetSideBySide(true);

        Assert.False(harness.Session.Diff.Source.ShowsSideBySide);
    }

    [Fact]
    public async Task GivenAFileOpenedWhileAnotherIsOpen_WhenItLoads_ThenTheLatestFileShows()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Send(new FocusPaneCommand(PaneKind.Detail));

        harness.SelectFile("docs/guide.md");
        harness.Enter();
        await harness.WaitForContentAsync();

        Assert.Equal(["# Guide"], harness.Session.Diff!.Source.Document.Rows.Select(row => row.Text));
    }

    [Fact]
    public async Task GivenTheFileAlreadyOpen_WhenOpeningItAgain_ThenTheCursorKeepsItsPlace()
    {
        using var harness = await Harness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var document = harness.Session.Diff!.Source.Document;
        harness.Send(new FocusPaneCommand(PaneKind.Detail));

        harness.SelectFile("a.txt");
        harness.Enter();

        Assert.Same(document, harness.Session.Diff.Source.Document);
        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenASubmoduleRow_WhenOpening_ThenANoticeSaysThereIsNoContent()
    {
        using var harness = await Harness.LoadAsync();
        var row = new FileTreeRow(FileTreeRowKind.Submodule, "mod", "mod", 0, false, null, 0);

        var notice = harness.Controller.OpenFileContent(row);

        Assert.Equal("A submodule has no content to show", notice);
        Assert.False(harness.Session.Diff!.IsOpen);
    }

    private sealed class Harness : IDisposable
    {
        private readonly TestRepository _repository;
        private readonly UiPump _pump;
        private readonly FileTreeController _fileTree;
        private KeyMode _mode = KeyMode.Normal;
        private int _cursor;

        private Harness(WorkspaceHost host, TabSession session, UiPump pump, TestRepository repository, ObjectId other)
        {
            Host = host;
            Session = session;
            _pump = pump;
            _repository = repository;
            OtherCommit = other;
            var detailList = new ListView { Source = session.DetailSource };
            var diffList = new ListView { X = 0, Y = 0, Width = 60, Height = 20 };
            diffList.SetRelativeLayout(new Size(80, 24));
            Controller = new DiffPaneController(
                host,
                () => Session,
                detailList,
                diffList,
                mode => _mode = mode,
                () => new ScreenSize(60, 40),
                () => { },
                () => { },
                new ClipboardCopier(
                    new ClipboardWriter(Clipboard, new Osc52Options(Osc52Mode.Direct, 100_000)),
                    new StatusChannel()));
            Controller.OnTabSwitched(null, session);
            _fileTree = new FileTreeController(
                host,
                () => Session,
                () => _cursor,
                row => _cursor = row,
                mode => _mode = mode,
                RefreshDetail,
                Controller.OpenFileContent);
        }

        public WorkspaceHost Host { get; }

        public TabSession Session { get; }

        public DiffPaneController Controller { get; }

        public ObjectId OtherCommit { get; }

        public StringWriter Clipboard { get; } = new();

        public RepositoryState Tab => Host.Workspace.ActiveTab!;

        public string FileTreeNotice => _fileTree.Notice;

        public static async Task<Harness> LoadAsync()
        {
            var repository = TestRepository.Create()
                .Commit("root", new TestFile("a.txt", "one\ntwo\nthree\n"))
                .Commit(
                    "second",
                    new TestFile("docs/guide.md", "# Guide\n"),
                    new TestFile("data.bin", "ab\0cd"),
                    new TestFile("big.txt", new string('x', BlobReader.MaximumBytes + 1)),
                    new TestFile("empty.txt", string.Empty));
            var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var pump = new UiPump(Token);
            var session = new TabSession(
                tabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, pump.Post, TimeSpan.Zero),
                new DiffStore(TestGit.Services.Diff, location, pump.Post),
                tree: new FileTreeSession(
                    new CommitTreeStore(TestGit.Services.CommitTree, location, pump.Post, TimeSpan.Zero)),
                contentStore: new FileContentStore(TestGit.Services.Blob, location, pump.Post));
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                session,
                host,
                new StatusChannel(),
                InlineUi.Post,
                session.Cancellation.Token);
            var head = session.Graph[0].Id;
            var harness = new Harness(host, session, pump, repository, session.Graph[1].Id);
            host.Dispatch(new TabCommand(tabId, new SetBottomTabCommand(BottomTab.Files)));
            await harness.SelectCommitAsync(head);
            session.Tree!.Store.Select(head);
            await pump.RunUntilAsync(() => session.Tree.Store.Current.Status == CommitDetailsStatus.Loaded);
            session.Tree.Expand(session.Tree.Rows.Single(row => row.Path == "docs"));
            harness.RefreshDetail();
            harness.Send(new FocusPaneCommand(PaneKind.Detail));

            return harness;
        }

        public async Task SelectCommitAsync(ObjectId commit)
        {
            Session.Details.Select(commit);
            await _pump.RunUntilAsync(
                () => Session.Details.Current.Status == CommitDetailsStatus.Loaded
                    && Session.Details.Current.Commit == commit);
        }

        public void Send(RepositoryCommand command) => Host.Dispatch(new TabCommand(Tab.Id, command));

        public void SelectFile(string path)
        {
            var rows = Session.DetailSource.Content.Tree;
            var index = rows.ToList().FindIndex(row => row.Path == path);
            Assert.True(index >= 0, $"{path} is not listed");
            _cursor = index;
        }

        public void Enter() => Assert.True(_fileTree.TryExecute(ActionIds.DetailOpenDiff, null));

        public async Task OpenAsync(string path)
        {
            SelectFile(path);
            Enter();
            await WaitForContentAsync();
        }

        public async Task WaitForContentAsync() =>
            await _pump.RunUntilAsync(() => Session.Diff!.ContentStore!.Current.Status == CommitDetailsStatus.Loaded);

        public void Execute(ActionId action) => Assert.True(Controller.TryExecute(action, null));

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Controller.HandleTextKey(new KeyEvent(character.ToString()));
            }
        }

        public string CopiedText()
        {
            var sequence = Clipboard.ToString();
            const string prefix = "\u001b]52;c;";
            Assert.StartsWith(prefix, sequence, StringComparison.Ordinal);
            var payload = sequence[prefix.Length..sequence.IndexOf('\u001b', prefix.Length)];

            return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }

        public void RefreshDetail()
        {
            var tree = Session.Tree!;
            var view = new FileTreeView(tree.Store.Current, tree.Rows, tree.Filter, SidebarGlyphs.Unicode);
            Session.DetailSource.Update(
                DetailContentBuilder.Build(Session.Graph[0], CommitDetailsState.Empty, BottomTab.Files, view));
        }

        public void Dispose()
        {
            Session.Dispose();
            _repository.Dispose();
        }
    }
}
