using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Tabs;

/// <summary>
/// The tab rename prompt (#219, <c>Ctrl-w r</c>): a prompt popup that starts with the tab's current name;
/// <c>Enter</c> saves it (the name is stored in the session), an empty name goes back to the repository name,
/// <c>Esc</c> leaves the name alone.
/// </summary>
public sealed class TabRenameController(WorkspaceHost host, Action<KeyMode> setMode, Action redraw) : IPromptHandler
{
    private TabId? _renaming;
    private string _text = string.Empty;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup
    {
        get
        {
            if (_renaming is null)
            {
                return null;
            }

            return PromptPopup.ForText("Rename tab", "Empty restores the repository name", _text, "save");
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action == ActionIds.TabRename && host.Workspace.ActiveTab is { } tab)
        {
            _renaming = tab.Id;
            _text = tab.Title;
            setMode(KeyMode.Insert);
        }
        else if (action == ActionIds.InputAccept && _renaming is { } id)
        {
            _renaming = null;
            setMode(KeyMode.Normal);
            host.Dispatch(new TabCommand(id, new SetTabNameCommand(_text)));
            Notice = host.Workspace.FindTab(id) is { } renamed ? $"tab is now {renamed.Title}" : string.Empty;
        }
        else if (action == ActionIds.Cancel && _renaming is not null)
        {
            _renaming = null;
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
        if (_renaming is null)
        {
            return false;
        }

        if (PromptTextEditor.Apply(_text, key) is { } edited)
        {
            _text = edited;
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }
}
