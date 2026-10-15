using System.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Gitunia.Tui;

public static class Program
{
    private static string _pending = "";
    private static bool _searching;
    private static string _query = "";
    private static readonly GraphSource _graph = new();
    private static readonly CommitSource _refs = new();
    private static string _repositoryPath = "";
    private static string _statusText = "";
    private static string _graphTitleText = " Graph ";
    private static IApplication _app = null!;
    private static ListView _active = null!;
    private static ListView _list = null!;
    private static ListView _side = null!;
    private static Label _details = null!;
    private static SegmentView _status = null!;
    private static SegmentView _sideTitle = null!;
    private static SegmentView _graphTitle = null!;
    private static CancellationTokenSource? _detailCancellationTokenSource;

    public static int Main(string[] arguments)
    {
        if (arguments.Length < 1)
        {
            Console.Error.WriteLine("usage: gitunia <repo> [--ips N]");
            return 2;
        }

        _repositoryPath = Path.GetFullPath(arguments[0]);
        _refs.AddRange(LoadRefs(_repositoryPath));

        using var app = Application.Create();
        _app = app;
        var iterationsArgument = Array.IndexOf(arguments, "--ips");
        Application.MaximumIterationsPerSecond =
            iterationsArgument >= 0 ? ushort.Parse(arguments[iterationsArgument + 1]) : (ushort)100;

        app.Init();

        var repositoryName = Path.GetFileName(_repositoryPath.TrimEnd(Path.DirectorySeparatorChar));
        var window = new Window { Title = "", BorderStyle = LineStyle.None, Width = Dim.Fill(), Height = Dim.Fill() };
        var header = new SegmentView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = 1, Fill = Look.Style(TextStyle.Bold | TextStyle.Reverse),
            Build = () => [($" ● Gitunia  ─  {repositoryName}", Look.Style(TextStyle.Bold | TextStyle.Reverse))],
        };
        var tabs = new SegmentView
        {
            X = 0, Y = 1, Width = Dim.Fill(), Height = 1,
            Build = () =>
            [
                ($" 1 {repositoryName} ", Look.Style(TextStyle.Reverse)),
                (" +  (tabs: M1)", Look.Style(TextStyle.Faint))
            ],
        };

        // Pane borders: bold when focused, faint otherwise (same cue as the custom demo).
        var paneScheme = new Scheme
        {
            Normal = Look.Style(TextStyle.Faint),
            Focus = Look.Style(TextStyle.Bold),
            HotNormal = Look.Style(TextStyle.Faint),
            HotFocus = Look.Style(TextStyle.Bold),
        };

        var sideFrame = CreatePane("Refs");
        sideFrame.Width = 26;
        sideFrame.Height = Dim.Fill(1);
        _side = new ListView
            { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true, Source = _refs };
        sideFrame.Add(_side);

        var graphFrame = CreatePane("Graph");
        graphFrame.X = 26;
        graphFrame.Width = Dim.Fill();
        graphFrame.Height = Dim.Percent(65);
        _list = new ListView
            { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true, Source = _graph };
        graphFrame.Add(_list);

        var detailFrame = CreatePane("Commit");
        detailFrame.X = 26;
        detailFrame.Y = Pos.Bottom(graphFrame);
        detailFrame.Width = Dim.Fill();
        detailFrame.Height = Dim.Fill(1);
        _details = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Text = "" };
        detailFrame.Add(_details);

        foreach (var frame in new[] { sideFrame, graphFrame, detailFrame })
        {
            frame.SetScheme(paneScheme);
            frame.Border.Settings &= ~BorderSettings.Title;
        }

        _status = new SegmentView
        {
            X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1,
            Fill = Look.Style(TextStyle.Faint | TextStyle.Reverse),
            Build = () => [(_statusText, Look.Style(TextStyle.Faint | TextStyle.Reverse))],
        };

        _sideTitle = CreateTitle(sideFrame, () => " Refs ", () => _side.HasFocus);
        _graphTitle = CreateTitle(graphFrame, () => _graphTitleText, () => _list.HasFocus);
        var detailTitle = new SegmentView
            { X = Pos.Left(detailFrame) + 2, Y = Pos.Top(detailFrame), Height = 1, Width = 8 };
        detailTitle.Build = () => [(" Commit ", TitleAppearance(false))];
        window.Add(header, tabs, sideFrame, graphFrame, detailFrame, _status, _sideTitle, _graphTitle, detailTitle);
        _active = _list;

        _side.HasFocusChanged += (_, changed) => OnListFocused(changed.NewValue, _side);
        _list.HasFocusChanged += (_, changed) => OnListFocused(changed.NewValue, _list);
        app.Keyboard.KeyDown += (_, key) => OnKeyDown(key);

        _list.SetFocus();
        _ = Task.Run(Stream);
        app.Run(window);
        window.Dispose();
        return 0;
    }

    private static FrameView CreatePane(string title) =>
        new() { Title = title, BorderStyle = LineStyle.Single, X = 0, Y = 2 };

    private static Attribute TitleAppearance(bool focused) =>
        Look.Style(focused ? TextStyle.Bold : TextStyle.Faint);

    private static SegmentView CreateTitle(View frame, Func<string> text, Func<bool> focused)
    {
        var view = new SegmentView { X = Pos.Left(frame) + 2, Y = 2, Height = 1, Width = 1 };
        view.Build = () =>
        {
            var titleText = text();
            view.Width = titleText.Length;
            return [(titleText, TitleAppearance(focused()))];
        };

        return view;
    }

    private static void OnListFocused(bool hasFocus, ListView focused)
    {
        if (!hasFocus)
        {
            return;
        }

        _active = focused;
        UpdateStatus();
        _sideTitle.SetNeedsDraw();
        _graphTitle.SetNeedsDraw();
    }

    private static void UpdateStatus()
    {
        var count = _active.Source?.Count ?? 0;
        var mode = _searching ? "SEARCH" : "NORMAL";
        var position = count == 0 ? 0 : (_active.SelectedItem ?? 0) + 1;
        _statusText = _searching
            ? $" /{_query}"
            : $" {mode}  [{(_active == _list ? "Graph" : "Refs")}]  {position}/{count}  Tab j/k gg G ^d ^u / n N q";
        if (_graph.Count > 0)
        {
            _graphTitleText = $" Graph  {(_list.SelectedItem ?? 0) + 1}/{_graph.Count} ";
            _graphTitle.SetNeedsDraw();
        }

        _status.SetNeedsDraw();
    }

    private static void Select(int index)
    {
        var count = _active.Source?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        _active.SelectedItem = index;
        _active.EnsureSelectedItemVisible();
        UpdateStatus();
        if (_active == _list)
        {
            LoadDetails(index);
        }
    }

    private static void Page(int direction)
    {
        var halfPage = Math.Max(1, _active.Viewport.Height / 2);
        Select(Math.Clamp((_active.SelectedItem ?? 0) + (direction * halfPage), 0,
            Math.Max(0, (_active.Source?.Count ?? 1) - 1)));
    }

    private static void FindNext()
    {
        var start = (_active.SelectedItem ?? -1) + 1;
        var count = _active.Source?.Count ?? 0;
        for (var offset = 0; offset < count; offset++)
        {
            var index = (start + offset) % count;
            var text = _active == _list ? _graph.Rows[index].Subject : _refs.Items[index].Text;
            if (text.Contains(_query, StringComparison.OrdinalIgnoreCase))
            {
                Select(index);
                return;
            }
        }

        _statusText = $" no match: {_query}";
        _status.SetNeedsDraw();
    }

    private static void OnKeyDown(Key key)
    {
        var typed = key.AsRune.ToString();
        if (_searching)
        {
            HandleSearchKey(key, typed);
            key.Handled = true;
            return;
        }

        if (key == Key.Tab)
        {
            // Default focus navigation did not move between the two lists, so switch explicitly.
            (_active == _list ? _side : _list).SetFocus();
            key.Handled = true;
            return;
        }

        if (key == Key.D.WithCtrl)
        {
            Page(1);
        }
        else if (key == Key.U.WithCtrl)
        {
            Page(-1);
        }
        else if (typed == "G")
        {
            Select((_active.Source?.Count ?? 1) - 1);
        }
        else if (typed == "g")
        {
            if (_pending == "g")
            {
                _pending = "";
                Select(0);
            }
            else
            {
                _pending = "g";
            }

            key.Handled = true;
            return;
        }
        else if (typed == "j")
        {
            Select(Math.Min((_active.Source?.Count ?? 1) - 1, (_active.SelectedItem ?? 0) + 1));
        }
        else if (typed == "k")
        {
            Select(Math.Max(0, (_active.SelectedItem ?? 0) - 1));
        }
        else if (typed == "/")
        {
            _searching = true;
            _query = "";
            UpdateStatus();
        }
        else if (typed == "n")
        {
            FindNext();
        }
        else if (typed == "q")
        {
            _app.RequestStop();
        }
        else
        {
            return;
        }

        _pending = "";
        key.Handled = true;
    }

    private static void HandleSearchKey(Key key, string typed)
    {
        if (key == Key.Enter)
        {
            _searching = false;
            FindNext();
        }
        else if (key == Key.Esc)
        {
            _searching = false;
            UpdateStatus();
        }
        else if (key == Key.Backspace && _query.Length > 0)
        {
            _query = _query[..^1];
            UpdateStatus();
        }
        else if (key.AsRune.Value >= 32)
        {
            _query += typed;
            UpdateStatus();
        }
    }

    private static List<SidebarItem> LoadRefs(string repositoryPath)
    {
        var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in new[]
                     { "-C", repositoryPath, "for-each-ref", "--format=%(refname:short)\t%(refname)" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var branches = new List<SidebarItem>();
        var tags = new List<SidebarItem>();
        string? line;
        while ((line = process.StandardOutput.ReadLine()) is not null)
        {
            var parts = line.Split('\t');
            var item = new SidebarItem(parts[0], false);
            (parts.Length > 1 && parts[1].StartsWith("refs/tags/", StringComparison.Ordinal) ? tags : branches)
                .Add(item);
        }

        process.WaitForExit();
        var result = new List<SidebarItem> { new($"Branches ({branches.Count})", true) };
        result.AddRange(branches);
        result.Add(new($"Tags ({tags.Count})", true));
        result.AddRange(tags);
        return result;
    }

    private static void LoadDetails(int index)
    {
        _detailCancellationTokenSource?.Cancel();
        var cancellationTokenSource = _detailCancellationTokenSource = new CancellationTokenSource();
        if (index >= _graph.Rows.Count)
        {
            return;
        }

        var hash = _graph.Rows[index].Hash;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(30, cancellationTokenSource.Token);
                var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
                foreach (var argument in new[] { "-C", _repositoryPath, "show", "--stat", "--format=fuller", hash })
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo)!;
                await using var registration = cancellationTokenSource.Token.Register(
                    ProcessKiller.KillQuietly,
                    process);

                var text = await process.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    _app.Invoke(() => _details.Text = text);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private static async Task Stream()
    {
        var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in new[] { "-C", _repositoryPath, "log", "--format=%H%x09%P%x09%s", "--date-order" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var lanes = new LaneBuilder();
        var batch = new List<GraphRow>(2048);
        var first = true;

        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            var fields = line.Split('	', 3);
            var parents = fields[1].Length == 0 ? [] : fields[1].Split(' ');
            batch.Add(lanes.Add(fields[0], parents, fields.Length > 2 ? fields[2] : ""));
            if (batch.Count >= (first ? 200 : 5000))
            {
                first = false;
                Flush(batch);
                batch = new List<GraphRow>(5000);
            }
        }

        Flush(batch);
    }

    private static void Flush(List<GraphRow> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        _app.Invoke(() =>
        {
            var wasEmpty = _graph.Count == 0;
            _graph.AddRange(batch);
            if (_list.SelectedItem is null && _graph.Count > 0)
            {
                _list.SelectedItem = 0;
            }

            if (wasEmpty)
            {
                LoadDetails(0);
            }

            _list.SetNeedsDraw();
            UpdateStatus();
        });
    }
}
