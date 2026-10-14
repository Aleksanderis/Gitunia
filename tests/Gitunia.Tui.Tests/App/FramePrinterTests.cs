using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class FramePrinterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenNoArrangementOnTheDetailsTab_WhenPrinting_ThenOnlyTheDetailPaneIsShown()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "one\n"));

        var lines = await PrintAsync(repository, 100, 30, null, BottomTab.Details);

        Assert.Contains(lines, line => line.Contains("Details", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains(" Diff ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenNoArrangementOnTheChangesTab_WhenPrinting_ThenTheDiffOfTheFirstFileSitsBesideTheFileList()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "one\n"));

        var lines = await PrintAsync(repository, 100, 30, null);

        Assert.Contains(lines, line => line.Contains("Changes", StringComparison.Ordinal) && line.Contains(" Diff ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("+one", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenANarrowScreenOnTheChangesTab_WhenPrinting_ThenTheDiffStaysClosed()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "one\n"));

        var lines = await PrintAsync(repository, 71, 30, null);

        Assert.DoesNotContain(lines, line => line.Contains(" Diff ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(100)]
    public async Task GivenAnOpenDiff_WhenPrinting_ThenFileListAndDiffRowsShareTheFrame(int width)
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "one\n"));
        repository.Commit("second", new TestFile("a.txt", "one\ntwo added\n"));

        var lines = await PrintAsync(repository, width, 30, new PaneArrangement(true, null));

        Assert.Contains(lines, line => line.Contains("Details", StringComparison.Ordinal) && line.Contains(" Diff ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("two added", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenAZoomedDiffOfARootCommit_WhenPrinting_ThenTheDiffFillsTheFrame()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "first line\n"));

        var lines = await PrintAsync(repository, 80, 24, new PaneArrangement(true, PaneKind.Diff));

        Assert.Contains(lines, line => line.Contains("first line", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Details", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenEachTab_WhenPrinting_ThenThePaneShowsThatTabsContent()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root commit message", new TestFile("a.txt", "one\n"));

        var details = await PrintAsync(repository, 100, 30, null, BottomTab.Details);
        var changes = await PrintAsync(repository, 100, 30, null, BottomTab.Changes);
        var files = await PrintAsync(repository, 100, 30, null, BottomTab.Files);

        Assert.Contains(details, line => line.StartsWith("│ author ", StringComparison.Ordinal));
        Assert.DoesNotContain(details, line => line.Contains("A a.txt", StringComparison.Ordinal));
        Assert.Contains(changes, line => line.Contains("A a.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(changes, line => line.StartsWith("│ author ", StringComparison.Ordinal));
        Assert.Contains(files, line => line.Contains("not implemented yet", StringComparison.Ordinal));
        Assert.DoesNotContain(files, line => line.Contains("A a.txt", StringComparison.Ordinal));
        Assert.All([details, changes, files], lines => Assert.Contains(lines, line => line.Contains(" Details ─ Changes ─ Files ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GivenAnOpenDiffAndTheFilesTab_WhenPrinting_ThenThePlaceholderSitsBesideTheDiff()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "first line\n"));

        var lines = await PrintAsync(repository, 100, 30, new PaneArrangement(true, null), BottomTab.Files);

        Assert.Contains(lines, line => line.Contains("The file tree", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("first line", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenColours_WhenPrinting_ThenTheGraphRowHasTheFocusedTintAndTheSidebarCursorTheDimOne()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "one\n"));
        repository.Branch("topic");
        var table = PaletteTable.For(PaletteSettings.Default);

        var lines = await PrintAsync(repository, 100, 30, null, colors: PaletteSettings.Default);

        var focusedTint = $"48;2;{table.SelectionFocused.R};{table.SelectionFocused.G};{table.SelectionFocused.B}";
        var dimTint = $"48;2;{table.SelectionDim.R};{table.SelectionDim.G};{table.SelectionDim.B}";
        var firstRow = lines[2];
        Assert.Contains(dimTint, firstRow, StringComparison.Ordinal);
        Assert.Contains(focusedTint, firstRow, StringComparison.Ordinal);
        Assert.True(firstRow.IndexOf(dimTint, StringComparison.Ordinal) < firstRow.IndexOf(focusedTint, StringComparison.Ordinal), "the sidebar part comes first");
        Assert.DoesNotContain(lines.Skip(3).Take(5), line => line.Contains(focusedTint, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenNoColours_WhenPrinting_ThenTheFrameHasNoEscapeSequences()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "one\n"));

        var lines = await PrintAsync(repository, 100, 30, null);

        Assert.DoesNotContain(lines, line => line.Contains('\u001b', StringComparison.Ordinal));
    }

    private static async Task<List<string>> PrintAsync(TestRepository repository, int width, int height, PaneArrangement? arrangement, BottomTab tab = BottomTab.Changes, PaletteSettings? colors = null)
    {
        var runner = new GitRunner();
        var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, Token);
        using var writer = new StringWriter();
        await FramePrinter.PrintAsync(runner, location, width, height, GraphGlyphSet.Unicode, writer, Token, arrangement, tab, colors);
        return [.. writer.ToString().Split('\n')];
    }
}
