using Gitunia.Core.Clipboard;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>
/// The effects only the terminal layer can perform (the clipboard, the terminal hand-off). The
/// runner
/// calls it on the UI thread; the terminal layer implements it. Each moves into a handler of its own when
/// its feature migrates (the clipboard: #971). The confirm dialog and the key mode are state (#970).
/// </summary>
public interface IShellEffects
{
    /// <summary>Copies the text to the clipboard and reports the outcome.</summary>
    void CopyToClipboard(CopyToClipboardEffect effect);

    /// <summary>Offers to run the same git arguments in the real terminal (credentials are needed).</summary>
    void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments);
}
