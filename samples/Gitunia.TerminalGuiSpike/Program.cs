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
        Application.MaximumIterationsPerSecond = ipsArg >= 0 ? ushort.Parse(args[ipsArg + 1]) : (ushort)100;

        app.Init();

        var repoName = Path.GetFileName(repo.TrimEnd(Path.DirectorySeparatorChar));
        var win = new Window { Title = "", BorderStyle = LineStyle.None, Width = Dim.Fill(), Height = Dim.Fill() };
        var header = new SegView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = 1, Fill = Look.Style(TextStyle.Bold | TextStyle.Reverse),
            Build = () => [(" ● Gitunia  ─  " + repoName, Look.Style(TextStyle.Bold | TextStyle.Reverse))],
        };
        var tabs = new SegView
        {
            X = 0, Y = 1, Width = Dim.Fill(), Height = 1,
            Build = () => [(" 1 " + repoName + " ", Look.Style(TextStyle.Reverse)), (" +  (tabs: M1)", Look.Style(TextStyle.Faint))],
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
        foreach (var f in new[] { sideFrame, graphFrame, detailFrame })
        {
            f.SetScheme(paneScheme);
        }

        var statusText = "";
        var status = new SegView
        {
            X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1, Fill = Look.Style(TextStyle.Faint | TextStyle.Reverse),
            Build = () => [(statusText, Look.Style(TextStyle.Faint | TextStyle.Reverse))],
        };
        foreach (var f in new[] { sideFrame, graphFrame, detailFrame })
        {
            f.Border!.Settings &= ~BorderSettings.Title;
        }

        Attribute TitleAttr(bool focused) => Look.Style(focused ? TextStyle.Bold : TextStyle.Faint);
        var graphTitleText = " Graph ";
        SegView Title(View frame, Func<string> text, Func<bool> focused)
        {
            var v = new SegView { X = Pos.Left(frame) + 2, Y = 2, Height = 1, Width = 1 };
            v.Build = () =>
            {
                var t = text();
                v.Width = t.Length;
                return [(t, TitleAttr(focused()))];
            };
            return v;
        }

        var sideTitle = Title(sideFrame, () => " Refs ", () => side.HasFocus);
        var graphTitle = Title(graphFrame, () => graphTitleText, () => list.HasFocus);
        var detailTitle = new SegView { X = Pos.Left(detailFrame) + 2, Y = Pos.Top(detailFrame), Height = 1, Width = 8 };
        detailTitle.Build = () => [(" Commit ", TitleAttr(false))];
        win.Add(header, tabs, sideFrame, graphFrame, detailFrame, status, sideTitle, graphTitle, detailTitle);
        _active = list;

        side.HasFocusChanged += (_, e) =>
        {
            if (e.NewValue)
            {
                _active = side;
                UpdateStatus();
                sideTitle.SetNeedsDraw();
                graphTitle.SetNeedsDraw();
            }
        };
        list.HasFocusChanged += (_, e) =>
        {
            if (e.NewValue)
            {
                _active = list;
                UpdateStatus();
                sideTitle.SetNeedsDraw();
                graphTitle.SetNeedsDraw();
            }
        };

        void UpdateStatus()
        {
            var n = _active.Source?.Count ?? 0;
            var mode = _searching ? "SEARCH" : "NORMAL";
            var pos = n == 0 ? 0 : (_active.SelectedItem ?? 0) + 1;
            statusText = _searching
                ? " /" + _query
                : $" {mode}  [{(_active == list ? "Graph" : "Refs")}]  {pos}/{n}  Tab j/k gg G ^d ^u / n N q";
            if (graph.Count > 0)
            {
                graphTitleText = $" Graph  {(list.SelectedItem ?? 0) + 1}/{graph.Count} ";
                graphTitle.SetNeedsDraw();
            }

            status.SetNeedsDraw();
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
                var text = _active == list ? graph.Rows[i].Subject : refs.Items[i].Text;
                if (text.Contains(_query, StringComparison.OrdinalIgnoreCase))
                {
                    Select(i);
                    return;
                }
            }

            statusText = " no match: " + _query;
            status.SetNeedsDraw();
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
                // Default focus navigation did not move between the two lists, so switch explicitly.
                (_active == list ? side : list).SetFocus();
                k.Handled = true;
                return;
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

    private static List<SidebarItem> LoadRefs(string repo)
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var a in new[] { "-C", repo, "for-each-ref", "--format=%(refname:short)\t%(refname)" })
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        var branches = new List<SidebarItem>();
        var tags = new List<SidebarItem>();
        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            var parts = line.Split('\t');
            var item = new SidebarItem(parts[0], false);
            (parts.Length > 1 && parts[1].StartsWith("refs/tags/", StringComparison.Ordinal) ? tags : branches).Add(item);
        }

        p.WaitForExit();
        var res = new List<SidebarItem> { new($"Branches ({branches.Count})", true) };
        res.AddRange(branches);
        res.Add(new($"Tags ({tags.Count})", true));
        res.AddRange(tags);
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
                foreach (var a in new[] { "-C", repo, "show", "--stat", "--format=fuller", hash })
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
