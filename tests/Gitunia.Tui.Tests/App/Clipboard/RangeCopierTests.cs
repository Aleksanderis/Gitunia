using Gitunia.App.Status;
using Gitunia.App.Tabs;
using System.Text;
using Gitunia.App.Loading.Commits;
using Gitunia.App;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Lists;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Rendering.Graph;
using Gitunia.Tui.Tests.Rendering.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.App.Clipboard;

public class RangeCopierTests : IDisposable
{
    private readonly StringWriter _terminal = new();
    private TerminalTabSession? _session;

    [Fact]
    public void GivenNoSession_WhenCopying_ThenANoticeSaysNothingWasCopied()
    {
        var copier = CreateCopier(null);

        Assert.Equal(new RangeCopyResult(false, "Nothing to copy"), copier.Copy(PaneKind.Graph, new RowRange(0, 1)));
        Assert.Equal(string.Empty, _terminal.ToString());
    }

    [Fact]
    public async Task GivenAGraphRange_WhenCopying_ThenTheHashesGoToTheClipboardAndTheNoticeIsEmpty()
    {
        using var testRepository = TestRepository.CreateCommitted("one", new TestFile("a.txt", "a\n"));
        using var session = await CreateSessionAsync(testRepository);

        var result = CreateCopier(session).Copy(PaneKind.Graph, new RowRange(0, 1));

        Assert.Equal(new RangeCopyResult(true, string.Empty), result);
        var text = $"{CommitFactory.Id(2)}\n{CommitFactory.Id(1)}\n";
        Assert.Equal($"\u001b]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}\u001b\\", _terminal.ToString());
    }

    [Fact]
    public void GivenATreeRangeOverAFolderRowAndItsFile_WhenCopying_ThenTheFolderAndTheFilePathsAreCopied()
    {
        var tree = WorkingTreeEntries.Tree(
            WorkingTreeEntries.Modified("src/a.cs"),
            WorkingTreeEntries.Modified("top.txt"));
        tree = tree.WithLayout(FilePathStyle.Tree, tree.CollapsedUnstaged, tree.CollapsedStaged);
        var copier = new RangeCopier(
            new ClipboardCopier(
                new ClipboardWriter(_terminal, new Osc52Options(Osc52Mode.Direct, 100_000)),
                new StatusReporter(new Dispatcher(AppState.Empty))),
            new FakeActiveSessions(() => null),
            new FakeActiveWorkingTree(() => tree));

        var result = copier.Copy(PaneKind.WorkingUnstaged, new RowRange(0, 2));

        Assert.True(result.Copied);
        const string text = "src\nsrc/a.cs\ntop.txt";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        Assert.Equal($"\u001b]52;c;{encoded}\u001b\\", _terminal.ToString());
    }

    [Fact]
    public async Task GivenARangeWithoutRowsOrInTheSidebar_WhenCopying_ThenNothingIsCopied()
    {
        using var testRepository = TestRepository.CreateCommitted("one", new TestFile("a.txt", "a\n"));
        using var session = await CreateSessionAsync(testRepository);

        Assert.Equal(new RangeCopyResult(false, "Nothing to copy"), CreateCopier(session).Copy(PaneKind.Graph, new RowRange(5, 6)));
        Assert.Equal(new RangeCopyResult(false, "Nothing to copy"), CreateCopier(session).Copy(PaneKind.Sidebar, new RowRange(0, 0)));
        Assert.Equal(string.Empty, _terminal.ToString());
    }

    [Fact]
    public async Task GivenAClipboardThatRefusesTheText_WhenCopying_ThenTheResultSaysNotCopied()
    {
        using var testRepository = TestRepository.CreateCommitted("one", new TestFile("a.txt", "a\n"));
        _session = await CreateSessionAsync(testRepository);
        var statuses = new StatusReporter(new Dispatcher(AppState.Empty));
        var writer = new ClipboardWriter(_terminal, new Osc52Options(Osc52Mode.Direct, 10));
        var copier = new RangeCopier(new ClipboardCopier(writer, statuses), new FakeActiveSessions(() => _session));

        var result = copier.Copy(PaneKind.Graph, new RowRange(0, 1));

        Assert.False(result.Copied);
        Assert.Equal(string.Empty, _terminal.ToString());
        Assert.Equal(StatusSeverity.Warning, statuses.Log.Latest?.Severity);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _terminal.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<TerminalTabSession> CreateSessionAsync(TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, TestContext.Current.CancellationToken);
        var session = new TerminalTabSession(
            new TabSessionParts(
                new TabId(1),
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero)),
            GraphGlyphSet.Unicode);
        session.Append(CommitFactory.Graph(CommitFactory.Commit(2, "b", [1]), CommitFactory.Commit(1, "a", [])).ToArray());
        return session;
    }

    private RangeCopier CreateCopier(TerminalTabSession? session) =>
        new(new ClipboardCopier(new ClipboardWriter(_terminal, new Osc52Options(Osc52Mode.Direct, 100_000)), new StatusReporter(new Dispatcher(AppState.Empty))), new FakeActiveSessions(() => session));
}
