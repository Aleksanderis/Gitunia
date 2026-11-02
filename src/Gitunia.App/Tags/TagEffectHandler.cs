using Gitunia.Core.Tags;
using Gitunia.Core.Text;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Tags;

namespace Gitunia.App.Tags;

/// <summary>
/// Runs the tag effects off the UI thread and posts exactly one result command for each: success with the text for the
/// status bar, failure with git's first error line (and the terminal retry when a remote call lacked credentials), or
/// cancellation. It never reads or changes state. Timeouts stay in the git layer.
/// </summary>
/// <param name="writer">Runs the tag commands.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class TagEffectHandler(ITagWriter writer, IDispatcher dispatcher)
{
    private const string TagPushNoun = "Tag push";
    private const string TagDeleteNoun = "Remote tag delete";

    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(TagEffect effect, CancellationToken cancellationToken)
    {
        switch (effect)
        {
            case CreateTagEffect create:
                Start(effect, token => CreateAsync(create, token), cancellationToken);
                break;
            case DeleteTagEffect delete:
                Start(effect, token => DeleteAsync(delete, token), cancellationToken);
                break;
            case DeleteTagOnRemoteEffect deleteOnRemote:
                Start(effect, token => DeleteOnRemoteAsync(deleteOnRemote, token), cancellationToken);
                break;
            case ListTagRemotesEffect list:
                Start(effect, token => ListRemotesAsync(list, token), cancellationToken);
                break;
            case PushTagEffect push:
                Start(effect, token => PushAsync(push, token), cancellationToken);
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private void Start(TagEffect effect, Func<CancellationToken, Task<TagCommand>> work, CancellationToken token) =>
        EffectWork.Start(
            dispatcher,
            work,
            () => new TagOperationCancelled(effect.TabId),
            exception => Failure(effect, exception),
            token);

    private async Task<TagCommand> CreateAsync(CreateTagEffect effect, CancellationToken token)
    {
        if (!await writer.IsValidNameAsync(effect.Location, effect.Name, token).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"'{effect.Name}' is not a valid tag name");
        }

        await writer.CreateAsync(effect.Location, effect.Name, effect.StartPoint, effect.Message, token)
            .ConfigureAwait(false);
        var kind = effect.Message.Length == 0 ? "lightweight tag" : "annotated tag";
        return new TagOperationSucceeded(effect.TabId, $"Created {kind} {effect.Name} at {effect.Label}");
    }

    private async Task<TagCommand> DeleteAsync(DeleteTagEffect effect, CancellationToken token)
    {
        await writer.DeleteAsync(effect.Location, effect.Name, token).ConfigureAwait(false);
        var text = $"Deleted tag {effect.Name} (was {ShortHash.Of(effect.ObjectHash)}; "
            + $"restore: git tag {effect.Name} {effect.ObjectHash})";
        return new TagOperationSucceeded(effect.TabId, text);
    }

    private async Task<TagCommand> ListRemotesAsync(ListTagRemotesEffect effect, CancellationToken token)
    {
        var remotes = await writer.ListRemotesAsync(effect.Location, token).ConfigureAwait(false);
        return new TagRemotesLoaded(effect.TabId, effect.Name, effect.Push, [.. remotes]);
    }

    private async Task<TagCommand> PushAsync(PushTagEffect effect, CancellationToken token)
    {
        await writer.PushAsync(effect.Location, effect.Remote, effect.Name, token).ConfigureAwait(false);
        return new TagOperationSucceeded(effect.TabId, $"Pushed tag {effect.Name} to {effect.Remote}");
    }

    private async Task<TagCommand> DeleteOnRemoteAsync(DeleteTagOnRemoteEffect effect, CancellationToken token)
    {
        await writer.DeleteOnRemoteAsync(effect.Location, effect.Remote, effect.Name, token).ConfigureAwait(false);
        var text = $"Deleted tag {effect.Name} on {effect.Remote} "
            + $"(restore: git push {effect.Remote} refs/tags/{effect.Name})";
        return new TagOperationSucceeded(effect.TabId, text);
    }

    private static TagCommand Failure(TagEffect effect, Exception exception)
    {
        var reason = FailureText.Reason(exception);
        return effect switch
        {
            CreateTagEffect => new TagOperationFailed(effect.TabId, $"Could not create the tag: {reason}", null),
            DeleteTagEffect => new TagOperationFailed(effect.TabId, $"Could not delete the tag: {reason}", null),
            ListTagRemotesEffect => new TagRemotesFailed(effect.TabId, $"Could not list the remotes: {reason}"),
            PushTagEffect push => new TagOperationFailed(
                effect.TabId,
                $"Could not push the tag: {reason}",
                RetryOf(exception, TagPushNoun, RemoteCommands.BuildTagPush(push.Remote, push.Name, false))),
            DeleteTagOnRemoteEffect delete => new TagOperationFailed(
                effect.TabId,
                $"Could not delete the tag on the remote: {reason}",
                RetryOf(exception, TagDeleteNoun, RemoteCommands.BuildTagDelete(delete.Remote, delete.Name, false))),
            _ => throw new UnhandledEffectException(effect),
        };
    }

    // The terminal can answer a passphrase or host key question that the background run cannot.
    private static TagRetry? RetryOf(Exception exception, string noun, IReadOnlyList<string> arguments)
    {
        var needsCredentials = exception is GitCommandFailedException failed
            && RemoteAuthFailure.Matches(failed.StandardError);
        return needsCredentials ? new TagRetry(noun, [.. arguments]) : null;
    }
}
