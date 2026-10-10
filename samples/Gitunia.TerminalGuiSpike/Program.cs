using System.Diagnostics;
using Terminal.Gui.App;
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
    private static CancellationTokenSource? _detailCts;

    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: gitunia <repo> [--ips N]");
            return 2;
        }

        var repo = Path.GetFullPath(args[0]);
        var graph = new GraphSource();
        var refs = new CommitSource();
        refs.AddRange(LoadRefs(repo));

        using IApplication app = Application.Create();
        var ipsArg = Array.IndexOf(args, "--ips");
        if (ipsArg >= 0)
        {
            Application.MaximumIterationsPerSecond = ushort.Parse(args[ipsArg + 1]);
        }

        app.Init();

        var win = new Window { Title = "gitunia (Terminal.Gui)", Width = Dim.Fill(), Height = Dim.Fill() };
        var tabs = new Label { X = 0, Y = 0, Width = Dim.Fill(), Text = " 1:" + Path.GetFileName(repo.TrimEnd(Path.DirectorySeparatorChar)) + "   2:other-repo   3:another" };

        var sideFrame = new FrameView { Title = "Refs", X = 0, Y = 1, Width = 26, Height = Dim.Fill(1) };
        var side = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true, Source = refs };
        sideFrame.Add(side);

        var graphFrame = new FrameView { Title = "Graph", X = 26, Y = 1, Width = Dim.Fill(), Height = Dim.Percent(65) };
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true, Source = graph };
        graphFrame.Add(list);

        var detailFrame = new FrameView { Title = "Details", X = 26, Y = Pos.Bottom(graphFrame), Width = Dim.Fill(), Height = Dim.Fill(1) };
        var details = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Text = "" };
        detailFrame.Add(details);

        var status = new Label { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Text = "" };
        win.Add(tabs, sideFrame, graphFrame, detailFrame, status);
        _active = list;

        side.HasFocusChanged += (_, e) =>
        {
            if (e.NewValue)
            {
                _active = side;
            }
        };
        list.HasFocusChanged += (_, e) =>
        {
            if (e.NewValue)
            {
                _active = list;
            }
        };

        void UpdateStatus()
        {
            var n = _active.Source?.Count ?? 0;
            var mode = _searching ? "SEARCH" : "NORMAL";
            status.Text = _searching ? "/" + _query : $" {mode}  {(_active == list ? "graph" : "refs")}  {(_active.SelectedItem ?? 0) + 1}/{n}";
        }

        void Select(int i)
        {
            var n = _active.Source?.Count ?? 0;
            if (n == 0)
            {
                return;
            }

            _active.SelectedItem = i;
            _active.EnsureSelectedItemVisible();
            UpdateStatus();
            if (_active == list)
            {
                LoadDetails(app, repo, graph, i, details);
            }
        }

        void Page(int dir)
        {
            var h = Math.Max(1, _active.Viewport.Height / 2);
            Select(Math.Clamp((_active.SelectedItem ?? 0) + (dir * h), 0, Math.Max(0, (_active.Source?.Count ?? 1) - 1)));
        }

        void FindNext()
        {
            var start = (_active.SelectedItem ?? -1) + 1;
            var n = _active.Source?.Count ?? 0;
            for (var k = 0; k < n; k++)
            {
                var i = (start + k) % n;
                var text = _active == list ? graph.Rows[i].Subject : refs.Items[i];
                if (text.Contains(_query, StringComparison.OrdinalIgnoreCase))
                {
                    Select(i);
                    return;
                }
            }

            status.Text = "no match: " + _query;
        }

        app.Keyboard.KeyDown += (_, k) =>
        {
            var r = k.AsRune.ToString();
            if (_searching)
            {
                if (k == Key.Enter)
                {
                    _searching = false;
                    FindNext();
                }
                else if (k == Key.Esc)
                {
                    _searching = false;
                    UpdateStatus();
                }
                else if (k == Key.Backspace && _query.Length > 0)
                {
                    _query = _query[..^1];
                    UpdateStatus();
                }
                else if (k.AsRune.Value >= 32)
                {
                    _query += r;
                    UpdateStatus();
                }

                k.Handled = true;
                return;
            }

            if (k == Key.Tab)
            {
                return; // default focus navigation moves between sidebar and graph
            }

            if (k == Key.D.WithCtrl)
            {
                Page(1);
            }
            else if (k == Key.U.WithCtrl)
            {
                Page(-1);
            }
            else if (r == "G")
            {
                Select((_active.Source?.Count ?? 1) - 1);
            }
            else if (r == "g")
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

                k.Handled = true;
                return;
            }
            else if (r == "j")
            {
                Select(Math.Min((_active.Source?.Count ?? 1) - 1, (_active.SelectedItem ?? 0) + 1));
            }
            else if (r == "k")
            {
                Select(Math.Max(0, (_active.SelectedItem ?? 0) - 1));
            }
            else if (r == "/")
            {
                _searching = true;
                _query = "";
                UpdateStatus();
            }
            else if (r == "n")
            {
                FindNext();
            }
            else if (r == "q")
            {
                app.RequestStop();
            }
            else
            {
                return;
            }

            _pending = "";
            k.Handled = true;
        };

        list.SetFocus();
        _ = Task.Run(() => Stream(app, repo, graph, list, details, UpdateStatus));
        app.Run(win);
        win.Dispose();
        return 0;
    }

    private static List<string> LoadRefs(string repo)
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var a in new[] { "-C", repo, "for-each-ref", "--format=%(refname:short)\t%(refname)" })
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        var res = new List<string>();
        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            var parts = line.Split('\t');
            res.Add((parts.Length > 1 && parts[1].StartsWith("refs/tags/", StringComparison.Ordinal) ? "◆ " : "● ") + parts[0]);
        }

        p.WaitForExit();
        return res;
    }

    private static void LoadDetails(IApplication app, string repo, GraphSource graph, int i, Label details)
    {
        _detailCts?.Cancel();
        var cts = _detailCts = new CancellationTokenSource();
        if (i >= graph.Rows.Count)
        {
            return;
        }

        var hash = graph.Rows[i].Hash;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(30, cts.Token);
                var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
                foreach (var a in new[] { "-C", repo, "show", "--stat", "--format=%H%n%an <%ae>%n%ad%n%n%B", hash })
                {
                    psi.ArgumentList.Add(a);
                }

                using var p = Process.Start(psi)!;
                using var reg = cts.Token.Register(() =>
                {
                    try
                    {
                        p.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                });
                var text = await p.StandardOutput.ReadToEndAsync(cts.Token);
                if (!cts.IsCancellationRequested)
                {
                    app.Invoke(() => details.Text = text);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private static async Task Stream(IApplication app, string repo, GraphSource graph, ListView list, Label details, Action updateStatus)
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var a in new[] { "-C", repo, "log", "--format=%H%x09%P%x09%s", "--date-order" })
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        var lanes = new LaneBuilder();
        var batch = new List<GraphRow>(2048);
        var first = true;
        string? line;
        while ((line = await p.StandardOutput.ReadLineAsync()) is not null)
        {
            var f = line.Split('\t', 3);
            var parents = f[1].Length == 0 ? [] : f[1].Split(' ');
            batch.Add(lanes.Add(f[0], parents, f.Length > 2 ? f[2] : ""));
            if (batch.Count >= (first ? 200 : 5000))
            {
                first = false;
                Flush(app, repo, graph, list, details, batch, updateStatus);
                batch = new List<GraphRow>(5000);
            }
        }

        Flush(app, repo, graph, list, details, batch, updateStatus);
    }

    private static void Flush(IApplication app, string repo, GraphSource graph, ListView list, Label details, List<GraphRow> batch, Action updateStatus)
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
                LoadDetails(app, repo, graph, 0, details);
            }

            list.SetNeedsDraw();
            updateStatus();
        });
    }
}
