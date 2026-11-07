using Gitunia.Core.Operations;
using Gitunia.Git.Operations.Models;

namespace Gitunia.Core.Branches;

/// <summary>Refusals that need no git and apply to more than one branch action.</summary>
public static class BranchRefusals
{
    /// <summary>Shown when a second branch operation is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: the last branch operation is still running";

    /// <summary>
    /// Why a checkout cannot start, or null: another branch operation runs, or a merge, rebase, cherry-pick or revert
    /// is unfinished. The reflog list asks this before it closes, so a refusal keeps the list.
    /// </summary>
    /// <param name="isBusy">A branch operation runs for the tab.</param>
    /// <param name="operation">The stopped operation of the tab, null when none.</param>
    public static string? ForCheckout(bool isBusy, OperationInfo? operation)
    {
        if (isBusy)
        {
            return BusyNotice;
        }

        return ForOperation(operation);
    }

    /// <summary>Why a checkout must wait for an unfinished operation, or null when none is stopped.</summary>
    /// <param name="operation">The stopped operation of the tab, null when none.</param>
    public static string? ForOperation(OperationInfo? operation)
    {
        if (operation is null)
        {
            return null;
        }

        var name = OperationBannerBuilder.NameOf(operation.Kind);

        return OperationKeys.InProgress(name);
    }
}
