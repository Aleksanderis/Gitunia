using Gitunia.Core.Clipboard;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Rendering.Graph;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Clipboard;

public class DetailPathCopierTests : IDisposable
{
    private readonly StringWriter terminal = new();

    [Fact]
    public void GivenAFileRow_WhenCopying_ThenItsPathGoesToTheClipboard()
    {
        var source = LoadedSource("src/a.cs", "docs/b.md");

        var notice = CreateCopier().Copy(source, source.Content.FirstFileRow + 1);

        Assert.Equal(string.Empty, notice);
        Assert.Equal(Osc52Sequence("docs/b.md"), terminal.ToString());
    }

    [Fact]
    public void GivenARowAboveTheFiles_WhenCopying_ThenANoticeSaysToSelectAFile()
    {
        var source = LoadedSource("src/a.cs");

        var notice = CreateCopier().Copy(source, source.Content.FirstFileRow - 1);

        Assert.Equal("Select a file to copy its path", notice);
        Assert.Equal(string.Empty, terminal.ToString());
    }

    [Fact]
    public void GivenARowPastTheLastFile_WhenCopying_ThenNothingIsCopied()
    {
        var source = LoadedSource("src/a.cs");

        var notice = CreateCopier().Copy(source, source.Content.FirstFileRow + 1);

        Assert.Equal("Select a file to copy its path", notice);
        Assert.Equal(string.Empty, terminal.ToString());
    }

    [Fact]
    public void GivenNoSelectionOrNoSession_WhenCopying_ThenNothingIsCopied()
    {
        var source = LoadedSource("src/a.cs");

        Assert.Equal("Select a file to copy its path", CreateCopier().Copy(source, null));
        Assert.Equal("Select a file to copy its path", CreateCopier().Copy(null, 0));
        Assert.Equal(string.Empty, terminal.ToString());
    }

    public void Dispose()
    {
        terminal.Dispose();
        GC.SuppressFinalize(this);
    }

    private static DetailListSource LoadedSource(params string[] paths)
    {
        var commit = CommitFactory.Commit(2, "Fix the thing", [1], []);
        var entry = CommitFactory.Graph(commit, CommitFactory.Commit(1, "root", []))[0];
        var when = new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero);
        var files = paths.Select(path => new CommitFileChange(path, null, FileChangeKind.Modified, null)).ToArray();
        var details = new CommitDetails(CommitFactory.Id(2), [CommitFactory.Id(1)], "Ann", "ann@x.org", when, "Ann", "ann@x.org", when, "Fix the thing", files);
        var source = new DetailListSource(GraphGlyphSet.Unicode);
        source.Update(DetailContentBuilder.Build(entry, new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty), BottomTab.Changes));
        return source;
    }

    private static string Osc52Sequence(string text) => "\u001b]52;c;" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)) + "\u001b\\";

    private DetailPathCopier CreateCopier() =>
        new(new ClipboardCopier(new ClipboardWriter(terminal, new Osc52Options(Osc52Mode.Direct, 100_000)), new StatusChannel()));
}
