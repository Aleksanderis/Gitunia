using Gitunia.Core.Keymap;
using Gitunia.Core.Search;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App;

/// <summary>
/// The tab rename prompt (#219, <c>Ctrl-w r</c>): a status bar prompt that starts with the tab's current name; <c>Enter</c> saves it
/// (the name is stored in the session), an empty name goes back to the repository name, <c>Esc</c> leaves the name alone.
/// </summary>
public sealed class TabRenameController(WorkspaceHost host, Action<KeyMode> setMode, Action redraw) : IPromptHandler
{
    private TabId? renaming;
    private string text = string.Empty;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => renaming is null ? string.Empty : "rename tab: " + text + "_  (empty: repository name)";

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action == ActionIds.TabRename && host.Workspace.ActiveTab is { } tab)
        {
            renaming = tab.Id;
            text = tab.Title;
            setMode(KeyMode.Insert);
        }
        else if (action == ActionIds.InputAccept && renaming is { } id)
        {
            renaming = null;
            setMode(KeyMode.Normal);
            host.Dispatch(new TabCommand(id, new SetTabNameCommand(text)));
            Notice = host.Workspace.FindTab(id) is { } renamed ? "tab is now " + renamed.Title : string.Empty;
        }
        else if (action == ActionIds.Cancel && renaming is not null)
        {
            renaming = null;
            setMode(KeyMode.Normal);
        }
        else
        {
            return false;
        }

        redraw();
        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (renaming is null)
        {
            return false;
        }

        if (PromptTextEditor.Apply(text, key) is { } edited)
        {
            text = edited;
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }
}
