using Gitunia.Core.IssueTracker.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Platform.Urls;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Details;

/// <summary>
/// Opens the tracker link on the selected line of the commit message (<c>gx</c>, or <c>Enter</c> and a double-click
/// on a line that has one). Where no browser can be started the address is copied instead.
/// </summary>
/// <param name="opener">Starts the system opener.</param>
/// <param name="clipboard">The fallback when the opener fails.</param>
/// <param name="statuses">Where the outcome goes.</param>
/// <param name="source">The detail pane's content of the active tab, null when there is none.</param>
/// <param name="selectedRow">The selected row of the detail list.</param>
/// <param name="postToUi">Runs an action on the UI thread; the opener runs off it and reports back here.</param>
/// <param name="isMessageFocused">The detail pane has focus and shows the Details tab.</param>
public sealed class IssueLinkActions(
    IUrlOpener opener,
    ClipboardCopier clipboard,
    StatusChannel statuses,
    Func<DetailListSource?> source,
    Func<int?> selectedRow,
    Func<bool> isMessageFocused,
    Action<Action> postToUi)
{
    private static readonly UrlOpenResult OpenerFailed = new(false, "the opener failed");

    public bool TryExecute(ActionId action, int? count)
    {
        var isOpenLink = action == ActionIds.DetailOpenLink;
        if ((!isOpenLink && action != ActionIds.DetailOpenDiff) || !isMessageFocused())
        {
            return false;
        }

        var references = ReferencesOfSelectedRow();
        if (references.Count == 0)
        {
            if (isOpenLink)
            {
                statuses.Warning("No issue link on this line");
            }

            return isOpenLink;
        }

        var number = count ?? 1;
        if (number < 1 || number > references.Count)
        {
            var noun = references.Count == 1 ? "issue link" : "issue links";
            statuses.Warning($"This line has {references.Count} {noun}: there is no link number {number}");

            return true;
        }

        Open(references[number - 1].Url);

        return true;
    }

    private IReadOnlyList<IssueReference> ReferencesOfSelectedRow()
    {
        var rows = source()?.Content.Rows;

        return selectedRow() is { } row && rows is not null && row >= 0 && row < rows.Count
            ? rows[row].References ?? []
            : [];
    }

    // Starting the opener can stall (ShellExecute on a cold start), so it never runs on the UI thread.
    private void Open(string url)
    {
        statuses.Post($"Opening {url}");
        _ = Task.Run(() => opener.Open(url), CancellationToken.None)
            .ContinueWith(
                task => postToUi(() => Report(url, task.IsCompletedSuccessfully ? task.Result : OpenerFailed)),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
    }

    private void Report(string url, UrlOpenResult result)
    {
        if (result.Opened)
        {
            statuses.Post($"Opened {url}");

            return;
        }

        if (clipboard.Copy(url, "issue link"))
        {
            statuses.Warning($"Could not open the link ({result.Error}); copied {url} instead");
        }
    }
}
