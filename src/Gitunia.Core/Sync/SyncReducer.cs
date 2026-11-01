using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Sync;

/// <summary>
/// Fetch, fetch of all remotes, pull and push of the tab's current branch, the cancel of the one that runs, the push
/// cases that need a question and the delete of a remote branch (ux-workflows.md, workflow 5), as pure transitions
/// <c>(SyncCommand, SyncContext) -> (SyncState, Effects)</c>. One such operation runs per tab; the progress line is the
/// <see cref="SyncState"/> of the tab, not status messages, so the message history stays clean. A force push is only
/// ever a lease and goes through a confirm that names the remote branch it overwrites. Prompts and confirms are the
/// shared prompt model; the question flows live in <see cref="SyncPushQuestions"/> and <see cref="SyncRemoteDelete"/>.
/// </summary>
public static class SyncReducer
{
    /// <summary>Shown when a second fetch, pull or push is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: a fetch, pull or push of this tab is still running";

    /// <summary>Shown when a pull is asked for while an operation of another kind runs.</summary>
    public const string OtherBusyNotice = "Busy: another operation of this tab is still running";

    /// <summary>Shown when the cancel action runs with nothing to cancel.</summary>
    public const string NothingRunningNotice = "No fetch, pull or push is running";

    /// <summary>The row (and the notice on Enter) of the pickers when nothing matches.</summary>
    public const string NoMatch = "No match";

    /// <summary>Shown when a confirm cannot be displayed (the screen is too small).</summary>
    public const string TooSmallNotice = "The screen is too small to ask: nothing was changed";

    private const string PlainPushDone = "Pushed the current branch";

    /// <summary>Applies a command and returns the new state of the tab's transfer and the effects to run.</summary>
    public static SyncUpdate Reduce(SyncCommand command, SyncContext context) => command switch
    {
        SyncBehindChecked checkedBehind => Keep(context, SyncPushQuestions.BehindChecked(checkedBehind, context)),
        SyncCancelled => Cancelled(context),
        SyncCancelRequested => Cancel(context),
        SyncDeleteBranchChosen chosen => Keep(context, SyncRemoteDelete.BranchChosen(chosen, context)),
        SyncDeleteConfirmed confirmed => Keep(context, SyncRemoteDelete.Confirmed(confirmed, context)),
        SyncDeleteRemoteChosen chosen => Keep(context, SyncRemoteDelete.RemoteChosen(chosen, context)),
        SyncDeleteRequested request => Keep(context, SyncRemoteDelete.Requested(request, context)),
        SyncFailed failed => Failed(failed, context),
        SyncForceConfirmed confirmed => ForceConfirmed(confirmed, context),
        SyncListFailed failed => Ended([Post(failed.Text, StatusSeverity.Error)]),
        SyncProgressed progressed => Progressed(progressed, context),
        SyncRemoteChosen chosen => RemoteChosen(chosen, context),
        SyncRemotesListed listed => Listed(listed, context),
        SyncRequested request => Requested(request.Kind, context),
        SyncSucceeded succeeded => Ended([Post(succeeded.Text, StatusSeverity.Info), new RefreshTabEffect(context.TabId)]),
        _ => throw new UnhandledCommandException(command),
    };

    /// <summary>A status message of the given severity.</summary>
    internal static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    /// <summary>The local branch that is checked out, null when HEAD is detached or nothing is loaded.</summary>
    internal static GitRef? CurrentBranch(ImmutableArray<GitRef> refs) =>
        refs.FirstOrDefault(reference => reference.Kind == RefKind.LocalBranch && reference.IsCurrent);

    /// <summary>Whether the branch tracks a remote branch that still exists.</summary>
    internal static bool HasUpstream(GitRef branch) => branch.UpstreamFullName is not null && !branch.IsUpstreamGone;

    private static SyncUpdate Keep(SyncContext context, ImmutableArray<AppEffect> effects) =>
        new(context.State, effects);

    private static SyncUpdate Ended(ImmutableArray<AppEffect> effects) => new(null, effects);

    private static SyncUpdate Requested(SyncKind kind, SyncContext context)
    {
        if (context.IsBusy)
        {
            return Keep(context, [Post(BusyNotice)]);
        }

        var current = CurrentBranch(context.Refs);
        switch (kind)
        {
            case SyncKind.Fetch:
                return Transfer(new FetchEffect(context.TabId, context.Location, false), "Fetching", "Fetch");
            case SyncKind.FetchAll:
                return Transfer(new FetchEffect(context.TabId, context.Location, true), "Fetching", "Fetch");
            case SyncKind.Pull when context.IsOtherBusy:
                return Keep(context, [Post(OtherBusyNotice)]);
            case SyncKind.Pull:
                return Transfer(new PullEffect(context.TabId, context.Location), "Pulling", "Pull");
            case SyncKind.PushForce:
                return Keep(context, SyncPushQuestions.ForceRequested(current, context));
            default:
                break;
        }

        // A branch without an upstream asks for the remote first; everything else is the plain push.
        if (current is not null && !HasUpstream(current))
        {
            return Keep(context, SyncPushQuestions.ListRemotes(SyncRemotesPurpose.FirstPush, current.Name, context));
        }

        var plain = new PushEffect(
            context.TabId,
            context.Location,
            new PushRequest(null),
            PlainPushDone,
            current?.Name ?? string.Empty);

        return Transfer(plain, "Pushing", "Push");
    }

    internal static SyncUpdate Transfer(SyncEffect effect, string activity, string noun) =>
        new(new SyncState(activity, noun, null, false), [effect]);

    private static SyncUpdate Cancel(SyncContext context)
    {
        if (context.State is not { } state)
        {
            return Keep(context, [Post(NothingRunningNotice)]);
        }

        return new SyncUpdate(state with { IsCancelling = true }, [new CancelSyncEffect(context.TabId)]);
    }

    private static SyncUpdate Cancelled(SyncContext context)
    {
        // A cancel by the user is reported; a closed tab has nobody to tell (the command is dropped before this).
        var effects = new List<AppEffect>();
        if (context.State is { IsCancelling: true } state)
        {
            effects.Add(Post($"{state.Noun} cancelled"));
        }

        effects.Add(new RefreshTabEffect(context.TabId));

        return Ended([.. effects]);
    }

    private static SyncUpdate Progressed(SyncProgressed progressed, SyncContext context)
    {
        if (context.State is not { } state)
        {
            return Keep(context, []);
        }

        return new SyncUpdate(state with { Latest = progressed.Report }, []);
    }

    private static SyncUpdate Failed(SyncFailed failed, SyncContext context)
    {
        var effects = new List<AppEffect>
        {
            Post(failed.Text, StatusSeverity.Error),
            new RefreshTabEffect(context.TabId),
        };
        if (failed.Retry is { } retry)
        {
            effects.Add(new OfferTerminalRetryEffect(context.TabId, retry.Noun, retry.Arguments));
        }
        else if (failed.Rejection is { } rejection)
        {
            effects.AddRange(SyncPushQuestions.OfferForce(rejection, context));
        }

        return Ended([.. effects]);
    }

    private static SyncUpdate Listed(SyncRemotesListed listed, SyncContext context)
    {
        if (!context.IsActive)
        {
            return Ended([]);
        }

        if (listed.Remotes.IsEmpty)
        {
            return Ended([Post("There is no remote: add one first")]);
        }

        return listed.Purpose switch
        {
            SyncRemotesPurpose.FirstPush => SyncPushQuestions.FirstPushRemotes(listed, context),
            SyncRemotesPurpose.Force => Ended(SyncPushQuestions.AskForce(listed, context)),
            _ => Ended(SyncRemoteDelete.Listed(listed, context)),
        };
    }

    private static SyncUpdate RemoteChosen(SyncRemoteChosen chosen, SyncContext context)
    {
        if (context.IsBusy)
        {
            return Keep(context, [Post($"{BusyNotice}; nothing was pushed")]);
        }

        return SyncPushQuestions.StartFirstPush(chosen.Branch, chosen.Remote, context);
    }

    private static SyncUpdate ForceConfirmed(SyncForceConfirmed confirmed, SyncContext context)
    {
        if (context.IsBusy)
        {
            return Keep(context, [Post($"{BusyNotice}; nothing was pushed")]);
        }

        var branch = CurrentBranch(context.Refs)?.Name ?? string.Empty;
        var effect = new PushEffect(context.TabId, context.Location, confirmed.Request, confirmed.Done, branch);

        return Transfer(effect, "Pushing", "Push");
    }
}
