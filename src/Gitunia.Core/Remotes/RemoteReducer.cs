using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Remotes;

/// <summary>
/// Add, remove, rename and re-point remotes (ux-workflows.md, workflow 5, "Managing remotes"), as pure transitions
/// <c>(RemoteCommand, RemoteContext) -> Effects</c>. The remotes are read first so a duplicate name is refused before
/// git runs. Names and URLs are typed in prompts, the remote is chosen in a picker prompt (or taken from a selected
/// remote branch row), and the removal goes through the confirm dialog, which names the remote-tracking branches that
/// disappear. The prompts are the shared prompt model (<see cref="ShowPromptEffect"/>, #971). Nothing is fetched. One
/// operation runs per tab.
/// </summary>
public static class RemoteReducer
{
    /// <summary>Shown when a second operation is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: the last remote change is still running";

    /// <summary>The row (and the notice on Enter) of the remote picker when nothing matches.</summary>
    public const string NoRemoteMatch = "No matching remote";

    private const int ListedBranches = 5;
    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";
    private const string NoRemoteNotice = "There is no remote: add one first";
    private const char RemoteSeparator = '/';

    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(RemoteCommand command, RemoteContext context) => command switch
    {
        RemoteActionRequested request => Requested(request, context),
        RemoteAddNameAccepted accepted => AddNameAccepted(accepted, context),
        RemoteAddUrlAccepted accepted => AddUrlAccepted(accepted, context),
        RemoteChosen chosen => Chosen(chosen, context),
        RemoteOperationCancelled => [],
        RemoteOperationFailed failed => Failed(failed, context),
        RemoteOperationSucceeded succeeded => Succeeded(succeeded, context),
        RemoteRemoveConfirmed confirmed => RemoveConfirmed(confirmed, context),
        RemoteRenameAccepted accepted => RenameAccepted(accepted, context),
        RemotesListed listed => Listed(listed, context),
        RemotesListFailed failed => [Post(failed.Text, StatusSeverity.Error)],
        RemoteUrlAccepted accepted => UrlAccepted(accepted, context),
        _ => throw new UnhandledCommandException(command),
    };

    private static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static ImmutableArray<AppEffect> Requested(RemoteActionRequested request, RemoteContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        return [new ListRemotesEffect(context.TabId, context.Location, request.Action, request.Target)];
    }

    private static ImmutableArray<AppEffect> Listed(RemotesListed listed, RemoteContext context)
    {
        if (!context.IsActive)
        {
            return [];
        }

        if (listed.Action == RemoteAction.Add)
        {
            var request = new PromptRequest(
                "Add remote",
                "Name of the new remote",
                "next",
                new RemoteAddNameAnswer(context.TabId, listed.Remotes),
                Owner: context.TabId);

            return [new ShowPromptEffect(request)];
        }

        if (listed.Remotes.Length == 0)
        {
            return [Post(NoRemoteNotice)];
        }

        if (RemoteOfBranch(listed.Remotes, listed.Target) is { } selected)
        {
            return AfterPick(listed.Action, selected, listed.Remotes, context);
        }

        return [new ShowPromptEffect(PickerRequest(listed, context))];
    }

    private static PromptRequest PickerRequest(RemotesListed listed, RemoteContext context)
    {
        var (title, text) = listed.Action switch
        {
            RemoteAction.Remove => ("Remove remote", "Remote to remove"),
            RemoteAction.Rename => ("Rename remote", "Remote to rename"),
            _ => ("Set remote URL", "Remote to change"),
        };

        return new PromptRequest(
            title,
            text,
            "choose",
            new RemotePickAnswer(context.TabId, listed.Action, listed.Remotes),
            Choices: [.. listed.Remotes.Select(remote => new PromptChoice(remote.Name, remote.Name))],
            NoMatch: NoRemoteMatch,
            Owner: context.TabId);
    }

    private static ImmutableArray<AppEffect> Chosen(RemoteChosen chosen, RemoteContext context)
    {
        var remote = chosen.Remotes.FirstOrDefault(candidate => candidate.Name == chosen.Name);
        if (remote is null)
        {
            return [];
        }

        return AfterPick(chosen.Action, remote, chosen.Remotes, context);
    }

    // The remote is fixed; remove asks, rename and set-url type what comes next.
    private static ImmutableArray<AppEffect> AfterPick(
        RemoteAction action,
        RemoteInfo remote,
        ImmutableArray<RemoteInfo> remotes,
        RemoteContext context)
    {
        if (action == RemoteAction.Remove)
        {
            return AskRemove(remote, remotes, context);
        }

        if (action == RemoteAction.Rename)
        {
            var rename = new PromptRequest(
                "Rename remote",
                $"New name for {remote.Name}",
                "rename",
                new RemoteRenameAnswer(context.TabId, remotes, remote.Name),
                Owner: context.TabId);

            return [new ShowPromptEffect(rename)];
        }

        var request = new PromptRequest(
            "Set remote URL",
            $"New URL of {remote.Name}",
            "set",
            new RemoteUrlAnswer(context.TabId, remote.Name, remote.Url),
            InitialText: remote.Url,
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> AddNameAccepted(RemoteAddNameAccepted accepted, RemoteContext context)
    {
        var name = accepted.Name.Trim();
        if (name.Length == 0)
        {
            return [Post("No name: nothing was added")];
        }

        if (accepted.Remotes.Any(remote => remote.Name == name))
        {
            return [Post($"Remote {name} already exists")];
        }

        var request = new PromptRequest(
            "Add remote",
            $"URL of {name}",
            "add",
            new RemoteAddUrlAnswer(context.TabId, name),
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> AddUrlAccepted(RemoteAddUrlAccepted accepted, RemoteContext context)
    {
        var url = accepted.Url.Trim();
        if (url.Length == 0)
        {
            return [Post("No URL: nothing was added")];
        }

        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was added")];
        }

        return [new AddRemoteEffect(context.TabId, context.Location, accepted.Name, url)];
    }

    private static ImmutableArray<AppEffect> RenameAccepted(RemoteRenameAccepted accepted, RemoteContext context)
    {
        var newName = accepted.NewName.Trim();
        if (newName.Length == 0 || newName == accepted.OldName)
        {
            return [Post("Same or empty name: nothing was renamed")];
        }

        if (accepted.Remotes.Any(remote => remote.Name == newName))
        {
            return [Post($"Remote {newName} already exists")];
        }

        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was renamed")];
        }

        return [new RenameRemoteEffect(context.TabId, context.Location, accepted.OldName, newName)];
    }

    private static ImmutableArray<AppEffect> UrlAccepted(RemoteUrlAccepted accepted, RemoteContext context)
    {
        var url = accepted.Url.Trim();
        if (url.Length == 0 || url == accepted.OldUrl)
        {
            return [Post("Same or empty URL: nothing was changed")];
        }

        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was changed")];
        }

        return [new SetRemoteUrlEffect(context.TabId, context.Location, accepted.Name, accepted.OldUrl, url)];
    }

    private static ImmutableArray<AppEffect> AskRemove(
        RemoteInfo remote,
        ImmutableArray<RemoteInfo> remotes,
        RemoteContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        var branches = TrackingBranches(context.Refs, remotes, remote);
        var lines = new List<string> { $"Remove the remote {remote.Name} ({remote.Url})?" };
        if (branches.Count == 0)
        {
            lines.Add("It has no remote-tracking branches.");
        }
        else
        {
            var gone = $"{branches.Count} remote-tracking branches disappear";
            if (branches.Count == 1)
            {
                gone = "1 remote-tracking branch disappears";
            }

            lines.Add($"{gone}: {BranchList(branches)}.");
        }

        lines.Add("Branches that track it lose their upstream. The remote itself is not touched.");
        var confirm = new ConfirmPrompt(
            "Remove remote",
            lines,
            $"Undo: git remote add {remote.Name} {remote.Url}, then fetch it",
            "Remove remote");
        var accepted = new RemoteRemoveConfirmed(context.TabId, remote.Name, remote.Url);

        return [new ShowConfirmEffect(confirm, accepted, TooSmallNotice)];
    }

    private static ImmutableArray<AppEffect> RemoveConfirmed(RemoteRemoveConfirmed confirmed, RemoteContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was removed")];
        }

        return [new RemoveRemoteEffect(context.TabId, context.Location, confirmed.Name, confirmed.Url)];
    }

    private static ImmutableArray<AppEffect> Succeeded(RemoteOperationSucceeded succeeded, RemoteContext context) =>
        [Post(succeeded.Text, StatusSeverity.Info), new RefreshTabEffect(context.TabId)];

    private static ImmutableArray<AppEffect> Failed(RemoteOperationFailed failed, RemoteContext context) =>
        [Post(failed.Text, StatusSeverity.Error), new RefreshTabEffect(context.TabId)];

    // The remote a selected remote branch row belongs to; the longest name wins when one remote name prefixes another.
    private static RemoteInfo? RemoteOfBranch(ImmutableArray<RemoteInfo> remotes, RemoteTarget target)
    {
        if (target.RowKind != SidebarRowKind.RemoteBranch || target.Name is not { } name)
        {
            return null;
        }

        return remotes
            .Where(remote => name.StartsWith($"{remote.Name}{RemoteSeparator}", StringComparison.Ordinal))
            .OrderByDescending(remote => remote.Name.Length)
            .FirstOrDefault();
    }

    // The remote-tracking branches of a remote, not those of a remote whose name extends it (a and a/b).
    private static List<string> TrackingBranches(
        ImmutableArray<GitRef> refs,
        ImmutableArray<RemoteInfo> remotes,
        RemoteInfo remote)
    {
        var prefix = $"{remote.Name}{RemoteSeparator}";
        var longer = remotes
            .Where(other => other.Name.Length > remote.Name.Length
                && other.Name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(other => $"{other.Name}{RemoteSeparator}")
            .ToList();

        return
        [
            .. refs
                .Where(reference => reference.Kind == RefKind.RemoteBranch && reference.SymbolicTarget is null)
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)
                    && !longer.Any(other => name.StartsWith(other, StringComparison.Ordinal)))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string BranchList(List<string> branches)
    {
        var shown = string.Join(", ", branches.Take(ListedBranches));
        return branches.Count > ListedBranches ? $"{shown} and {branches.Count - ListedBranches} more" : shown;
    }
}
