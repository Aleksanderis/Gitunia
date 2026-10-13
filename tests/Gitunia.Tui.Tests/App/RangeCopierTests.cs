using Gitunia.Core.Clipboard;
using Gitunia.Core.Lists;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Tests.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class RangeCopierTests : IDisposable
{
    private readonly StringWriter terminal = new();

    [Fact]
    public void GivenNoSession_WhenCopying_ThenANoticeSaysNothingWasCopied()
    {
        var copier = CreateCopier(null);

        Assert.Equal(new RangeCopyResult(false, "Nothing to copy"), copier.Copy(PaneKind.Graph, new RowRange(0, 1)));
        Assert.Equal(string.Empty, terminal.ToString());
    }

    [Fact]
    public async Task GivenAGraphRange_WhenCopying_ThenTheHashesGoToTheClipboardAndTheNoticeIsEmpty()
    {
        using var repository = TestRepository.Create().Commit("one", ("a.txt", "a\n"));
        using var session = await CreateSessionAsync(repository);

        var result = CreateCopier(session).Copy(PaneKind.Graph, new RowRange(0, 1));

        Assert.Equal(new RangeCopyResult(true, string.Empty), result);
        var text = CommitFactory.Id(2) + "\n" + CommitFactory.Id(1) + "\n";
        Assert.Equal("\u001b]52;c;" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)) + "\u001b\\", terminal.ToString());
    }

    [Fact]
    public async Task GivenARangeWithoutRowsOrInTheSidebar_WhenCopying_ThenNothingIsCopied()
    {
        using var repository = TestRepository.Create().Commit("one", ("a.txt", "a\n"));
        using var session = await CreateSessionAsync(repository);

        Assert.Equal(new RangeCopyResult(false, "Nothing to copy"), CreateCopier(session).Copy(PaneKind.Graph, new RowRange(5, 6)));
        Assert.Equal(new RangeCopyResult(false, "Nothing to copy"), CreateCopier(session).Copy(PaneKind.Sidebar, new RowRange(0, 0)));
        Assert.Equal(string.Empty, terminal.ToString());
    }

    [Fact]
    public async Task GivenAClipboardThatRefusesTheText_WhenCopying_ThenTheResultSaysNotCopied()
    {
        using var repository = TestRepository.Create().Commit("one", ("a.txt", "a\n"));
        using var session = await CreateSessionAsync(repository);
        var statuses = new StatusChannel();
        var copier = new RangeCopier(new ClipboardCopier(new ClipboardWriter(terminal, new Osc52Options(Osc52Mode.Direct, 10)), statuses), () => session);

        var result = copier.Copy(PaneKind.Graph, new RowRange(0, 1));

        Assert.False(result.Copied);
        Assert.Equal(string.Empty, terminal.ToString());
        Assert.Equal(StatusSeverity.Warning, statuses.Log.Latest?.Severity);
    }

    public void Dispose()
    {
        terminal.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<TabSession> CreateSessionAsync(TestRepository repository)
    {
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, TestContext.Current.CancellationToken);
        var session = new TabSession(new TabId(1), GraphGlyphSet.Unicode, new CommitDetailsStore(repo, action => action(), TimeSpan.Zero));
        session.Append(CommitFactory.Graph(CommitFactory.Commit(2, "b", [1]), CommitFactory.Commit(1, "a", [])).ToArray());
        return session;
    }

    private RangeCopier CreateCopier(TabSession? session) =>
        new(new ClipboardCopier(new ClipboardWriter(terminal, new Osc52Options(Osc52Mode.Direct, 100_000)), new StatusChannel()), () => session);
}
