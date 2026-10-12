using System.Globalization;

namespace Gitunia.Tui.Rendering;

/// <summary>Builds the header, tab row and status bar lines.</summary>
public static class ChromeBuilder
{
    private const string KeyHints = "j/k gg G ^d ^u @ q";
    private const string PickerHints = "Enter open  Tab complete  Alt-p pin  Alt-d forget  Esc close";

    public static StyledLine Header(ScreenModel model, int width, GraphGlyphSet glyphs) =>
        new LineBuilder(width, glyphs.Ellipsis)
            .Append(" " + glyphs.Node + " Gitunia  " + glyphs.Horizontal + "  " + model.RepositoryName, SegmentRole.Header)
            .Build(SegmentRole.Header);

    public static StyledLine Tabs(ScreenModel model, int width, GraphGlyphSet glyphs)
    {
        if (model.Tabs is { Count: > 0 } labels)
        {
            return TabRow(labels, width, glyphs);
        }

        if (model.TabCount == 0)
        {
            return new LineBuilder(width, glyphs.Ellipsis).Append(" no repository open", SegmentRole.Faint).Build(SegmentRole.Plain);
        }

        var label = model.BranchName is { } branch ? $" {model.TabNumber} {model.RepositoryName}  {branch} " : $" {model.TabNumber} {model.RepositoryName} ";
        return new LineBuilder(width, glyphs.Ellipsis)
            .Append(label, SegmentRole.ActiveTab)
            .Append(model.TabCount > 1 ? $"  {model.TabCount} tabs" : string.Empty, SegmentRole.Faint)
            .Build(SegmentRole.Plain);
    }

    public static StyledLine Status(ScreenModel model, int width, GraphGlyphSet glyphs)
    {
        var position = model.SelectedPosition.ToString(CultureInfo.InvariantCulture)
            + "/" + model.LoadedCount.ToString(CultureInfo.InvariantCulture)
            + (model.IsLoading ? "+" : string.Empty);
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append(" " + model.ModeName + " ", SegmentRole.StatusMode)
            .Append(" ", SegmentRole.Status);
        if (model.PendingKeys.Length > 0)
        {
            builder.Append(model.PendingKeys + " ", SegmentRole.StatusMode);
        }

        builder.Append(position + " ", SegmentRole.Status);
        if (model.IsLoading)
        {
            builder.Append("loading... ", SegmentRole.Status);
        }

        if (model.Message.Length > 0)
        {
            builder.Append(" " + model.Message + " ", SegmentRole.Status);
        }

        builder.Append(" " + (model.Picker is null ? KeyHints : PickerHints), SegmentRole.Status);
        return builder.Build(SegmentRole.Status);
    }

    private static StyledLine TabRow(IReadOnlyList<TabLabel> labels, int width, GraphGlyphSet glyphs)
    {
        var cells = TabBarLayout.Layout(labels, width, glyphs);
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (cells.Count > 0 && cells[0].Number > 1)
        {
            builder.Append("< ", SegmentRole.Faint);
        }

        for (var index = 0; index < cells.Count; index++)
        {
            builder.Append(index > 0 ? " " : string.Empty, SegmentRole.Plain);
            builder.Append(cells[index].Text, cells[index].IsActive ? SegmentRole.ActiveTab : SegmentRole.InactiveTab);
        }

        return builder.Build(SegmentRole.Plain);
    }
}
