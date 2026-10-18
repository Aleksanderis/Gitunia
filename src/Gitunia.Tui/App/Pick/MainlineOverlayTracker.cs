using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.App.Pick;

/// <summary>
/// Keeps the mainline overlay in step with the screen: shows or hides it when the prompt opens or closes, and places it
/// again when the window is resized while it is open. Another overlay on top keeps the mainline one untouched.
/// </summary>
/// <param name="hasPrompt">Whether a mainline prompt is pending in the active tab.</param>
/// <param name="otherOverlayOpen">Whether the palette, a confirm or the reflog overlay is open.</param>
/// <param name="refresh">Builds and shows the current overlay frame.</param>
public sealed class MainlineOverlayTracker(Func<bool> hasPrompt, Func<bool> otherOverlayOpen, Action refresh)
{
    private bool _shown;

    /// <summary>Subscribes the re-layout to the window's viewport changes, which a resize raises.</summary>
    public void Attach(View window) => window.ViewportChanged += (_, _) => OnResized();

    /// <summary>The prompt opened, closed or its tab was left: refresh when the shown state differs.</summary>
    public void Sync()
    {
        var pending = hasPrompt();
        if (pending != _shown)
        {
            _shown = pending;
            refresh();
        }
    }

    /// <summary>The screen size changed: place an open mainline overlay again.</summary>
    public void OnResized()
    {
        if (_shown && !otherOverlayOpen())
        {
            refresh();
        }
    }
}
