using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>The drawn banner and where its chips are, for hit testing.</summary>
/// <param name="Line">The line, exactly as wide as the screen.</param>
/// <param name="Chips">The click targets, left to right.</param>
public sealed record BannerLine(StyledLine Line, IReadOnlyList<BannerChipArea> Chips)
{
    /// <summary>The mouse view of the chip under <paramref name="column"/>, null when the column is not on a
    /// chip.</summary>
    public string? ViewAt(int column) =>
        Chips.FirstOrDefault(area => column >= area.Start && column < area.Start + area.Width)?.View;
}
