using Gitunia.Core;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>
/// The effects only the terminal layer can perform (dialogs, key mode, the clipboard, the terminal hand-off). The
/// runner
/// calls it on the UI thread; the terminal layer implements it. Each moves into state or into a handler of its own when
/// its feature migrates (key mode: #970).
/// </summary>
public interface IShellEffects
{
    /// <summary>Shows the confirm dialog and dispatches the command on <c>y</c>; false when it cannot be
    /// shown.</summary>
    bool ShowConfirm(ConfirmPrompt prompt, AppCommand onAccepted);

    /// <summary>Switches the keymap mode.</summary>
    void SetKeyMode(KeyMode mode);

    /// <summary>Copies the text to the clipboard and reports the outcome.</summary>
    void CopyToClipboard(CopyToClipboardEffect effect);

    /// <summary>Offers to run the same git arguments in the real terminal (credentials are needed).</summary>
    void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments);
}
