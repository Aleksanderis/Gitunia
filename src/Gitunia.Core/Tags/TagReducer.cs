using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Tags;

/// <summary>
/// Create, delete and push tags, and delete one on a remote (ux-workflows.md, workflow 3, "Tags"), as pure transitions
/// <c>(TagCommand, TagContext) -> Effects</c>. A name and an optional message are typed in prompts (an empty message
/// makes a lightweight tag), a remote is chosen in a picker prompt, and both deletes go through the confirm dialog. The
/// prompts are the shared prompt model (<see cref="ShowPromptEffect"/>, #971): the feature keeps no prompt state, only
/// the results it is given back. Refusals that need no git are decided here from the tab refs. One operation runs per
/// tab.
/// </summary>
public static class TagReducer
{
    /// <summary>Shown when a second operation is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: the last tag operation is still running";

    /// <summary>The row (and the notice on Enter) of the remote picker when nothing matches.</summary>
    public const string NoRemoteMatch = "No matching remote";

    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";
    private const string SelectNotice = "Select a tag (sidebar) or a commit with one tag on it";

    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(TagCommand command, TagContext context) => command switch
    {
        TagCreateRequested create => Create(create.Target, context),
        TagDeleteConfirmed confirmed => DeleteConfirmed(confirmed, context),
        TagDeleteOnRemoteConfirmed confirmed => DeleteOnRemoteConfirmed(confirmed, context),
        TagDeleteOnRemoteRequested request => LoadRemotes(request.Target, false, context),
        TagDeleteRequested request => AskDelete(request.Target, context),
        TagMessageAccepted accepted => MessageAccepted(accepted, context),
        TagNameAccepted accepted => NameAccepted(accepted, context),
        TagOperationCancelled => [],
        TagOperationFailed failed => Failed(failed, context),
        TagOperationSucceeded succeeded => Succeeded(succeeded, context),
        TagPushRequested request => LoadRemotes(request.Target, true, context),
        TagRemoteChosen chosen => RemoteChosen(chosen, context),
        TagRemotesFailed failed => [Post(failed.Text, StatusSeverity.Error)],
        TagRemotesLoaded loaded => RemotesLoaded(loaded, context),
        _ => throw new UnhandledCommandException(command),
    };

    private static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static ImmutableArray<AppEffect> Create(TagTarget target, TagContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        if (target.CommitHash is not { } hash)
        {
            return [Post("Select a commit or a ref first")];
        }

        var label = target.Name ?? ShortHash.Of(hash);
        var request = new PromptRequest(
            "New tag",
            $"At {label}",
            "next",
            new TagNameAnswer(context.TabId, hash, label),
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> NameAccepted(TagNameAccepted accepted, TagContext context)
    {
        var name = accepted.Name.Trim();
        if (name.Length == 0)
        {
            return [Post("No name: nothing was created")];
        }

        if (context.Refs.Any(reference => reference.Kind == RefKind.Tag && reference.Name == name))
        {
            return [Post($"Tag {name} already exists")];
        }

        var request = new PromptRequest(
            "Tag message",
            $"{name}: empty makes a lightweight tag",
            "create",
            new TagMessageAnswer(context.TabId, accepted.StartPoint, accepted.Label, name),
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> MessageAccepted(TagMessageAccepted accepted, TagContext context)
    {
        var effect = new CreateTagEffect(
            context.TabId,
            context.Location,
            accepted.Name,
            accepted.StartPoint,
            accepted.Message.Trim(),
            accepted.Label);

        return [effect];
    }

    private static ImmutableArray<AppEffect> RemoteChosen(TagRemoteChosen chosen, TagContext context)
    {
        if (chosen.Push)
        {
            return [new PushTagEffect(context.TabId, context.Location, chosen.Remote, chosen.TagName)];
        }

        var confirm = new ConfirmPrompt(
            "Delete tag on remote",
            [$"Delete the tag {chosen.TagName} on the remote {chosen.Remote}?", "The local tag stays."],
            UndoNotes.FromHistory,
            "Delete on remote");
        var accepted = new TagDeleteOnRemoteConfirmed(context.TabId, chosen.Remote, chosen.TagName);

        return [new ShowConfirmEffect(confirm, accepted, TooSmallNotice)];
    }

    private static ImmutableArray<AppEffect> AskDelete(TagTarget target, TagContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        if (ResolveTag(target, context.Refs, out var tag) is { } refusal)
        {
            return [Post(refusal)];
        }

        var confirm = new ConfirmPrompt(
            "Delete tag",
            [
                $"Delete the local tag {tag!.Name} (at {ShortHash.Of(tag.CommitHash)})?",
                "A copy on a remote is not touched.",
            ],
            UndoNotes.FromHistory,
            "Delete tag");
        var accepted = new TagDeleteConfirmed(context.TabId, tag.Name, tag.ObjectHash);

        return [new ShowConfirmEffect(confirm, accepted, TooSmallNotice)];
    }

    private static ImmutableArray<AppEffect> DeleteConfirmed(TagDeleteConfirmed confirmed, TagContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was deleted")];
        }

        return [new DeleteTagEffect(context.TabId, context.Location, confirmed.Name, confirmed.ObjectHash)];
    }

    private static ImmutableArray<AppEffect> DeleteOnRemoteConfirmed(
        TagDeleteOnRemoteConfirmed confirmed,
        TagContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was deleted")];
        }

        return [new DeleteTagOnRemoteEffect(context.TabId, context.Location, confirmed.Remote, confirmed.Name)];
    }

    private static ImmutableArray<AppEffect> LoadRemotes(TagTarget target, bool push, TagContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        if (ResolveTag(target, context.Refs, out var tag) is { } refusal)
        {
            return [Post(refusal)];
        }

        return [new ListTagRemotesEffect(context.TabId, context.Location, tag!.Name, push)];
    }

    private static ImmutableArray<AppEffect> RemotesLoaded(TagRemotesLoaded loaded, TagContext context)
    {
        if (loaded.Remotes.Length == 0)
        {
            return [Post("There is no remote: add one first")];
        }

        if (!context.IsActive)
        {
            return [];
        }

        var title = loaded.Push ? "Push tag" : "Delete tag on remote";
        var verb = loaded.Push ? "push" : "delete";
        var preposition = loaded.Push ? "to" : "from";
        var request = new PromptRequest(
            title,
            $"Remote to {verb} {loaded.Name} {preposition}",
            "choose",
            new TagRemoteAnswer(context.TabId, loaded.Name, loaded.Push),
            Choices: [.. loaded.Remotes.Select(remote => new PromptChoice(remote, remote))],
            NoMatch: NoRemoteMatch,
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> Succeeded(TagOperationSucceeded succeeded, TagContext context) =>
        [
            new PostStatusEffect(succeeded.Text, StatusSeverity.Info, succeeded.Detail),
            new RefreshTabEffect(context.TabId),
        ];

    private static ImmutableArray<AppEffect> Failed(TagOperationFailed failed, TagContext context)
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

        return [.. effects];
    }

    // Finds the local tag a delete or a push acts on, or returns why there is none.
    private static string? ResolveTag(TagTarget target, ImmutableArray<GitRef> refs, out GitRef? tag)
    {
        tag = null;
        var tags = refs.Where(reference => reference.Kind == RefKind.Tag).ToList();
        if (target.RowKind is not null)
        {
            if (target.RowKind == SidebarRowKind.Tag)
            {
                tag = tags.FirstOrDefault(reference => reference.Name == target.Name);
            }

            return tag is null ? "Select a tag first" : null;
        }

        var here = new List<GitRef>();
        if (target.CommitHash is not null)
        {
            here = [.. tags.Where(reference => reference.CommitHash == target.CommitHash)];
        }

        if (here.Count > 1)
        {
            return $"{here.Count} tags point here: pick one in the sidebar";
        }

        tag = here.FirstOrDefault();
        return tag is null ? SelectNotice : null;
    }
}
