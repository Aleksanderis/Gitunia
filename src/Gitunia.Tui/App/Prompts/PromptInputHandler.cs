using Gitunia.App;
using Gitunia.Core.Editing;
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
public sealed class PromptInputHandler(Dispatcher host) : IActionHandler, ITextKeyHandler, IPromptDisplay
{
    public PromptLine StatusLine => PromptLine.Empty;

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
        else if (action == ActionIds.ListDown || action == ActionIds.ListUp)
        {
            // The wheel and the move hint of the popup; a prompt without candidates takes it and does nothing.
            var step = count ?? 1;
            host.Dispatch(new PromptMovedCommand(action == ActionIds.ListDown ? step : -step));
        }
        else if (action == ActionIds.PromptRow)
        {
            ChooseRow(count ?? 0);
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

    // A click on a row of the popup: an option of a one-key prompt answers, a candidate row takes the highlight.
    private void ChooseRow(int row)
    {
        if (host.State.Shell.Prompt is { KeyChoices: not null })
        {
            host.Dispatch(new PromptChoiceSelectedCommand(row));
        }
        else
        {
            host.Dispatch(new PromptMovedCommand(row));
        }
    }
}
