using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Operations;
using Gitunia.Core.Status;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Integration;

/// <summary>
/// Merges the selected commit, branch or tag into the current branch: <c>gm</c> fast-forwards when possible, <c>gM</c>
/// always makes a merge commit and <c>gs</c> squashes (ux-workflows.md, workflow 3, "Integrating"), as a pure
/// transition. Nothing is lost by a merge, so there is no Confirm; the status line names the old tip for the undo. A
/// stop on conflicts is not a failure: the in-progress operation takes over. Refused on a detached or unborn HEAD and
/// while another operation is in progress.
/// </summary>
public static class MergeReducer
{
    /// <summary>Shown when a merge is asked for while another call of the tab runs.</summary>
    public const string BusyNotice = "Busy: another operation of this tab is still running";

    /// <summary>Shown when no commit, branch or tag is selected.</summary>
    public const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) to merge";

    /// <summary>Applies a merge request.</summary>
    public static ImmutableArray<AppEffect> Reduce(MergeRequested requested, IntegrationContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        if (Refusal(context) is { } refusal)
        {
            return [Post(refusal)];
        }

        if (requested.Target is not { } target)
        {
            return [Post(SelectNotice)];
        }

        var effect = new MergeBranchEffect(
            context.TabId,
            context.Location,
            context.Head!.BranchName!,
            target,
            requested.Mode);

        return [effect];
    }

    private static PostStatusEffect Post(string text) => new(text, StatusSeverity.Warning);

    private static string? Refusal(IntegrationContext context)
    {
        if (context.Operation is { } operation)
        {
            var name = OperationBannerBuilder.NameOf(operation.Kind);

            return OperationKeys.InProgress(name);
        }

        if (context.Head is not { Kind: HeadKind.Branch, BranchName: not null })
        {
            if (context.Head?.Kind == HeadKind.Unborn)
            {
                return "The branch has no commits yet: nothing to merge into";
            }

            return "HEAD is detached: switch to a branch first";
        }

        return null;
    }
}
