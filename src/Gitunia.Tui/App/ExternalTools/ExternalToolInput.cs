using Gitunia.App;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Git.ExternalTools;
using Gitunia.Tui.App.Rebase;

namespace Gitunia.Tui.App.ExternalTools;

/// <summary>
/// The terminal side of the tool actions: it turns the tool keys into <see cref="ExternalToolRequested"/> for the
/// active tab. The reducer decides; the terminal hand-off runs as an effect.
/// </summary>
/// <param name="host">Takes the commands.</param>
/// <param name="selection">The commit selected in the view, which the difftool needs.</param>
public sealed class ExternalToolInput(Dispatcher host, IGraphSelectionSource selection)
{
    public bool TryExecute(ActionId action)
    {
        ExternalToolKind kind;
        if (action == ActionIds.ToolDifftool)
        {
            kind = ExternalToolKind.Difftool;
        }
        else if (action == ActionIds.ToolMergetool)
        {
            kind = ExternalToolKind.Mergetool;
        }
        else if (action == ActionIds.ToolCommitEditor)
        {
            kind = ExternalToolKind.CommitWithEditor;
        }
        else
        {
            return false;
        }

        host.Dispatch(new ExternalToolRequested(kind, selection.SelectedCommit()));

        return true;
    }
}
