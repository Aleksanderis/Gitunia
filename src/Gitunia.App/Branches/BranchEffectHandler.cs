using Gitunia.Core.Branches;
using Gitunia.Core.Text;
using Gitunia.Git.Branches;
using Gitunia.Git.Refs;

namespace Gitunia.App.Branches;

/// <summary>
/// Runs the branch effects off the UI thread and posts exactly one result command for each: success with the text for
/// the status bar, failure with git's first error line, a request for the second delete answer, or cancellation. It
/// never reads or changes state. Timeouts stay in the git layer.
/// </summary>
/// <param name="writer">Runs the branch commands.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class BranchEffectHandler(IBranchWriter writer, IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(BranchEffect effect, CancellationToken cancellationToken)
    {
        EffectWork.Start(
            dispatcher,
            token => WorkAsync(effect, token),
            () => new BranchOperationCancelled(effect.TabId),
            exception => new BranchOperationFailed(
                effect.TabId,
                $"{FailureOf(effect)}: {FailureText.Reason(exception)}"),
            cancellationToken);
    }

    private Task<BranchCommand> WorkAsync(BranchEffect effect, CancellationToken token) => effect switch
    {
        CreateBranchEffect create => CreateAsync(create, token),
        DeleteBranchEffect delete => DeleteAsync(delete, token),
        FastForwardBranchEffect fastForward => FastForwardAsync(fastForward, token),
        RenameBranchEffect rename => RenameAsync(rename, token),
        SetUpstreamEffect set => SetUpstreamAsync(set, token),
        SwitchBranchEffect switchTo => SwitchAsync(switchTo, token),
        UnsetUpstreamEffect unset => UnsetUpstreamAsync(unset, token),
        _ => throw new UnhandledEffectException(effect),
    };

    private async Task<BranchCommand> CreateAsync(CreateBranchEffect effect, CancellationToken token)
    {
        await EnsureValidAsync(effect, effect.Name, token).ConfigureAwait(false);
        await writer.CreateAsync(effect.Location, effect.Name, effect.StartPoint, effect.Checkout, token)
            .ConfigureAwait(false);
        var suffix = effect.Checkout ? " and checked it out" : string.Empty;

        return Done(effect, $"Created branch {effect.Name} at {effect.Label}{suffix}");
    }

    private async Task<BranchCommand> RenameAsync(RenameBranchEffect effect, CancellationToken token)
    {
        await EnsureValidAsync(effect, effect.Name, token).ConfigureAwait(false);
        await writer.RenameAsync(effect.Location, effect.From, effect.Name, token).ConfigureAwait(false);

        return Done(effect, $"Renamed {effect.From} to {effect.Name}");
    }

    private async Task<BranchCommand> SwitchAsync(SwitchBranchEffect effect, CancellationToken token)
    {
        var target = effect.Target;
        switch (effect.TargetKind)
        {
            case SwitchTargetKind.LocalBranch:
                await writer.SwitchAsync(effect.Location, target, token).ConfigureAwait(false);

                return Done(effect, $"Switched to {target}");
            case SwitchTargetKind.RemoteBranch:
                await writer.SwitchToRemoteAsync(effect.Location, target, token).ConfigureAwait(false);
                var local = SwitchCandidates.LocalNameOf(target);

                return Done(effect, $"Switched to new branch {local} tracking {target}");
            default:
                await writer.SwitchDetachedAsync(effect.Location, target, token).ConfigureAwait(false);

                return Done(effect, $"HEAD is detached at {ShortHash.Of(target)}");
        }
    }

    // Without force, a branch with commits that neither HEAD nor its upstream reach is left alone and the user is
    // asked.
    private async Task<BranchCommand> DeleteAsync(DeleteBranchEffect effect, CancellationToken token)
    {
        if (!effect.Force)
        {
            var merged = await writer.IsMergedAsync(effect.Location, effect.Name, effect.UpstreamFullName, token)
                .ConfigureAwait(false);
            if (!merged)
            {
                return new BranchDeleteNeedsForce(effect.TabId, effect.Name, effect.CommitHash);
            }
        }

        await writer.DeleteAsync(effect.Location, effect.Name, effect.Force, token).ConfigureAwait(false);
        var text = $"Deleted branch {effect.Name} (was {ShortHash.Of(effect.CommitHash)}; restore: "
            + $"git branch {effect.Name} {effect.CommitHash})";

        return Done(effect, text);
    }

    private async Task<BranchCommand> SetUpstreamAsync(SetUpstreamEffect effect, CancellationToken token)
    {
        var upstream = $"{GitRefPrefixes.Remotes}{effect.Upstream}";
        await writer.SetUpstreamAsync(effect.Location, effect.Name, upstream, token).ConfigureAwait(false);

        return Done(effect, $"{effect.Name} now tracks {effect.Upstream}");
    }

    private async Task<BranchCommand> UnsetUpstreamAsync(UnsetUpstreamEffect effect, CancellationToken token)
    {
        await writer.UnsetUpstreamAsync(effect.Location, effect.Name, token).ConfigureAwait(false);
        var restore = $"git branch --set-upstream-to={effect.Upstream} {effect.Name}";

        return Done(effect, $"{effect.Name} no longer tracks {effect.Upstream} (restore: {restore})");
    }

    private async Task<BranchCommand> FastForwardAsync(FastForwardBranchEffect effect, CancellationToken token)
    {
        await writer.FastForwardAsync(effect.Location, effect.Name, effect.UpstreamFullName, token)
            .ConfigureAwait(false);
        var moved = $"from {ShortHash.Of(effect.OldHash)} to {ShortHash.Of(effect.NewHash)}";
        var text = $"Fast-forwarded {effect.Name} {moved} "
            + $"(undo: git update-ref {GitRefPrefixes.Heads}{effect.Name} {effect.OldHash})";

        return Done(effect, text);
    }

    private async Task EnsureValidAsync(BranchEffect effect, string name, CancellationToken token)
    {
        if (!await writer.IsValidNameAsync(effect.Location, name, token).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"'{name}' is not a valid branch name");
        }
    }

    private static BranchOperationSucceeded Done(BranchEffect effect, string text) =>
        new(effect.TabId, text, effect.Follow);

    private static string FailureOf(BranchEffect effect) => effect switch
    {
        CreateBranchEffect => "Could not create the branch",
        DeleteBranchEffect => "Could not delete",
        FastForwardBranchEffect => "Could not fast-forward",
        RenameBranchEffect => "Could not rename the branch",
        SetUpstreamEffect => "Could not set the upstream",
        SwitchBranchEffect { TargetKind: SwitchTargetKind.DetachedCommit } => "Could not check out",
        SwitchBranchEffect => "Could not switch",
        UnsetUpstreamEffect => "Could not unset the upstream",
        _ => throw new UnhandledEffectException(effect),
    };
}
