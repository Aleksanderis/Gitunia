using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Pick;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Pick;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Pick;

/// <summary>
/// The terminal side of cherry-pick (<c>gp</c>) and revert (<c>gv</c>): it turns the keys into
/// <see cref="PickCommand"/>s for the active tab and, while the mainline question is open in the shell state, the
/// answer keys (a digit, Esc) and the click on a parent row into commands. It decides nothing; the reducer does, and
/// the runner performs the effects.
/// </summary>
/// <param name="host">Holds the state the question is drawn from and takes the commands.</param>
/// <param name="selection">What the actions act on.</param>
public sealed class PickActions(Dispatcher host, IPickSelectionSource selection) : IPromptHandler
{
    /// <summary>Refusals are status messages posted by the reducer, so there is no notice.</summary>
    public string Notice => string.Empty;

    /// <summary>The mainline question as a status line, for a screen too small for the overlay.</summary>
    public string StatusLine
    {
        get
        {
            if (OpenQuestion() is not { } question)
            {
                return string.Empty;
            }

            return PickTexts.MainlinePrompt(question.Kind, question.Merge);
        }
    }

    /// <summary>The mainline question as an overlay (title, a row per parent); null while none is open here.</summary>
    public MainlinePrompt? Prompt
    {
        get
        {
            if (OpenQuestion() is not { } question)
            {
                return null;
            }

            var merge = question.Merge;

            return new MainlinePrompt(
                PickTexts.MainlineTitle(question.Kind, merge),
                PickTexts.MainlineRows(merge),
                question.UsableParents);
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        if (host.Workspace.ActiveTabId is not { } tabId)
        {
            return false;
        }

        if (OpenQuestion() is not null)
        {
            return TryAnswer(tabId, action, count);
        }

        var kind = KindOf(action);
        if (kind is null)
        {
            return false;
        }

        var current = selection.Current();
        host.Dispatch(new PickRequested(tabId, kind.Value, new PickTarget(current.RowKind, [.. current.Hashes])));

        // The range ends once the action took it: when a refusal came back instead, it stays.
        if (host.Workspace.FindTab(tabId)?.Running.IsRunning(RunningOperationKind.Pick) == true)
        {
            selection.EndRange();
        }

        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (OpenQuestion() is not { } question)
        {
            return false;
        }

        var normalized = key.Normalize();
        if (normalized is { Modifiers: KeyModifiers.None } && MainlineOf(normalized.Name) is { } parent)
        {
            host.Dispatch(new PickMainlineChosen(question.TabId, parent));
        }

        return true;
    }

    public void OnGraphChanged()
    {
    }

    private static PickKind? KindOf(ActionId action)
    {
        if (action == ActionIds.PickCherry)
        {
            return PickKind.CherryPick;
        }

        return action == ActionIds.PickRevert ? PickKind.Revert : null;
    }

    private static int? MainlineOf(string name) =>
        name.Length == 1 && char.IsAsciiDigit(name[0]) ? name[0] - '0' : null;

    // The question belongs to the tab it was opened in; the reducer drops it when that tab is left.
    private MainlineQuestion? OpenQuestion() =>
        host.State.Shell.Mainline is { } question && host.Workspace.ActiveTabId == question.TabId ? question : null;

    private bool TryAnswer(TabId tabId, ActionId action, int? count)
    {
        if (action == ActionIds.Cancel)
        {
            host.Dispatch(new PickMainlineCancelled(tabId));
        }
        else if (action == ActionIds.PickMainline)
        {
            host.Dispatch(new PickMainlineChosen(tabId, count ?? 0));
        }
        else if (action != ActionIds.InputAccept)
        {
            return false;
        }

        return true;
    }
}
