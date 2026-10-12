using System.Globalization;

namespace Gitunia.Tui.Rendering;

/// <summary>Builds the header, tab row and status bar lines.</summary>
public static class ChromeBuilder
{
    private const string KeyHints = "j/k gg G ^d ^u @ q";

    public static StyledLine Header(ScreenModel model, int width, GraphGlyphSet glyphs) =>
        new LineBuilder(width, glyphs.Ellipsis)
            .Append(" " + glyphs.Node + " Gitunia  " + glyphs.Horizontal + "  " + model.RepositoryName, SegmentRole.Header)
            .Build(SegmentRole.Header);

    public static StyledLine Tabs(ScreenModel model, int width, GraphGlyphSet glyphs)
    {
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

        builder.Append((model.SearchLine.Length > 0 ? model.SearchLine : position) + " ", SegmentRole.Status);
        if (model.IsLoading)
        {
            builder.Append("loading... ", SegmentRole.Status);
        }

        if (model.Message.Length > 0)
        {
            builder.Append(" " + model.Message + " ", SegmentRole.Status);
        }

        if (model.SearchLine.Length == 0)
        {
            builder.Append(" " + KeyHints, SegmentRole.Status);
        }

        return builder.Build(SegmentRole.Status);
    }
}
