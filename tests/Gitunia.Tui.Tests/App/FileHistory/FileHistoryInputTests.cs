using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Diff;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.FileHistory;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Tests.Fakes;
using Terminal.Gui.Views;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.FileHistory;

public sealed class FileHistoryInputTests : IDisposable
{
    private ListView? _list;
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly List<AppEffect> _effects = [];

    public FileHistoryInputTests()
    {
        _host.EffectRaised += _effects.Add;
        _host.Dispatch(new OpenRepositoryCommand(Location()));
        _host.Dispatch(new ScreenResizedCommand(new ScreenExtent(100, 24), _host.Clock.GetUtcNow()));
        _effects.Clear();
    }

    public void Dispose() => _list?.Dispose();

    [Fact]
    public void GivenAFileUnderTheCursor_WhenTheOpenActionRuns_ThenTheHistoryOfThatFileIsRequestedFromItsRevision()
    {
        var opener = NewOpener(new FixedTarget(new FileHistoryTarget("dir/a b.txt", "abc")));

        var taken = opener.TryExecute(ActionIds.FileHistoryOpen, null);

        Assert.True(taken);
        var read = Assert.IsType<ReadFileHistoryEffect>(Assert.Single(_effects));
        Assert.Equal(("dir/a b.txt", "abc"), (read.Path, read.Revision));
        Assert.Equal(string.Empty, opener.Notice);
    }

    [Fact]
    public void GivenNoFileUnderTheCursor_WhenTheOpenActionRuns_ThenNothingStartsAndTheNoticeSaysWhy()
    {
        var opener = NewOpener(new FixedTarget(null));

        var taken = opener.TryExecute(ActionIds.FileHistoryOpen, null);

        Assert.True(taken);
        Assert.Empty(_effects);
        Assert.Equal(FileHistoryOpener.NoFileNotice, opener.Notice);
    }

    [Fact]
    public void GivenAnotherAction_WhenOfferedToTheOpener_ThenItIsNotTaken()
    {
        var opener = NewOpener(new FixedTarget(new FileHistoryTarget("a.txt", "HEAD")));

        Assert.False(opener.TryExecute(ActionIds.ListDown, null));
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenSelectedLines_WhenTheLineHistoryActionRuns_ThenTheRangeIsRequestedFromTheTargetRevision()
    {
        var lines = new FixedLines(new LineHistoryTarget("src/a.txt", "abc", new LineRange(4, 9), string.Empty));
        var opener = NewOpener(new FixedTarget(null), lines);

        var taken = opener.TryExecute(ActionIds.LineHistoryOpen, null);

        Assert.True(taken);
        var read = Assert.IsType<ReadFileHistoryEffect>(Assert.Single(_effects));
        Assert.Equal(("src/a.txt", "abc", new LineRange(4, 9)), (read.Path, read.Revision, read.Range!.Value));
    }

    [Fact]
    public void GivenNoUsableLines_WhenTheLineHistoryActionRuns_ThenNothingStartsAndTheNoticeSaysWhy()
    {
        var lines = new FixedLines(LineHistoryTarget.Refused(DiffLineRanges.UncommittedNotice));
        var opener = NewOpener(new FixedTarget(null), lines);

        var taken = opener.TryExecute(ActionIds.LineHistoryOpen, null);

        Assert.True(taken);
        Assert.Empty(_effects);
        Assert.Equal(DiffLineRanges.UncommittedNotice, opener.Notice);
    }

    [Fact]
    public void GivenAClosedHistory_WhenAnyActionIsOffered_ThenTheInputTakesNothingAndDrawsNothing()
    {
        var input = new FileHistoryInput(_host, GraphGlyphSet.Unicode);

        Assert.False(input.TryExecute(ActionIds.ListDown, null));
        Assert.False(input.IsOpen);
        Assert.Null(input.CurrentFrame());
    }

    [Fact]
    public void GivenAnOpenHistory_WhenActionsAreOffered_ThenTheyAreConsumedMoveTheHighlightAndEscCloses()
    {
        var input = Open(3);

        Assert.True(input.TryExecute(ActionIds.ListDown, null));
        Assert.Equal(1, _host.State.Shell.FileHistory.History!.SelectedIndex);
        Assert.NotNull(input.CurrentFrame());
        Assert.True(input.TryExecute(ActionIds.NavigateBack, null));
        Assert.False(input.IsOpen);
        Assert.Null(input.CurrentFrame());
    }

    [Fact]
    public void GivenAnOpenHistory_WhenClickingACommitRow_ThenItIsHighlightedAndNoActionIsReturned()
    {
        var input = Open(3);

        var view = input.OnLineClicked(3, out var handled);

        Assert.True(handled);
        Assert.Null(view);
        Assert.Equal(2, _host.State.Shell.FileHistory.History!.SelectedIndex);
    }

    [Fact]
    public void GivenAnOpenHistory_WhenClickingTheActionRow_ThenItsMouseViewIsReturnedAndTheHighlightStays()
    {
        var input = Open(3);
        var actionLine = FileHistoryFrameLines() - 3;

        var view = input.OnLineClicked(actionLine, out var handled);

        Assert.True(handled);
        Assert.Equal(DefaultMouseMap.FileHistorySelectButton, view);
        Assert.Equal(0, _host.State.Shell.FileHistory.History!.SelectedIndex);
    }

    [Fact]
    public void GivenAnOpenHistory_WhenClickingTheTitleOrTheFooter_ThenTheClickIsNotHandled()
    {
        var input = Open(3);

        input.OnLineClicked(0, out var onTitle);
        input.OnLineClicked(FileHistoryFrameLines() - 2, out var onFooter);

        Assert.False(onTitle);
        Assert.False(onFooter);
    }

    [Fact]
    public void GivenAnOpenHistory_WhenItsTabIsClosed_ThenTheActionPassesOnAndTheHistoryCloses()
    {
        var input = Open(2);
        var tab = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new CloseTabCommand(tab));

        var taken = input.TryExecute(ActionIds.ListDown, null);

        Assert.False(taken);
        Assert.False(input.IsOpen);
    }

    private int FileHistoryFrameLines() =>
        new FileHistoryInput(_host, GraphGlyphSet.Unicode).CurrentFrame()!.Lines.Count;

    private FileHistoryInput Open(int count)
    {
        var tab = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new FileHistoryOpenRequested(tab, "a.txt", "HEAD"));
        var read = Assert.IsType<ReadFileHistoryEffect>(_effects.Last());
        var entries = Enumerable.Range(0, count)
            .Select(index => new FileHistoryEntry(
                new string((char)('a' + index), 40),
                "Ann",
                "2026-11-02",
                $"commit {index}",
                "a.txt",
                null,
                FileChangeKind.Modified))
            .ToList();
        _host.Dispatch(new FileHistoryLoaded(read.TabId, read.Generation, read.Path, read.Revision, entries));

        return new FileHistoryInput(_host, GraphGlyphSet.Unicode);
    }

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-file-history-input");
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private FileHistoryOpener NewOpener(IFileHistoryTargetSource target, ILineHistorySource? lines = null)
    {
        _list = new ListView();
        var visual = new VisualRangeInput(_host, new FakePaneLists(_list));

        return new FileHistoryOpener(_host, target, lines ?? new FixedLines(LineHistoryTarget.Refused("none")), visual);
    }

    private sealed class FixedLines(LineHistoryTarget target) : ILineHistorySource
    {
        public LineHistoryTarget Resolve() => target;
    }

    private sealed class FixedTarget(FileHistoryTarget? target) : IFileHistoryTargetSource
    {
        public FileHistoryTarget? FileUnderCursor() => target;
    }
}
