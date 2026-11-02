using Gitunia.App.Loading.Graph;

namespace Gitunia.Tui.Views.Theme;

/// <summary>Reports the lane colours of the palette that is configured right now.</summary>
public sealed class PaletteLaneColors : ILaneColors
{
    public int Count => Palette.LaneColorCount;
}
