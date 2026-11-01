using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Prompts;

/// <summary>
/// The terminal side of the one prompt model (#971): while a prompt is open in <see cref="ShellState"/>
/// it turns Enter, Esc and the typed keys into prompt commands and hands the popup to the renderer. It decides nothing
/// and knows no feature: what a prompt means is its result command, reduced in Core.
/// </summary>
/// <param name="host">Holds the state the popup is drawn from and takes the commands.</param>
public sealed class PromptInputHandler(Dispatcher host) : IPromptHandler
{
    public string Notice => string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup => host.State.Shell.Prompt is { } prompt ? OpenPromptPopup.Of(prompt) : null;

    public bool TryExecute(ActionId action, int? count)
    {
        if (host.State.Shell.Prompt is null)
        {
            return false;
        }

        if (action == ActionIds.InputAccept)
        {
            host.Dispatch(new PromptAcceptedCommand());
        }
        else if (action == ActionIds.Cancel)
        {
            host.Dispatch(new PromptCancelledCommand());
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (host.State.Shell.Prompt is null)
        {
            return false;
        }

        host.Dispatch(new PromptKeyPressedCommand(key));

        return true;
    }

    public void OnGraphChanged()
    {
    }
}
