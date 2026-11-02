using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// The overlays that cover the two editors: the help, palette, menu and confirm dialog, the reflog list, the settings
/// screen, the mainline question and the prompt popups. The rebase editor must not take their keys.
/// </summary>
/// <param name="store">The help, palette, menu, confirm dialog and output box.</param>
/// <param name="reflog">The reflog list.</param>
/// <param name="settings">The settings screen.</param>
/// <param name="pick">The mainline question.</param>
/// <param name="prompts">The prompts with a popup.</param>
public sealed class OtherOverlays(
    OverlayStore store,
    ReflogInput reflog,
    SettingsScreenInput settings,
    PickActions pick,
    PromptDisplays prompts) : IOverlayCover
{
    /// <summary>A mainline question or a prompt popup hides behind the dialogs, lists and screens that are open.</summary>
    public bool IsHidingMainline => store.State.IsOpen || reflog.IsOpen || settings.IsOpen;

    /// <summary>The settings screen is open.</summary>
    public bool IsSettingsOpen => settings.IsOpen;

    /// <summary>The reflog list is open.</summary>
    public bool IsReflogOpen => reflog.IsOpen;

    /// <inheritdoc />
    public bool IsOtherOverlayOpen() =>
        store.State.IsOpen
        || reflog.IsOpen
        || settings.IsOpen
        || pick.Prompt is not null
        || prompts.CurrentPopup() is not null;
}
