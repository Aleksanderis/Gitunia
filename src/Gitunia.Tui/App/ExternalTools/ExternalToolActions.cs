using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Git.ExternalTools;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.App.ExternalTools;

/// <summary>Maps the tool actions to hand-offs for the active tab.</summary>
public sealed class ExternalToolActions(
    ExternalToolRunner runner,
    Func<RepositoryLocation?> activeLocation,
    Func<string?> selectedCommit)
{
    /// <summary>The status bar notice of the last handled action, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

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

        Notice = Start(kind);

        return true;
    }

    private string Start(ExternalToolKind kind)
    {
        if (activeLocation() is not { } location)
        {
            return "No repository open";
        }

        var commit = selectedCommit();
        if (kind == ExternalToolKind.Difftool && commit is null)
        {
            return "No commit selected";
        }

        return runner.Launch(kind, location, commit);
    }
}
