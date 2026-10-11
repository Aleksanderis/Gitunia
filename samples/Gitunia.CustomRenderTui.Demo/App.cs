using System.Threading.Channels;
using Gitunia.CustomRenderTui.Demo.Graph;
using Gitunia.CustomRenderTui;
using Gitunia.CustomRenderTui.Demo.Views;

namespace Gitunia.CustomRenderTui.Demo;

/// <summary>
/// Demo app: an M1-like screen (sidebar, lane graph, async commit details) over a streamed commit log.
/// Everything that changes state runs on one UI thread that drains <c>_events</c>; background work (loading,
/// input reading, resize) only posts events. This file holds the loop, event handling and drawing;
/// <c>App.Loading.cs</c> the background loaders and <c>App.Keys.cs</c> the vim-style key handling.
/// </summary>
public sealed partial class App : IDisposable
{
    private readonly string _repo;
    private readonly Channel<object> _events = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CommitGraph _graph = new();
    private readonly CellBuffer _buf = new();
    private readonly InputParser _parser = new();
    private readonly ListCursor _graphCur = new();
    private readonly ListCursor _sideCur = new();
    private readonly ListCursor _detailCur = new();
    private List<SidebarItem> _side = [];
    private List<string> _details = [];
    private CancellationTokenSource? _detailCts;
    private string _detailHash = string.Empty;
    private Pane _focus = Pane.Graph;
    private bool _loading = true;
    private bool _gotoEndWhenDone;
    private bool _pendingG;
    private bool _searching;
    private string _query = string.Empty;
    private string _input = string.Empty;
    private bool _quit;

    public App(string repo) => _repo = repo;

    public void Dispose() => _detailCts?.Dispose();

    private sealed record Resize;

    private sealed record Refs(List<SidebarItem> Items);

    private sealed record DetailsLoaded(string Hash, string Text);

    public int Run()
    {
        using var term = new RawTerminal();
        term.Enter();
        using var cts = new CancellationTokenSource();
        term.Resized += () => _events.Writer.TryWrite(new Resize());

        (var w, var h) = RawTerminal.GetSize();
        _buf.Resize(w, h);
        _ = GitLogRunner.RunAsync(
            _repo,
            chunk => _events.Writer.TryWrite(chunk),
            () => _events.Writer.TryWrite("done"),
            cts.Token);
        _ = LoadRefsAsync(cts.Token);
        var reader = new Thread(() => ReadInput(term)) { IsBackground = true };
        reader.Start();

        var reading = _events.Reader;
        while (!_quit)
        {
            var dirty = false;
            if (!reading.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                break;
            }

            while (reading.TryRead(out var ev))
            {
                dirty |= Handle(ev);
            }

            if (dirty && _graph.Count > 0)
            {
                Draw();
            }
        }

        cts.Cancel();
        _detailCts?.Cancel();
        return 0;
    }

    /// <summary>Blocking read loop on its own thread: bytes in, parsed key events posted to the UI thread.</summary>
    private void ReadInput(RawTerminal term)
    {
        var buf = new byte[1024];
        var keys = new List<KeyEvent>();
        int n;
        while ((n = term.Read(buf)) > 0)
        {
            keys.Clear();
            _parser.Feed(buf.AsSpan(0, n), keys);
            foreach (var k in keys)
            {
                _events.Writer.TryWrite(k);
            }
        }
    }

    private bool Handle(object ev)
    {
        switch (ev)
        {
            case byte[] chunk:
                _graph.Append(chunk);
                RequestDetails();
                return true;
            case string:
                _loading = false;
                if (_gotoEndWhenDone)
                {
                    _graphCur.ToEnd(_graph.Count);
                    RequestDetails();
                }

                return true;
            case Refs refs:
                _side = refs.Items;
                return true;
            case DetailsLoaded d when d.Hash == _detailHash:
                _details = [.. d.Text.Split('\n')];
                _detailCur.ToStart();
                return true;
            case Resize:
                (var w, var h) = RawTerminal.GetSize();
                _buf.Resize(w, h);
                return true;
            case KeyEvent k:
                var before = _graphCur.Selection;
                var dirty = OnKey(k);
                if (_graphCur.Selection != before)
                {
                    RequestDetails();
                }

                return dirty;
            default:
                return false;
        }
    }

    private void Draw()
    {
        var status = _searching
            ? "/" + _input
            : $" NORMAL  [{_focus}]  {_graphCur.Selection + 1}/{_graph.Count}{(_loading ? " loading..." : string.Empty)}  Tab j/k gg G ^d ^u / n N q" + (_query.Length > 0 ? $"  [/{_query}]" : string.Empty);
        Screen.Draw(_buf, new ScreenModel
        {
            RepoName = Path.GetFileName(_repo.TrimEnd(Path.DirectorySeparatorChar)),
            Focus = _focus,
            Graph = _graph,
            GraphCursor = _graphCur,
            Sidebar = _side,
            SidebarCursor = _sideCur,
            Details = _details,
            DetailsCursor = _detailCur,
            Loading = _loading,
            Status = status,
        });
        var bytes = _buf.Flush();
        if (bytes.Length > 0)
        {
            RawTerminal.Write(bytes);
        }
    }
}
