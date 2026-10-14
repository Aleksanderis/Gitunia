using Gitunia.CustomRenderTui;
using Gitunia.CustomRenderTui.Demo.Graph;

namespace Gitunia.CustomRenderTui.Demo.Views;

/// <summary>Draws the M1-like layout: title bar, tab bar, sidebar, graph, details pane and status line.</summary>
public static class Screen
{
    private static readonly byte[] LaneColors = [75, 114, 215, 177, 203, 80, 222, 141, 150, 209, 117, 183];

    public static void Draw(CellBuffer buffer, ScreenModel model)
    {
        buffer.Clear();
        var width = buffer.Width;
        var height = buffer.Height;
        if (width < 60 || height < 14)
        {
            buffer.Put(0, 0, "terminal too small", Style.Bold);
            return;
        }

        DrawChrome(buffer, model);
        var bodyHeight = height - 3;
        var sidebarWidth = Math.Clamp(width / 5, 16, 28);
        var detailsHeight = Math.Clamp(bodyHeight / 3, 6, 12);
        DrawSidebar(buffer, model, 0, 2, sidebarWidth, bodyHeight);
        DrawGraph(buffer, model, sidebarWidth, 2, width - sidebarWidth, bodyHeight - detailsHeight);
        DrawDetails(buffer, model, sidebarWidth, 2 + bodyHeight - detailsHeight, width - sidebarWidth, detailsHeight);
        buffer.Put(0, height - 1, model.Status.PadRight(width), Style.Dim | Style.Reverse);
    }

    private static void DrawChrome(CellBuffer buffer, ScreenModel model)
    {
        var width = buffer.Width;
        buffer.Put(0, 0, ($" ● Gitunia  ─  {model.RepoName}").PadRight(width), Style.Bold | Style.Reverse);
        buffer.Put(0, 1, $" 1 {model.RepoName} ", Style.Reverse);
        buffer.Put(4 + model.RepoName.Length, 1, " +  (tabs: M1)", Style.Dim);
    }

    private static Style BorderStyle(ScreenModel model, Pane pane) => model.Focus == pane ? Style.Bold : Style.Dim;

    private static void DrawSidebar(CellBuffer buffer, ScreenModel model, int x, int y, int width, int height)
    {
        buffer.Box(x, y, width, height, "Refs", BorderStyle(model, Pane.Sidebar));
        var rows = height - 2;
        var top = model.SidebarCursor.Scroll(rows, model.Sidebar.Count);
        for (var row = 0; row < rows && top + row < model.Sidebar.Count; row++)
        {
            var item = model.Sidebar[top + row];
            var text = item.Header ? $"▾ {item.Text}" : $"  {item.Text}";
            buffer.Put(x + 1, y + 1 + row, Clip(text, width - 2), item.Header ? Style.Bold : Style.None, item.Header ? (byte)110 : (byte)0);
            if (top + row == model.SidebarCursor.Selection && model.Focus == Pane.Sidebar)
            {
                buffer.StyleSpan(x + 1, y + 1 + row, width - 2, Style.Reverse);
            }
        }
    }

    private static void DrawGraph(CellBuffer buffer, ScreenModel model, int x, int y, int width, int height)
    {
        var count = model.Graph.Count;
        buffer.Box(x, y, width, height, $"Graph  {(count == 0 ? 0 : model.GraphCursor.Selection + 1)}/{count}{(model.Loading ? " loading…" : string.Empty)}", BorderStyle(model, Pane.Graph));
        var rows = height - 2;
        var inner = width - 2;
        var top = model.GraphCursor.Scroll(rows, count);

        var lanes = 1;
        for (var row = 0; row < rows && top + row < count; row++)
        {
            lanes = Math.Max(lanes, model.Graph.Layout.Row(top + row).Length);
        }

        var textX = x + 1 + (lanes * 2) + 1;
        for (var row = 0; row < rows && top + row < count; row++)
        {
            var index = top + row;
            var glyphs = model.Graph.Layout.Row(index);
            for (var j = 0; j < glyphs.Length; j++)
            {
                var glyph = glyphs[j] switch
                {
                    LaneLayout.Pipe => "│",
                    LaneLayout.Node => "●",
                    LaneLayout.Merge => "╯",
                    LaneLayout.Fork => "╮",
                    _ => " ",
                };
                buffer.Put(x + 1 + (j * 2), y + 1 + row, glyph, Style.None, LaneColors[j % LaneColors.Length]);
            }

            var hash = model.Graph.Store.Hash(index);
            buffer.Put(textX, y + 1 + row, hash.Length >= 7 ? hash[..7] : hash, Style.Dim, 244);
            buffer.Put(textX + 8, y + 1 + row, Clip(model.Graph.Store.Subject(index), x + width - 1 - (textX + 8)), Style.None);
            if (index == model.GraphCursor.Selection)
            {
                buffer.StyleSpan(x + 1, y + 1 + row, inner, model.Focus == Pane.Graph ? Style.Reverse : Style.Bold);
            }
        }
    }

    private static void DrawDetails(CellBuffer buffer, ScreenModel model, int x, int y, int width, int height)
    {
        buffer.Box(x, y, width, height, "Commit", BorderStyle(model, Pane.Details));
        var rows = height - 2;
        var top = model.DetailsCursor.Scroll(rows, model.Details.Count);
        for (var row = 0; row < rows && top + row < model.Details.Count; row++)
        {
            buffer.Put(x + 1, y + 1 + row, Clip(model.Details[top + row], width - 2), Style.None);
        }
    }

    private static string Clip(string text, int width) => width <= 0 ? string.Empty : text.Length > width ? text[..width] : text;
}
