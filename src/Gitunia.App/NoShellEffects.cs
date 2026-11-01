using Gitunia.Core.Clipboard;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>The shell of a runner without a terminal: nothing happens.</summary>
public sealed class NoShellEffects : IShellEffects
{
    public static NoShellEffects Instance { get; } = new();

    public void CopyToClipboard(CopyToClipboardEffect effect)
    {
    }

    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments)
    {
    }
}
