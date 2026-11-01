using Gitunia.Core;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>The shell of a runner without a terminal: no dialog can be shown and nothing else happens.</summary>
public sealed class NoShellEffects : IShellEffects
{
    public static NoShellEffects Instance { get; } = new();

    public bool ShowConfirm(ConfirmPrompt prompt, AppCommand onAccepted) => false;

    public void SetKeyMode(KeyMode mode)
    {
    }

    public void CopyToClipboard(CopyToClipboardEffect effect)
    {
    }

    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments)
    {
    }
}
