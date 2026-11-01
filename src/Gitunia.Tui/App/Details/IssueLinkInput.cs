using Gitunia.App;
using Gitunia.Core.IssueLinks;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Details;

/// <summary>
/// The terminal side of the issue links: <c>gx</c> (or <c>Enter</c> and a double-click on a line that has a link)
/// becomes an <see cref="IssueLinkOpenRequested"/> with the links of the selected message line. The reducer decides.
/// </summary>
/// <param name="host">Takes the commands.</param>
/// <param name="view">The detail pane.</param>
public sealed class IssueLinkInput(Dispatcher host, IIssueLinkView view) : IActionHandler
{
    public bool TryExecute(ActionId action, int? count)
    {
        var isOpenLink = action == ActionIds.DetailOpenLink;
        if ((!isOpenLink && action != ActionIds.DetailOpenDiff) || !view.IsMessageFocused())
        {
            return false;
        }

        var links = view.LinksOfSelectedRow();
        if (links.IsEmpty && !isOpenLink)
        {
            return false;
        }

        host.Dispatch(new IssueLinkOpenRequested(links, count, isOpenLink));

        return true;
    }
}
