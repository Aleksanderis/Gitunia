using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Core.Stash;

/// <summary>
/// Push, apply, pop and drop stashes and restore one file of a shown stash (ux-workflows.md, workflow 7), as pure
/// transitions <c>(StashCommand, StashContext) -> Effects</c>. Push asks for an optional message in the shared prompt
/// model (untracked files only through their own key); drop and a restore that would replace local content ask first
/// and name the undo. The target travels as the stash commit the user saw, so a stack that changed meanwhile never
/// redirects it. One stash operation runs per tab.
/// </summary>
public static class StashReducer
{
    /// <summary>Shown when a second operation is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: another operation of this tab is still running";

    /// <summary>Shown when apply, pop or drop has no stash row under the cursor.</summary>
    public const string NoStashNotice = "Select a stash in the sidebar first";

    /// <summary>Shown when the restore has no stash file under the cursor.</summary>
    public const string NoFileNotice =
        "Select a file of a shown stash first (Enter on the stash, Ctrl-j to its files)";

    private const string TooSmallNoRestoreNotice = "The screen is too small to ask: nothing was restored";
    private const string TooSmallNoDropNotice = "The screen is too small to ask: nothing was dropped";

    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(StashCommand command, StashContext context) => command switch
    {
        StashApplyRequested apply => Apply(apply, context),
        StashDropConfirmed confirmed => DropConfirmed(confirmed, context),
        StashDropRequested drop => AskDrop(drop, context),
        StashMessageAccepted accepted => MessageAccepted(accepted, context),
        StashOperationCancelled => [],
        StashOperationFailed failed => Finished(failed.Text, StatusSeverity.Error, context),
        StashOperationSucceeded succeeded =>
            Finished(succeeded.Text, succeeded.Severity, context, succeeded.Detail),
        StashPushRequested push => Push(push, context),
        StashRestoreChecked restoreChecked => RestoreChecked(restoreChecked, context),
        StashRestoreConfirmed confirmed => RestoreConfirmed(confirmed, context),
        StashRestoreFileRequested restore => Restore(restore, context),
        _ => throw new UnhandledCommandException(command),
    };

    private static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static ImmutableArray<AppEffect> Push(StashPushRequested push, StashContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        var kind = push.IncludeUntracked ? "Tracked and untracked files" : "Tracked changes";
        var request = new PromptRequest(
            "Stash changes",
            $"{kind}, message optional",
            "stash",
            new StashMessageAnswer(context.TabId, push.IncludeUntracked),
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> MessageAccepted(StashMessageAccepted accepted, StashContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        var effect = new PushStashEffect(
            context.TabId,
            context.Location,
            accepted.Message.Trim(),
            accepted.IncludeUntracked);

        return [effect];
    }

    private static ImmutableArray<AppEffect> Apply(StashApplyRequested apply, StashContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        if (EntryOf(apply.CommitHash, context) is not { } entry)
        {
            return [Post(NoStashNotice)];
        }

        return [new ApplyStashEffect(context.TabId, context.Location, entry, apply.Pop)];
    }

    private static ImmutableArray<AppEffect> AskDrop(StashDropRequested drop, StashContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        if (EntryOf(drop.CommitHash, context) is not { } entry)
        {
            return [Post(NoStashNotice)];
        }

        var confirm = new ConfirmPrompt(
            "Drop stash",
            [$"Drop {entry.Name}: {entry.Message}?"],
            UndoNotes.FromHistory,
            "Drop stash");
        var accepted = new StashDropConfirmed(context.TabId, entry);

        return [new ShowConfirmEffect(confirm, accepted, TooSmallNoDropNotice)];
    }

    private static ImmutableArray<AppEffect> DropConfirmed(StashDropConfirmed confirmed, StashContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was dropped")];
        }

        return [new DropStashEffect(context.TabId, context.Location, confirmed.Entry)];
    }

    private static ImmutableArray<AppEffect> Restore(StashRestoreFileRequested restore, StashContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        if (restore.Target is not { } target)
        {
            return [Post(NoFileNotice)];
        }

        return [new CheckStashRestoreEffect(context.TabId, context.Location, target)];
    }

    private static ImmutableArray<AppEffect> RestoreChecked(StashRestoreChecked restoreChecked, StashContext context)
    {
        var target = restoreChecked.Target;
        if (restoreChecked.State == StashRestoreState.Creates)
        {
            return [new RestoreStashFileEffect(context.TabId, context.Location, target)];
        }

        if (restoreChecked.State != StashRestoreState.Overwrites)
        {
            var report = StashReports.Restore(new StashRestoreResult(restoreChecked.State, null), target);
            return [Post(report.Text, report.Severity)];
        }

        var confirm = new ConfirmPrompt(
            "Restore file from stash",
            [
                $"Replace {target.File.Change.Path} in the working tree with its version from {target.Entry.Name}?",
                "Your current content of the file is replaced; the stash and the index stay as they are.",
            ],
            $"Your current content is saved first; afterwards {UndoNotes.MessageHistory} shows how to get it back.",
            "Restore file");
        var accepted = new StashRestoreConfirmed(context.TabId, target);

        return [new ShowConfirmEffect(confirm, accepted, TooSmallNoRestoreNotice)];
    }

    private static ImmutableArray<AppEffect> RestoreConfirmed(StashRestoreConfirmed confirmed, StashContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was restored")];
        }

        return [new RestoreStashFileEffect(context.TabId, context.Location, confirmed.Target)];
    }

    private static ImmutableArray<AppEffect> Finished(
        string text,
        StatusSeverity severity,
        StashContext context,
        string? detail = null) =>
        [new PostStatusEffect(text, severity, detail), new RefreshTabEffect(context.TabId)];

    // Apply, pop and drop act only on a stash row of the focused sidebar; the stash is found by its commit.
    private static StashEntry? EntryOf(string? commitHash, StashContext context)
    {
        if (context.FocusedPane != PaneKind.Sidebar || commitHash is null)
        {
            return null;
        }

        return context.Stashes.FirstOrDefault(entry => entry.CommitHash == commitHash);
    }
}
