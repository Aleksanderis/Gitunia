using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Refs.Models;
using RemoteMatcher = Gitunia.Core.Search.FuzzyMatcher;

namespace Gitunia.Core.Tags;

/// <summary>
/// Create, delete and push tags, and delete one on a remote (ux-workflows.md, workflow 3, "Tags"), as pure transitions
/// <c>(TagState, TagCommand) -> (TagState, Effects)</c>. A name and an optional message are typed in prompt popups (an
/// empty message makes a lightweight tag), a remote is chosen in a type-to-filter prompt, and both deletes go through
/// the confirm dialog. Refusals that need no git are decided here from the tab refs. One operation runs per tab.
/// </summary>
public static class TagReducer
{
    /// <summary>Shown when a second operation is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: the last tag operation is still running";

    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";
    private const string SelectNotice = "Select a tag (sidebar) or a commit with one tag on it";

    private static readonly TagPrompt BlankPrompt = new(
        TagPromptKind.CreateName,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        [],
        [],
        0);

    /// <summary>Applies a command to the tag slice of a tab.</summary>
    public static TagUpdate Reduce(TagState state, TagCommand command, TagContext context) => command switch
    {
        TagCreateRequested create => Create(state, create.Target, context),
        TagDeleteConfirmed confirmed => DeleteConfirmed(state, confirmed, context),
        TagDeleteOnRemoteConfirmed confirmed => DeleteOnRemoteConfirmed(state, confirmed, context),
        TagDeleteOnRemoteRequested request => LoadRemotes(state, request.Target, false, context),
        TagDeleteRequested request => AskDelete(state, request.Target, context),
        TagOperationCancelled => Update(state),
        TagOperationFailed failed => Failed(state, failed, context),
        TagOperationSucceeded succeeded => Succeeded(state, succeeded, context),
        TagPromptAccepted => Accept(state, context),
        TagPromptCancelled => Close(state),
        TagPromptMoved moved => Move(state, moved.Step),
        TagPromptTextChanged changed => TextChanged(state, changed.Text),
        TagPushRequested request => LoadRemotes(state, request.Target, true, context),
        TagRemotesFailed failed => Update(state, Post(failed.Text, StatusSeverity.Error)),
        TagRemotesLoaded loaded => RemotesLoaded(state, loaded, context),
        _ => throw new UnhandledCommandException(command),
    };

    /// <summary>The effects a closed tab leaves behind: an open prompt held the key mode in Insert.</summary>
    public static ImmutableArray<AppEffect> OnTabClosed(TagState state) =>
        state.Prompt is null ? [] : [new SetKeyModeEffect(KeyMode.Normal)];

    private static TagUpdate Update(TagState state, params AppEffect[] effects) => new(state, [.. effects]);

    private static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static TagUpdate Create(TagState state, TagTarget target, TagContext context)
    {
        if (state.Prompt is not null)
        {
            return Update(state);
        }

        if (context.IsBusy)
        {
            return Update(state, Post(BusyNotice));
        }

        if (target.CommitHash is not { } hash)
        {
            return Update(state, Post("Select a commit or a ref first"));
        }

        var prompt = BlankPrompt with { StartPoint = hash, Label = target.Name ?? ShortHash.Of(hash) };
        return Open(state, prompt);
    }

    private static TagUpdate Open(TagState state, TagPrompt prompt) =>
        Update(state with { Prompt = prompt }, new SetKeyModeEffect(KeyMode.Insert));

    private static TagUpdate Close(TagState state)
    {
        if (state.Prompt is null)
        {
            return Update(state);
        }

        return Update(state with { Prompt = null }, new SetKeyModeEffect(KeyMode.Normal));
    }

    private static TagUpdate CloseWith(TagState state, params AppEffect[] effects)
    {
        var closed = Close(state);
        return new TagUpdate(closed.State, [.. closed.Effects, .. effects]);
    }

    private static TagUpdate Accept(TagState state, TagContext context)
    {
        if (state.Prompt is not { } prompt)
        {
            return Update(state);
        }

        return prompt.Kind switch
        {
            TagPromptKind.CreateName => AcceptName(state, prompt, context),
            TagPromptKind.CreateMessage => AcceptMessage(state, prompt, context),
            _ => AcceptRemote(state, prompt, context),
        };
    }

    private static TagUpdate AcceptName(TagState state, TagPrompt prompt, TagContext context)
    {
        var name = prompt.Text.Trim();
        if (name.Length == 0)
        {
            return CloseWith(state, Post("No name: nothing was created"));
        }

        if (context.Refs.Any(reference => reference.Kind == RefKind.Tag && reference.Name == name))
        {
            return CloseWith(state, Post($"Tag {name} already exists"));
        }

        var next = prompt with { Kind = TagPromptKind.CreateMessage, NewName = name, Text = string.Empty };
        return Open(state, next);
    }

    private static TagUpdate AcceptMessage(TagState state, TagPrompt prompt, TagContext context)
    {
        var effect = new CreateTagEffect(
            context.TabId,
            context.Location,
            prompt.NewName,
            prompt.StartPoint,
            prompt.Text.Trim(),
            prompt.Label);
        return CloseWith(state, effect);
    }

    private static TagUpdate AcceptRemote(TagState state, TagPrompt prompt, TagContext context)
    {
        if (!prompt.TryGetSelected(out var remote))
        {
            return Update(state, Post("No matching remote"));
        }

        if (prompt.Kind == TagPromptKind.PushRemote)
        {
            return CloseWith(
                state,
                new PushTagEffect(context.TabId, context.Location, remote, prompt.TagName));
        }

        var confirm = new ConfirmPrompt(
            "Delete tag on remote",
            [$"Delete the tag {prompt.TagName} on the remote {remote}?", "The local tag stays."],
            $"Undo: git push {remote} refs/tags/{prompt.TagName}",
            "Delete on remote");
        var accepted = new TagDeleteOnRemoteConfirmed(context.TabId, remote, prompt.TagName);
        return CloseWith(state, new ShowConfirmEffect(confirm, accepted, TooSmallNotice));
    }

    private static TagUpdate TextChanged(TagState state, string text)
    {
        if (state.Prompt is not { } prompt || prompt.Text == text)
        {
            return Update(state);
        }

        var edited = prompt with { Text = text };
        return Update(state with { Prompt = prompt.IsPicker ? Filtered(edited) : edited });
    }

    private static TagPrompt Filtered(TagPrompt prompt)
    {
        var picker = new FilterPicker<string>();
        picker.Filter(prompt.Remotes, prompt.Text, remote => remote, RemoteMatcher.Score);
        return prompt with { Candidates = [.. picker.Candidates], Index = 0 };
    }

    private static TagUpdate Move(TagState state, int step)
    {
        if (state.Prompt is not { IsPicker: true } prompt)
        {
            return Update(state);
        }

        var count = prompt.Candidates.Length;
        var index = count == 0 ? 0 : (((prompt.Index + step) % count) + count) % count;
        return Update(state with { Prompt = prompt with { Index = index } });
    }

    private static TagUpdate AskDelete(TagState state, TagTarget target, TagContext context)
    {
        if (state.Prompt is not null)
        {
            return Update(state);
        }

        if (context.IsBusy)
        {
            return Update(state, Post(BusyNotice));
        }

        if (ResolveTag(target, context.Refs, out var tag) is { } refusal)
        {
            return Update(state, Post(refusal));
        }

        var confirm = new ConfirmPrompt(
            "Delete tag",
            [
                $"Delete the local tag {tag!.Name} (at {ShortHash.Of(tag.CommitHash)})?",
                "A copy on a remote is not touched.",
            ],
            $"Undo: git tag {tag.Name} {tag.ObjectHash}",
            "Delete tag");
        var accepted = new TagDeleteConfirmed(context.TabId, tag.Name, tag.ObjectHash);
        return Update(state, new ShowConfirmEffect(confirm, accepted, TooSmallNotice));
    }

    private static TagUpdate DeleteConfirmed(TagState state, TagDeleteConfirmed confirmed, TagContext context)
    {
        if (context.IsBusy)
        {
            return Update(state, Post($"{BusyNotice}; nothing was deleted"));
        }

        var effect = new DeleteTagEffect(context.TabId, context.Location, confirmed.Name, confirmed.ObjectHash);
        return Update(state, effect);
    }

    private static TagUpdate DeleteOnRemoteConfirmed(
        TagState state,
        TagDeleteOnRemoteConfirmed confirmed,
        TagContext context)
    {
        if (context.IsBusy)
        {
            return Update(state, Post($"{BusyNotice}; nothing was deleted"));
        }

        var effect = new DeleteTagOnRemoteEffect(context.TabId, context.Location, confirmed.Remote, confirmed.Name);
        return Update(state, effect);
    }

    private static TagUpdate LoadRemotes(TagState state, TagTarget target, bool push, TagContext context)
    {
        if (state.Prompt is not null)
        {
            return Update(state);
        }

        if (context.IsBusy)
        {
            return Update(state, Post(BusyNotice));
        }

        if (ResolveTag(target, context.Refs, out var tag) is { } refusal)
        {
            return Update(state, Post(refusal));
        }

        var effect = new ListTagRemotesEffect(context.TabId, context.Location, tag!.Name, push);
        return Update(state, effect);
    }

    private static TagUpdate RemotesLoaded(TagState state, TagRemotesLoaded loaded, TagContext context)
    {
        var idle = state;
        if (loaded.Remotes.Length == 0)
        {
            return Update(idle, Post("There is no remote: add one first"));
        }

        if (!context.IsActive || state.Prompt is not null)
        {
            return Update(idle);
        }

        var kind = loaded.Push ? TagPromptKind.PushRemote : TagPromptKind.DeleteRemote;
        var prompt = Filtered(BlankPrompt with { Kind = kind, TagName = loaded.Name, Remotes = loaded.Remotes });
        return Open(idle, prompt);
    }

    private static TagUpdate Succeeded(TagState state, TagOperationSucceeded succeeded, TagContext context) =>
        Update(
            state,
            Post(succeeded.Text, StatusSeverity.Info),
            new RefreshTabEffect(context.TabId));

    private static TagUpdate Failed(TagState state, TagOperationFailed failed, TagContext context)
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

        return new TagUpdate(state, [.. effects]);
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
