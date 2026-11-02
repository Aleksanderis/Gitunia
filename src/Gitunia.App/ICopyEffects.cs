using Gitunia.Core.Clipboard;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>The clipboard and the terminal retry.</summary>
public interface ICopyEffects
{
    /// <summary>Copies the text to the clipboard and reports the outcome.</summary>
    void CopyToClipboard(CopyToClipboardEffect effect);

    /// <summary>Copies the rows of a Visual range and answers with <c>VisualCopied</c>.</summary>
    void CopyVisualRange(CopyVisualRangeEffect effect);

    /// <summary>Offers to run the same git arguments in the real terminal (credentials are needed).</summary>
    void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments);

    /// <summary>Runs the same git arguments in the real terminal after the user accepted the retry dialog.</summary>
    void RunTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments);
}
