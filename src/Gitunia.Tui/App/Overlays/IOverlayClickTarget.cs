namespace Gitunia.Tui.App.Overlays;

/// <summary>An overlay or editor that can take a click on a line of the overlay frame.</summary>
public interface IOverlayClickTarget
{
    /// <summary>Handles a click on a frame line and column.</summary>
    /// <param name="line">The line of the frame.</param>
    /// <param name="column">The column of the frame.</param>
    /// <returns>What the click came to, null when this overlay is not under the click.</returns>
    OverlayClick? ClickAt(int line, int column);
}
