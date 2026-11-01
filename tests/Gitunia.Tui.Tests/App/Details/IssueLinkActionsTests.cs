using System.Text;
using Gitunia.App.Loading.Commits;
using Gitunia.Core.Clipboard;
using Gitunia.Core.IssueTracker;
using Gitunia.Core.Keymap;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.IssueTracker.Models;
using Gitunia.Platform.Urls;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Tests.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Xunit;

namespace Gitunia.Tui.Tests.App.Details;

public class IssueLinkActionsTests : IDisposable
{
    private const string Message = "Subject\n\nSee #1 and #2\nplain line";

    private readonly StringWriter _terminal = new();
    private readonly StatusChannel _statuses = new();
    private readonly FakeUrlOpener _opener = new();
    private readonly Queue<Action> _posted = new();
    private readonly TestSignal _arrived = new();
    private bool _focused = true;

    [Fact]
    public void GivenALineWithTwoLinks_WhenOpeningWithoutACount_ThenTheFirstOpens()
    {
        var actions = CreateActions(LineOf("See #1 and #2"));

        Assert.True(actions.TryExecute(ActionIds.DetailOpenLink, null));
        Settle();

        Assert.Equal(["https://example.org/1"], _opener.Opened);
    }

    [Fact]
    public void GivenALineWithTwoLinks_WhenOpeningWithACount_ThenThatLinkOpens()
    {
        var actions = CreateActions(LineOf("See #1 and #2"));

        actions.TryExecute(ActionIds.DetailOpenLink, 2);
        Settle();

        Assert.Equal(["https://example.org/2"], _opener.Opened);
    }

    [Fact]
    public void GivenACountPastTheLinks_WhenOpening_ThenNothingOpensAndTheStatusSaysSo()
    {
        var actions = CreateActions(LineOf("See #1 and #2"));

        actions.TryExecute(ActionIds.DetailOpenLink, 3);

        Assert.Empty(_opener.Opened);
        Assert.Contains("no link number 3", _statuses.Current?.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALineWithoutLinks_WhenOpening_ThenTheStatusSaysSoAndTheKeyIsHandled()
    {
        var actions = CreateActions(LineOf("plain line"));

        Assert.True(actions.TryExecute(ActionIds.DetailOpenLink, null));

        Assert.Empty(_opener.Opened);
        Assert.Equal("No issue link on this line", _statuses.Current?.Text);
    }

    [Fact]
    public void GivenALineWithoutLinks_WhenPressingEnter_ThenTheActionIsLeftToTheOthers()
    {
        var actions = CreateActions(LineOf("plain line"));

        Assert.False(actions.TryExecute(ActionIds.DetailOpenDiff, null));
    }

    [Fact]
    public void GivenALineWithALink_WhenPressingEnter_ThenTheLinkOpens()
    {
        var actions = CreateActions(LineOf("See #1 and #2"));

        Assert.True(actions.TryExecute(ActionIds.DetailOpenDiff, null));
        Settle();

        Assert.Equal(["https://example.org/1"], _opener.Opened);
    }

    [Fact]
    public void GivenTheDetailPaneIsNotFocusedOnTheMessage_WhenOpening_ThenNothingHappens()
    {
        _focused = false;
        var actions = CreateActions(LineOf("See #1 and #2"));

        Assert.False(actions.TryExecute(ActionIds.DetailOpenLink, null));
        Assert.Empty(_opener.Opened);
    }

    [Fact]
    public void GivenTheOpenerFails_WhenOpening_ThenTheAddressIsCopiedAndTheStatusSaysSo()
    {
        _opener.Result = new UrlOpenResult(false, "could not start xdg-open");
        var actions = CreateActions(LineOf("See #1 and #2"));

        actions.TryExecute(ActionIds.DetailOpenLink, null);
        Settle();

        var expected = $"\u001b]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes("https://example.org/1"))}\u001b\\";
        Assert.Equal(expected, _terminal.ToString());
        Assert.Contains("could not start xdg-open", _statuses.Current?.Text, StringComparison.Ordinal);
        Assert.Contains("copied https://example.org/1", _statuses.Current?.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenASlowOpener_WhenOpening_ThenTheKeyReturnsBeforeTheOpenerFinishes()
    {
        using var release = new ManualResetEventSlim();
        _opener.Gate = release;
        var actions = CreateActions(LineOf("See #1 and #2"));

        Assert.True(actions.TryExecute(ActionIds.DetailOpenLink, null));

        Assert.Empty(_opener.Opened);
        Assert.Equal("Opening https://example.org/1", _statuses.Current?.Text);
        release.Set();
        Settle();
        Assert.Equal(["https://example.org/1"], _opener.Opened);
        Assert.Equal("Opened https://example.org/1", _statuses.Current?.Text);
    }

    public void Dispose()
    {
        _terminal.Dispose();
        _arrived.Dispose();
        GC.SuppressFinalize(this);
    }

    private static DetailListSource Build()
    {
        var commit = CommitFactory.Commit(2, "Subject", [1], []);
        var entry = CommitFactory.Graph(commit, CommitFactory.Commit(1, "root", []))[0];
        var when = new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero);
        var details = new CommitDetails(
            CommitFactory.Id(2),
            [CommitFactory.Id(1)],
            "Ann",
            "ann@x.org",
            when,
            "Ann",
            "ann@x.org",
            when,
            Message,
            []);
        var links = new IssueTrackerLinks([new IssueTrackerRule("x", "#(\\d+)", "https://example.org/$1")]);
        var source = new DetailListSource(GraphGlyphSet.Unicode);
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty);
        source.Update(DetailContentBuilder.Build(entry, state, BottomTab.Details, null, FilePathStyle.FullPath, links));

        return source;
    }

    private static int RowOf(DetailListSource source, string text) =>
        source.Content.Rows
            .Select(row => string.Concat(row.Segments.Select(segment => segment.Text)).Trim())
            .ToList()
            .IndexOf(text);

    private static Line LineOf(string text)
    {
        var source = Build();

        return new Line(source, RowOf(source, text));
    }

    private IssueLinkActions CreateActions(Line line) => new(
        _opener,
        new ClipboardCopier(new ClipboardWriter(_terminal, new Osc52Options(Osc52Mode.Direct, 100_000)), _statuses),
        _statuses,
        () => line.Source,
        () => line.Row,
        () => _focused,
        action =>
        {
            _posted.Enqueue(action);
            _arrived.Release();
        });

    private void Settle()
    {
        Assert.True(_arrived.Wait(TimeSpan.FromSeconds(30)));
        while (_posted.TryDequeue(out var action))
        {
            action();
        }
    }

    private sealed record Line(DetailListSource Source, int Row);

    private sealed class FakeUrlOpener : IUrlOpener
    {
        public List<string> Opened { get; } = [];

        public UrlOpenResult Result { get; set; } = new(true);

        public ManualResetEventSlim? Gate { get; set; }

        public bool IsSupported => true;

        public UrlOpenResult Open(string url)
        {
            Gate?.Wait(TimeSpan.FromSeconds(10));
            if (Result.Opened)
            {
                lock (Opened)
                {
                    Opened.Add(url);
                }
            }

            return Result;
        }
    }
}
