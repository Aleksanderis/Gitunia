using System.Globalization;
using Gitunia.Core.Status;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.Tabs;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>Builds the tab row and status bar lines.</summary>
public static class ChromeBuilder
{
    private const string KeyHints = "j/k gg G ^d ^u @ ? : q";
    private const string PickerHints = "Enter open  Tab accept  Alt-p pin to top  Alt-d forget  Esc close";

    public static StyledLine Tabs(ScreenModel model, int width, GraphGlyphSet glyphs)
    {
        if (model.Tabs is { Count: > 0 } labels)
        {
            return TabRow(labels, width, glyphs);
        }

        if (model.TabCount == 0)
        {
            return new LineBuilder(width, glyphs.Ellipsis).Append(" no repository open", SegmentRole.Status).Build(SegmentRole.Status);
        }

        return new LineBuilder(width, glyphs.Ellipsis)
            .Append(" ", SegmentRole.Status)
            .Append($"  {model.TabNumber} {model.RepositoryName}  ", SegmentRole.ActiveTab)
            .Append(model.TabCount > 1 ? $"  {model.TabCount} tabs" : string.Empty, SegmentRole.Status)
            .Build(SegmentRole.Status);
    }

    public static StyledLine Status(ScreenModel model, int width, GraphGlyphSet glyphs)
    {
        var position = $"{model.SelectedPosition.ToString(CultureInfo.InvariantCulture)}/{model.LoadedCount.ToString(CultureInfo.InvariantCulture)}{(model.IsLoading ? "+" : string.Empty)}";
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append($" {model.ModeName} ", SegmentRole.StatusMode)
            .Append(" ", SegmentRole.Status);
        if (model.PendingKeys.Length > 0)
        {
            builder.Append($"{model.PendingKeys} ", SegmentRole.StatusMode);
        }

        builder.Append($"{(model.SearchLine.Length > 0 ? model.SearchLine : position)} ", SegmentRole.Status);
        if (model.IsLoading)
        {
            builder.Append("loading... ", SegmentRole.Status);
        }

        if (model.Message.Length > 0)
        {
            builder.Append($" {model.Message} ", model.MessageSeverity == StatusSeverity.Error ? SegmentRole.StatusError : SegmentRole.Status);
        }

        if (model.SearchLine.Length == 0)
        {
            builder.Append($" {(model.Picker is not null ? PickerHints : model.Hints.Length > 0 ? model.Hints : KeyHints)}", SegmentRole.Status);
        }

        return builder.Build(SegmentRole.Status);
    }

    private static StyledLine TabRow(IReadOnlyList<TabLabel> labels, int width, GraphGlyphSet glyphs)
    {
        var cells = TabBarLayout.Layout(labels, width, glyphs);
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append(new string(' ', TabBarLayout.LeftMargin), SegmentRole.Status);
        if (cells.Count > 0 && cells[0].Number > 1)
        {
            builder.Append("< ", SegmentRole.Status);
        }

        for (var index = 0; index < cells.Count; index++)
        {
            builder.Append(index > 0 ? " " : string.Empty, SegmentRole.Status);
            builder.Append(
                cells[index].IsActive ? cells[index].Text : ButtonText(cells[index].Text, glyphs),
                cells[index].IsActive ? SegmentRole.ActiveTab : SegmentRole.RepositoryTab);
        }

        builder.Append(" ", SegmentRole.Status);
        builder.Append(TabBarLayout.NewTabLabel, SegmentRole.BarButton);
        if (TabBarLayout.CommandsStart(width) is { } commandsStart)
        {
            builder.Append(new string(' ', Math.Max(0, commandsStart - (width - builder.Remaining))), SegmentRole.Status);
            builder.Append(TabBarLayout.CommandsLabel, SegmentRole.BarButton);
        }

        return builder.Build(SegmentRole.Status);
    }

    // Without Unicode a button is marked by brackets in its outer padding columns, so its width and click zone stay the same.
    private static string ButtonText(string text, GraphGlyphSet glyphs) =>
        glyphs.IsAscii && text.Length > 2 && text[0] == ' ' && text[^1] == ' ' ? $"[{text[1..^1]}]" : text;
}
