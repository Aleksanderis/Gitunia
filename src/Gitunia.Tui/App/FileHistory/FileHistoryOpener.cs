using Gitunia.App;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.FileHistory;

/// <summary>
/// Opens the history of the file under the cursor (<see cref="ActionIds.FileHistoryOpen"/>) from the commit file list
/// and the Changes view. It decides nothing: the reducer asks for the log, this only names the file and where to start.
/// </summary>
/// <param name="host">Holds the active tab and takes the command.</param>
/// <param name="source">Tells the file under the cursor.</param>
/// <param name="lines">Tells the lines selected in the focused diff, for <see cref="ActionIds.LineHistoryOpen"/>.</param>
/// <param name="visual">The Visual range, which ends once the line history opened.</param>
public sealed class FileHistoryOpener(
    Dispatcher host,
    IFileHistoryTargetSource source,
    ILineHistorySource lines,
    VisualRangeInput visual) : IActionHandler, IActionNotice
{
    /// <summary>The notice when the cursor is not on a file.</summary>
    public const string NoFileNotice = "Select a file to see its history";

    /// <inheritdoc />
    public string Notice { get; private set; } = string.Empty;

    /// <summary>Opens the history on <see cref="ActionIds.FileHistoryOpen"/>; false lets the next handler try.</summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, unused.</param>
    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action == ActionIds.LineHistoryOpen)
        {
            return OpenLines();
        }

        if (action != ActionIds.FileHistoryOpen)
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is not { } tabId || source.FileUnderCursor() is not { } target)
        {
            Notice = NoFileNotice;

            return true;
        }

        host.Dispatch(new FileHistoryOpenRequested(tabId, target.Path, target.Revision));

        return true;
    }

    private bool OpenLines()
    {
        var target = lines.Resolve();
        if (host.Workspace.ActiveTabId is not { } tabId || target.Path is not { } path)
        {
            Notice = target.Notice;

            return true;
        }

        Notice = target.Notice;
        host.Dispatch(new FileHistoryOpenRequested(tabId, path, target.Revision, target.Range));

        // A refused open (a read still running) keeps the range.
        if (host.State.Shell.FileHistory.IsReading)
        {
            visual.End();
        }

        return true;
    }
}
