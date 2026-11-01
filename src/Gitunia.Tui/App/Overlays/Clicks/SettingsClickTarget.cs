using Gitunia.Core.Mouse;
using Gitunia.Tui.App.Settings;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on the settings screen.</summary>
public sealed class SettingsClickTarget(SettingsScreenInput settings) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        OverlayClick.OfEditor(settings.OnLineClicked(line, column, out var handled), handled);
}
