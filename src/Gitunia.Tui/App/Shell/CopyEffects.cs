using Gitunia.App;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Remote;

namespace Gitunia.Tui.App.Shell;

/// <summary>The shell effects that copy text or offer the terminal.</summary>
/// <param name="clipboard">Copies text.</param>
/// <param name="ranges">Copies the rows of a Visual range.</param>
/// <param name="retry">Offers to run a git command in the real terminal.</param>
/// <param name="host">Takes the outcome of a copy.</param>
public sealed class CopyEffects(ClipboardCopier clipboard, RangeCopier ranges, TerminalRetry retry, Dispatcher host) : ICopyEffects
{
    /// <summary>Copies the text to the clipboard and reports the outcome.</summary>
    /// <param name="effect">The text.</param>
    public void CopyToClipboard(CopyToClipboardEffect effect) => clipboard.Copy(effect.Text, effect.Description);

    /// <summary>Copies the rows of a Visual range and answers with <c>VisualCopied</c>.</summary>
    /// <param name="effect">The range.</param>
    public void CopyVisualRange(CopyVisualRangeEffect effect)
    {
        var result = ranges.Copy(effect.Pane, effect.Range, effect.Chars, effect.IsOldSide);
        host.Dispatch(new VisualCopied(result.Copied, result.Notice));
    }

    /// <summary>Offers to run the same git arguments in the real terminal (credentials are needed).</summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="noun">What failed.</param>
    /// <param name="arguments">The git arguments.</param>
    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments) =>
        retry.Offer(tabId, noun, arguments);

    /// <summary>Runs the same git arguments in the real terminal after the user accepted the dialog.</summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="noun">What failed.</param>
    /// <param name="arguments">The git arguments.</param>
    public void RunTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments) =>
        retry.Run(tabId, noun, arguments);
}
