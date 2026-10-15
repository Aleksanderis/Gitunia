using System.Threading.Channels;
using Gitunia.CustomRenderTui.Demo.Graph;
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
    private readonly string _repositoryPath;
    private readonly Channel<object> _events = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CommitGraph _graph = new();
    private readonly CellBuffer _buffer = new();
    private readonly InputParser _parser = new();
    private readonly ListCursor _graphCursor = new();
    private readonly ListCursor _sidebarCursor = new();
    private readonly ListCursor _detailCursor = new();
    private List<SidebarItem> _sidebar = [];
    private List<string> _details = [];
    private CancellationTokenSource? _detailCancellationTokenSource;
    private string _detailHash = string.Empty;
    private Pane _focus = Pane.Graph;
    private bool _loading = true;
    private bool _gotoEndWhenDone;
    private bool _pendingG;
    private bool _searching;
    private string _query = string.Empty;
    private string _input = string.Empty;
    private bool _quit;

    public App(string repositoryPath) => _repositoryPath = repositoryPath;

    public void Dispose() => _detailCancellationTokenSource?.Dispose();

    private sealed record Resize;

    private sealed record Refs(List<SidebarItem> Items);

    private sealed record DetailsLoaded(string Hash, string Text);

    public int Run()
    {
        using var terminal = new RawTerminal();
        terminal.Enter();
        using var cancellationTokenSource = new CancellationTokenSource();
        terminal.Resized += () => _events.Writer.TryWrite(new Resize());

        var (width, height) = RawTerminal.GetSize();
        _buffer.Resize(width, height);
        _ = GitLogRunner.RunAsync(
            _repositoryPath,
            chunk => _events.Writer.TryWrite(chunk),
            () => _events.Writer.TryWrite("done"),
            cancellationTokenSource.Token);
        _ = LoadRefsAsync(cancellationTokenSource.Token);

        var reader = new Thread(() => ReadInput(terminal)) { IsBackground = true };
        reader.Start();

        var reading = _events.Reader;
        while (!_quit)
        {
            var dirty = false;
            if (!reading.WaitToReadAsync(cancellationTokenSource.Token).AsTask().GetAwaiter().GetResult())
            {
                break;
            }

            while (reading.TryRead(out var appEvent))
            {
                dirty |= Handle(appEvent);
            }

            if (dirty && _graph.Count > 0)
            {
                Draw();
            }
        }

        cancellationTokenSource.Cancel();
        _detailCancellationTokenSource?.Cancel();
        return 0;
    }

    /// <summary>Blocking read loop on its own thread: bytes in, parsed key events posted to the UI thread.</summary>
    private void ReadInput(RawTerminal terminal)
    {
        var buffer = new byte[1024];
        var keys = new List<KeyEvent>();
        int bytesRead;
        while ((bytesRead = terminal.Read(buffer)) > 0)
        {
            keys.Clear();
            _parser.Feed(buffer.AsSpan(0, bytesRead), keys);
            foreach (var key in keys)
            {
                _events.Writer.TryWrite(key);
            }
        }
    }

    private bool Handle(object appEvent)
    {
        switch (appEvent)
        {
            case byte[] chunk:
                _graph.Append(chunk);
                RequestDetails();
                return true;
            case string:
                _loading = false;
                if (_gotoEndWhenDone)
                {
                    _graphCursor.ToEnd(_graph.Count);
                    RequestDetails();
                }

                return true;
            case Refs refs:
                _sidebar = refs.Items;
                return true;
            case DetailsLoaded loaded when loaded.Hash == _detailHash:
                _details = [.. loaded.Text.Split('\n')];
                _detailCursor.ToStart();
                return true;
            case Resize:
                var (width, height) = RawTerminal.GetSize();
                _buffer.Resize(width, height);
                return true;
            case KeyEvent key:
                var before = _graphCursor.Selection;
                var dirty = OnKey(key);
                if (_graphCursor.Selection != before)
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
            ? $"/{_input}"
            : $" NORMAL  [{_focus}]  {_graphCursor.Selection + 1}/{_graph.Count}{(_loading ? " loading..." : string.Empty)}  Tab j/k gg G ^d ^u / n N q{(_query.Length > 0 ? $"  [/{_query}]" : string.Empty)}";
        Screen.Draw(_buffer, new ScreenModel
        {
            RepoName = Path.GetFileName(_repositoryPath.TrimEnd(Path.DirectorySeparatorChar)),
            Focus = _focus,
            Graph = _graph,
            GraphCursor = _graphCursor,
            Sidebar = _sidebar,
            SidebarCursor = _sidebarCursor,
            Details = _details,
            DetailsCursor = _detailCursor,
            Loading = _loading,
            Status = status,
        });
        var bytes = _buffer.Flush();
        if (bytes.Length > 0)
        {
            RawTerminal.Write(bytes);
        }
    }
}
