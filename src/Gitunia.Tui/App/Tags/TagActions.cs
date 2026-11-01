using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Tags;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Prompts;

namespace Gitunia.Tui.App.Tags;

/// <summary>
/// The terminal side of the tag actions: it turns the tag keys into <see cref="TagCommand"/>s for the active tab. It
/// decides nothing; the reducer does, and the runner performs the effects. The prompts it leads to are the shared prompt
/// model (<see cref="PromptInputHandler"/> turns their keys into commands).
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the actions act on.</param>
public sealed class TagActions(Dispatcher host, IBranchSelectionSource selection)
{
    /// <summary>Runs the action when it is a tag action; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (host.Workspace.ActiveTabId is not { } tabId)
        {
            return false;
        }

        var command = Request(tabId, action);
        if (command is null)
        {
            return false;
        }

        host.Dispatch(command);

        return true;
    }

    private TagCommand? Request(TabId tabId, ActionId action)
    {
        if (action != ActionIds.TagCreate && action != ActionIds.TagDelete && action != ActionIds.TagPush
            && action != ActionIds.TagDeleteRemote)
        {
            return null;
        }

        var current = selection.Current();
        var target = new TagTarget(current.RowKind, current.Name, current.CommitHash);
        if (action == ActionIds.TagCreate)
        {
            return new TagCreateRequested(tabId, target);
        }

        if (action == ActionIds.TagDelete)
        {
            return new TagDeleteRequested(tabId, target);
        }

        if (action == ActionIds.TagPush)
        {
            return new TagPushRequested(tabId, target);
        }

        return new TagDeleteOnRemoteRequested(tabId, target);
    }
}
