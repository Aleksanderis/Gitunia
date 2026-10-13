using Gitunia.Core.Search;

namespace Gitunia.Tui.Rendering;

/// <summary>How graph rows are drawn at the current terminal size.</summary>
/// <param name="Width">Columns of the row.</param>
/// <param name="LaneColumns">Lanes shown on the left, the same for every row so columns stay put while scrolling.</param>
/// <param name="Glyphs">The characters to draw with.</param>
/// <param name="Highlight">The active graph search, whose matches are highlighted; null when none.</param>
public sealed record GraphRowOptions(int Width, int LaneColumns, GraphGlyphSet Glyphs, SearchQuery? Highlight = null)
{
    /// <summary>Where the columns of the row sit, computed once per options instead of once per row; build a new instance instead of using <c>with</c> on Width or LaneColumns.</summary>
    public GraphColumns Columns { get; } = GraphColumnLayout.Compute(Width, LaneColumns);

    /// <summary>Longest lane area as a share of the width, so subjects keep room on narrow terminals.</summary>
    public const int MaximumLaneAreaPercent = 40;

    /// <summary>Lane columns to show for a graph whose widest row has <paramref name="maximumLaneCount"/> lanes.</summary>
    public static int ChooseLaneColumns(int maximumLaneCount, int width)
    {
        var affordable = Math.Max(1, width * MaximumLaneAreaPercent / 100 / 2);
        return Math.Clamp(maximumLaneCount, 1, affordable);
    }
}
