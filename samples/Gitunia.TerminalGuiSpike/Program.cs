using System.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Gitunia.Tui;

public static class Program
{
    private static string _pending = "";
    private static bool _searching;
    private static string _query = "";
    private static ListView _active = null!;
    private static CancellationTokenSource? _detailCancellationTokenSource;

    public static int Main(string[] arguments)
    {
        if (arguments.Length < 1)
        {
            Console.Error.WriteLine("usage: gitunia <repo> [--ips N]");
            return 2;
        }

        var repositoryPath = Path.GetFullPath(arguments[0]);
        var graph = new GraphSource();
        var refs = new CommitSource();
        refs.AddRange(LoadRefs(repositoryPath));

        using IApplication app = Application.Create();
        var iterationsArgument = Array.IndexOf(arguments, "--ips");
        Application.MaximumIterationsPerSecond = iterationsArgument >= 0 ? ushort.Parse(arguments[iterationsArgument + 1]) : (ushort)100;

        app.Init();

        var repositoryName = Path.GetFileName(repositoryPath.TrimEnd(Path.DirectorySeparatorChar));
        var window = new Window { Title = "", BorderStyle = LineStyle.None, Width = Dim.Fill(), Height = Dim.Fill() };
        var header = new SegmentView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = 1, Fill = Look.Style(TextStyle.Bold | TextStyle.Reverse),
            Build = () => [(" ● Gitunia  ─  " + repositoryName, Look.Style(TextStyle.Bold | TextStyle.Reverse))],
        };
        var tabs = new SegmentView
        {
            X = 0, Y = 1, Width = Dim.Fill(), Height = 1,
            Build = () => [(" 1 " + repositoryName + " ", Look.Style(TextStyle.Reverse)), (" +  (tabs: M1)", Look.Style(TextStyle.Faint))],
        };

        // Pane borders: bold when focused, faint otherwise (same cue as the custom demo).
        var paneScheme = new Scheme
        {
            Normal = Look.Style(TextStyle.Faint),
            Focus = Look.Style(TextStyle.Bold),
            HotNormal = Look.Style(TextStyle.Faint),
            HotFocus = Look.Style(TextStyle.Bold),
        };

        FrameView Pane(string title) => new() { Title = title, BorderStyle = LineStyle.Single, X = 0, Y = 2 };

        var sideFrame = Pane("Refs");
        sideFrame.Width = 26;
        sideFrame.Height = Dim.Fill(1);
        var side = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true, Source = refs };
        sideFrame.Add(side);

        var graphFrame = Pane("Graph");
        graphFrame.X = 26;
        graphFrame.Width = Dim.Fill();
        graphFrame.Height = Dim.Percent(65);
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true, Source = graph };
        graphFrame.Add(list);

        var detailFrame = Pane("Commit");
        detailFrame.X = 26;
        detailFrame.Y = Pos.Bottom(graphFrame);
        detailFrame.Width = Dim.Fill();
        detailFrame.Height = Dim.Fill(1);
        var details = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Text = "" };
        detailFrame.Add(details);
        foreach (var frame in new[] { sideFrame, graphFrame, detailFrame })
        {
            frame.SetScheme(paneScheme);
        }

        var statusText = "";
        var status = new SegmentView
        {
            X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1, Fill = Look.Style(TextStyle.Faint | TextStyle.Reverse),
            Build = () => [(statusText, Look.Style(TextStyle.Faint | TextStyle.Reverse))],
        };
        foreach (var frame in new[] { sideFrame, graphFrame, detailFrame })
        {
            frame.Border!.Settings &= ~BorderSettings.Title;
        }

        Attribute TitleAppearance(bool focused) => Look.Style(focused ? TextStyle.Bold : TextStyle.Faint);
        var graphTitleText = " Graph ";
        SegmentView Title(View frame, Func<string> text, Func<bool> focused)
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

        var sideTitle = Title(sideFrame, () => " Refs ", () => side.HasFocus);
        var graphTitle = Title(graphFrame, () => graphTitleText, () => list.HasFocus);
        var detailTitle = new SegmentView { X = Pos.Left(detailFrame) + 2, Y = Pos.Top(detailFrame), Height = 1, Width = 8 };
        detailTitle.Build = () => [(" Commit ", TitleAppearance(false))];
        window.Add(header, tabs, sideFrame, graphFrame, detailFrame, status, sideTitle, graphTitle, detailTitle);
        _active = list;

        side.HasFocusChanged += (_, changed) =>
        {
            if (changed.NewValue)
            {
                _active = side;
                UpdateStatus();
                sideTitle.SetNeedsDraw();
                graphTitle.SetNeedsDraw();
            }
        };
        list.HasFocusChanged += (_, changed) =>
        {
            if (changed.NewValue)
            {
                _active = list;
                UpdateStatus();
                sideTitle.SetNeedsDraw();
                graphTitle.SetNeedsDraw();
            }
        };

        void UpdateStatus()
        {
            var count = _active.Source?.Count ?? 0;
            var mode = _searching ? "SEARCH" : "NORMAL";
            var position = count == 0 ? 0 : (_active.SelectedItem ?? 0) + 1;
            statusText = _searching
                ? " /" + _query
                : $" {mode}  [{(_active == list ? "Graph" : "Refs")}]  {position}/{count}  Tab j/k gg G ^d ^u / n N q";
            if (graph.Count > 0)
            {
                graphTitleText = $" Graph  {(list.SelectedItem ?? 0) + 1}/{graph.Count} ";
                graphTitle.SetNeedsDraw();
            }

            status.SetNeedsDraw();
        }

        void Select(int index)
        {
            var count = _active.Source?.Count ?? 0;
            if (count == 0)
            {
                return;
            }

            _active.SelectedItem = index;
            _active.EnsureSelectedItemVisible();
            UpdateStatus();
            if (_active == list)
            {
                LoadDetails(app, repositoryPath, graph, index, details);
            }
        }

        void Page(int direction)
        {
            var halfPage = Math.Max(1, _active.Viewport.Height / 2);
            Select(Math.Clamp((_active.SelectedItem ?? 0) + (direction * halfPage), 0, Math.Max(0, (_active.Source?.Count ?? 1) - 1)));
        }

        void FindNext()
        {
            var start = (_active.SelectedItem ?? -1) + 1;
            var count = _active.Source?.Count ?? 0;
            for (var offset = 0; offset < count; offset++)
            {
            var index = (start + offset) % count;
                var text = _active == list ? graph.Rows[index].Subject : refs.Items[index].Text;
                if (text.Contains(_query, StringComparison.OrdinalIgnoreCase))
                {
                    Select(index);
                    return;
                }
            }

            statusText = " no match: " + _query;
            status.SetNeedsDraw();
        }

        app.Keyboard.KeyDown += (_, key) =>
        {
            var typed = key.AsRune.ToString();
            if (_searching)
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

                key.Handled = true;
                return;
            }

            if (key == Key.Tab)
            {
                // Default focus navigation did not move between the two lists, so switch explicitly.
                (_active == list ? side : list).SetFocus();
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
                app.RequestStop();
            }
            else
            {
                return;
            }

            _pending = "";
            key.Handled = true;
        };

        list.SetFocus();
        _ = Task.Run(() => Stream(app, repositoryPath, graph, list, details, UpdateStatus));
        app.Run(window);
        window.Dispose();
        return 0;
    }

    private static List<SidebarItem> LoadRefs(string repositoryPath)
    {
        var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in new[] { "-C", repositoryPath, "for-each-ref", "--format=%(refname:short)\t%(refname)" })
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
            (parts.Length > 1 && parts[1].StartsWith("refs/tags/", StringComparison.Ordinal) ? tags : branches).Add(item);
        }

        process.WaitForExit();
        var result = new List<SidebarItem> { new($"Branches ({branches.Count})", true) };
        result.AddRange(branches);
        result.Add(new($"Tags ({tags.Count})", true));
        result.AddRange(tags);
        return result;
    }

    private static void LoadDetails(IApplication app, string repositoryPath, GraphSource graph, int index, Label details)
    {
        _detailCancellationTokenSource?.Cancel();
        var cancellationTokenSource = _detailCancellationTokenSource = new CancellationTokenSource();
        if (index >= graph.Rows.Count)
        {
            return;
        }

        var hash = graph.Rows[index].Hash;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(30, cancellationTokenSource.Token);
                var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
                foreach (var argument in new[] { "-C", repositoryPath, "show", "--stat", "--format=fuller", hash })
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo)!;
                using var registration = cancellationTokenSource.Token.Register(() =>
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                });
                var text = await process.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    app.Invoke(() => details.Text = text);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private static async Task Stream(IApplication app, string repositoryPath, GraphSource graph, ListView list, Label details, Action updateStatus)
    {
        var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in new[] { "-C", repositoryPath, "log", "--format=%H%x09%P%x09%s", "--date-order" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var lanes = new LaneBuilder();
        var batch = new List<GraphRow>(2048);
        var first = true;
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
        {
            var fields = line.Split('	', 3);
            var parents = fields[1].Length == 0 ? [] : fields[1].Split(' ');
            batch.Add(lanes.Add(fields[0], parents, fields.Length > 2 ? fields[2] : ""));
            if (batch.Count >= (first ? 200 : 5000))
            {
                first = false;
                Flush(app, repositoryPath, graph, list, details, batch, updateStatus);
                batch = new List<GraphRow>(5000);
            }
        }

        Flush(app, repositoryPath, graph, list, details, batch, updateStatus);
    }

    private static void Flush(IApplication app, string repositoryPath, GraphSource graph, ListView list, Label details, List<GraphRow> batch, Action updateStatus)
    {
        if (batch.Count == 0)
        {
            return;
        }

        app.Invoke(() =>
        {
            var wasEmpty = graph.Count == 0;
            graph.AddRange(batch);
            if (list.SelectedItem is null && graph.Count > 0)
            {
                list.SelectedItem = 0;
            }

            if (wasEmpty)
            {
                LoadDetails(app, repositoryPath, graph, 0, details);
            }

            list.SetNeedsDraw();
            updateStatus();
        });
    }
}
