using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>What a <see cref="LineView"/> shows: the row built for the current width on every draw.</summary>
public interface ILineSource
{
    /// <summary>The row to draw.</summary>
    /// <param name="width">The width of the view in columns.</param>
    StyledLine Line(int width);
}
