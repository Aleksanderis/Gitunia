using Gitunia.CustomRenderTui.Demo.Graph;
using Gitunia.CustomRenderTui;

namespace Gitunia.CustomRenderTui.Demo.Views;

/// <summary>Draws the M1-like layout: title bar, tab bar, sidebar, graph, details pane and status line.</summary>
public static class Screen
{
    private static readonly byte[] LaneColors = [75, 114, 215, 177, 203, 80, 222, 141, 150, 209, 117, 183];

    public static void Draw(CellBuffer buf, ScreenModel m)
    {
        buf.Clear();
        var w = buf.Width;
        var h = buf.Height;
        if (w < 60 || h < 14)
        {
            buf.Put(0, 0, "terminal too small", Style.Bold);
            return;
        }

        DrawChrome(buf, m);
        var bodyH = h - 3;
        var sideW = Math.Clamp(w / 5, 16, 28);
        var detailsH = Math.Clamp(bodyH / 3, 6, 12);
        DrawSidebar(buf, m, 0, 2, sideW, bodyH);
        DrawGraph(buf, m, sideW, 2, w - sideW, bodyH - detailsH);
        DrawDetails(buf, m, sideW, 2 + bodyH - detailsH, w - sideW, detailsH);
        buf.Put(0, h - 1, m.Status.PadRight(w), Style.Dim | Style.Reverse);
    }

    private static void DrawChrome(CellBuffer buf, ScreenModel m)
    {
        var w = buf.Width;
        buf.Put(0, 0, (" ● Gitunia  ─  " + m.RepoName).PadRight(w), Style.Bold | Style.Reverse);
        buf.Put(0, 1, " 1 " + m.RepoName + " ", Style.Reverse);
        buf.Put(4 + m.RepoName.Length, 1, " +  (tabs: M1)", Style.Dim);
    }

    private static Style BorderStyle(ScreenModel m, Pane p) => m.Focus == p ? Style.Bold : Style.Dim;

    private static void DrawSidebar(CellBuffer buf, ScreenModel m, int x, int y, int w, int h)
    {
        buf.Box(x, y, w, h, "Refs", BorderStyle(m, Pane.Sidebar));
        var rows = h - 2;
        var top = m.SidebarCursor.Scroll(rows, m.Sidebar.Count);
        for (var r = 0; r < rows && top + r < m.Sidebar.Count; r++)
        {
            var item = m.Sidebar[top + r];
            var text = item.Header ? "▾ " + item.Text : "  " + item.Text;
            buf.Put(x + 1, y + 1 + r, Clip(text, w - 2), item.Header ? Style.Bold : Style.None, item.Header ? (byte)110 : (byte)0);
            if (top + r == m.SidebarCursor.Selection && m.Focus == Pane.Sidebar)
            {
                buf.StyleSpan(x + 1, y + 1 + r, w - 2, Style.Reverse);
            }
        }
    }

    private static void DrawGraph(CellBuffer buf, ScreenModel m, int x, int y, int w, int h)
    {
        var count = m.Graph.Count;
        buf.Box(x, y, w, h, $"Graph  {(count == 0 ? 0 : m.GraphCursor.Selection + 1)}/{count}{(m.Loading ? " loading…" : string.Empty)}", BorderStyle(m, Pane.Graph));
        var rows = h - 2;
        var inner = w - 2;
        var top = m.GraphCursor.Scroll(rows, count);

        var lanes = 1;
        for (var r = 0; r < rows && top + r < count; r++)
        {
            lanes = Math.Max(lanes, m.Graph.Layout.Row(top + r).Length);
        }

        var textX = x + 1 + (lanes * 2) + 1;
        for (var r = 0; r < rows && top + r < count; r++)
        {
            var idx = top + r;
            var row = m.Graph.Layout.Row(idx);
            for (var j = 0; j < row.Length; j++)
            {
                var glyph = row[j] switch
                {
                    LaneLayout.Pipe => "│",
                    LaneLayout.Node => "●",
                    LaneLayout.Merge => "╯",
                    LaneLayout.Fork => "╮",
                    _ => " ",
                };
                buf.Put(x + 1 + (j * 2), y + 1 + r, glyph, Style.None, LaneColors[j % LaneColors.Length]);
            }

            var hash = m.Graph.Store.Hash(idx);
            buf.Put(textX, y + 1 + r, hash.Length >= 7 ? hash[..7] : hash, Style.Dim, 244);
            buf.Put(textX + 8, y + 1 + r, Clip(m.Graph.Store.Subject(idx), x + w - 1 - (textX + 8)), Style.None);
            if (idx == m.GraphCursor.Selection)
            {
                buf.StyleSpan(x + 1, y + 1 + r, inner, m.Focus == Pane.Graph ? Style.Reverse : Style.Bold);
            }
        }
    }

    private static void DrawDetails(CellBuffer buf, ScreenModel m, int x, int y, int w, int h)
    {
        buf.Box(x, y, w, h, "Commit", BorderStyle(m, Pane.Details));
        var rows = h - 2;
        var top = m.DetailsCursor.Scroll(rows, m.Details.Count);
        for (var r = 0; r < rows && top + r < m.Details.Count; r++)
        {
            buf.Put(x + 1, y + 1 + r, Clip(m.Details[top + r], w - 2), Style.None);
        }
    }

    private static string Clip(string s, int width) => width <= 0 ? string.Empty : s.Length > width ? s[..width] : s;
}
