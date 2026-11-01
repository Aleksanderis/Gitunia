using Gitunia.Core.Remotes;
using Gitunia.Core.Text;
using Gitunia.Git.Remotes;

namespace Gitunia.App.Remotes;

/// <summary>
/// Runs the remote management effects off the UI thread and posts exactly one result command for each: success with the
/// text for the status bar, failure with git's first error line, or cancellation. It never reads or changes state.
/// Timeouts stay in the git layer.
/// </summary>
/// <param name="manager">Runs the remote commands.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class RemoteEffectHandler(IRemoteManager manager, IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(RemoteEffect effect, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => RunAsync(effect, cancellationToken), CancellationToken.None);
    }

    private async Task RunAsync(RemoteEffect effect, CancellationToken token)
    {
        RemoteCommand result;
        try
        {
            result = await PerformAsync(effect, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new RemoteOperationCancelled(effect.TabId);
        }
        catch (Exception exception)
        {
            result = Failure(effect, exception);
        }

        dispatcher.Post(result);
    }

    private async Task<RemoteCommand> PerformAsync(RemoteEffect effect, CancellationToken token)
    {
        switch (effect)
        {
            case AddRemoteEffect add:
                await manager.AddAsync(add.Location, add.Name, add.Url, token).ConfigureAwait(false);
                return Success(effect, $"Added remote {add.Name} ({add.Url}); run fetch to get its branches");
            case ListRemotesEffect list:
                var remotes = await manager.ListAsync(list.Location, token).ConfigureAwait(false);
                return new RemotesListed(effect.TabId, list.Action, list.Target, [.. remotes]);
            case RemoveRemoteEffect remove:
                await manager.RemoveAsync(remove.Location, remove.Name, token).ConfigureAwait(false);
                var restore = $"restore: git remote add {remove.Name} {remove.Url}, then fetch";
                return Success(effect, $"Removed remote {remove.Name} ({restore})");
            case RenameRemoteEffect rename:
                await manager.RenameAsync(rename.Location, rename.OldName, rename.NewName, token)
                    .ConfigureAwait(false);
                return Success(effect, $"Renamed remote {rename.OldName} to {rename.NewName}");
            case SetRemoteUrlEffect setUrl:
                await manager.SetUrlAsync(setUrl.Location, setUrl.Name, setUrl.Url, token).ConfigureAwait(false);
                return Success(effect, $"Set the URL of {setUrl.Name} to {setUrl.Url} (was {setUrl.OldUrl})");
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private static RemoteOperationSucceeded Success(RemoteEffect effect, string text) => new(effect.TabId, text);

    private static RemoteCommand Failure(RemoteEffect effect, Exception exception)
    {
        var reason = FailureText.Reason(exception);
        return effect switch
        {
            AddRemoteEffect => new RemoteOperationFailed(effect.TabId, $"Could not add the remote: {reason}"),
            ListRemotesEffect => new RemotesListFailed(effect.TabId, $"Could not list the remotes: {reason}"),
            RemoveRemoteEffect => new RemoteOperationFailed(effect.TabId, $"Could not remove the remote: {reason}"),
            RenameRemoteEffect => new RemoteOperationFailed(effect.TabId, $"Could not rename the remote: {reason}"),
            SetRemoteUrlEffect => new RemoteOperationFailed(effect.TabId, $"Could not set the URL: {reason}"),
            _ => throw new UnhandledEffectException(effect),
        };
    }
}
