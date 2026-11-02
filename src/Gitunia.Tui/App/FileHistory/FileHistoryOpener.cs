using Gitunia.App;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.FileHistory;

/// <summary>
/// Opens the history of the file under the cursor (<see cref="ActionIds.FileHistoryOpen"/>) from the commit file list
/// and the Changes view. It decides nothing: the reducer asks for the log, this only names the file and where to start.
/// </summary>
/// <param name="host">Holds the active tab and takes the command.</param>
/// <param name="source">Tells the file under the cursor.</param>
public sealed class FileHistoryOpener(Dispatcher host, IFileHistoryTargetSource source)
    : IActionHandler, IActionNotice
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
}
