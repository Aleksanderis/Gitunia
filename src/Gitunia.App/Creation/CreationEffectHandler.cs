using Gitunia.App.Picker;
using Gitunia.Core.Creation;
using Gitunia.Core.Picker;
using Gitunia.Core.Text;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories;

namespace Gitunia.App.Creation;

/// <summary>
/// Runs clone and init off the UI thread and posts the progress and exactly one result command for each. A clone never
/// prompts for credentials: a failure says so and names the way out (#45 offers the real terminal; the place for it is
/// <see cref="FailureText"/>, where an auth failure is recognised, and the clone call). Esc or the cancel chip stops a
/// clone; the creator removes what the clone made. It never reads or changes state.
/// </summary>
/// <param name="creator">Runs clone and init.</param>
/// <param name="files">The file system questions of the default clone folder.</param>
/// <param name="dispatcher">Where the progress and result commands go.</param>
public sealed class CreationEffectHandler(IRepositoryCreator creator, IDirectoryLister files, IDispatcher dispatcher)
{
    private const string PromptsOffMarker = "terminal prompts disabled";
    private const string DeniedMarker = "Permission denied";
    private const string AuthMarker = "Authentication failed";
    private const string CredentialHint =
        "set up a credential helper or an ssh-agent key, credential prompts are not supported yet";

    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(5);

    private CancellationTokenSource? _clone;
    private Task? _cloneTask;

    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    /// <param name="effect">What to do.</param>
    /// <param name="cancellationToken">Ends the work when the app is closing.</param>
    public void Handle(CreationEffect effect, CancellationToken cancellationToken)
    {
        switch (effect)
        {
            case ResolveCloneParentEffect parent:
                _ = Task.Run(() => ResolveParent(parent), CancellationToken.None);
                break;
            case CloneRepositoryEffect clone:
                StartClone(clone, cancellationToken);
                break;
            case CancelCloneEffect:
                CancelClone();
                break;
            case InitRepositoryEffect init:
                _ = Task.Run(() => InitAsync(init, cancellationToken), CancellationToken.None);
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    /// <summary>
    /// Stops a running clone and waits for it to clean up (at the end of the program, so a half-made directory does
    /// not stay); gives up after a few seconds.
    /// </summary>
    public void CancelAndWait()
    {
        CancelClone();
        _cloneTask?.Wait(ShutdownWait);
    }

    private static string FailureText(Exception exception)
    {
        var reason = exception is CloneTargetException ? exception.Message : RemoteFailureText.Reason(exception);
        var needsCredentials = reason.Contains(PromptsOffMarker, StringComparison.OrdinalIgnoreCase)
            || reason.Contains(DeniedMarker, StringComparison.OrdinalIgnoreCase)
            || reason.Contains(AuthMarker, StringComparison.OrdinalIgnoreCase);

        return needsCredentials ? $"{reason} ({CredentialHint})" : reason;
    }

    // The folder a clone goes into: the folder typed in the picker (or the one holding the typed name), else the first
    // scan root that exists, else home.
    private void ResolveParent(ResolveCloneParentEffect effect)
    {
        string parent;
        if (effect.TypedPath is { } typed)
        {
            parent = effect.EndsWithSeparator || files.DirectoryExists(typed)
                ? typed
                : Path.GetDirectoryName(typed) ?? typed;
        }
        else
        {
            var roots = effect.ScanRoots ?? RepositoryScanner.DefaultRoots(effect.HomeDirectory, files.DirectoryExists);
            parent = roots.FirstOrDefault(files.DirectoryExists) ?? effect.HomeDirectory;
        }

        dispatcher.Post(new CloneParentResolved(effect.Source, parent));
    }

    private void StartClone(CloneRepositoryEffect effect, CancellationToken shutdown)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        _clone = cancellation;
        _cloneTask = Task.Run(() => CloneAsync(effect, cancellation), CancellationToken.None);
    }

    private void CancelClone()
    {
        try
        {
            _clone?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The clone ended meanwhile.
        }
    }

    private async Task CloneAsync(CloneRepositoryEffect effect, CancellationTokenSource cancellation)
    {
        CreationCommand result;
        try
        {
            var progress = new CloneProgress(dispatcher);
            var location = await creator
                .CloneAsync(effect.Request, progress, cancellation.Token)
                .ConfigureAwait(false);
            result = new CloneSucceeded(location);
        }
        catch (OperationCanceledException)
        {
            result = new CloneCancelled();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result = new CloneFailed($"Clone failed: {FailureText(exception)}");
        }

        cancellation.Dispose();
        dispatcher.Post(result);
    }

    private async Task InitAsync(InitRepositoryEffect effect, CancellationToken token)
    {
        CreationCommand result;
        try
        {
            var init = await creator.InitAsync(effect.Directory, token).ConfigureAwait(false);
            result = new InitSucceeded(init.Location, init.WasRepository);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result = new InitFailed($"Init failed: {FailureText(exception)}");
        }

        dispatcher.Post(result);
    }

    private sealed class CloneProgress(IDispatcher dispatcher) : IProgress<RemoteProgress>
    {
        public void Report(RemoteProgress value) => dispatcher.Post(new CloneProgressed(value));
    }
}
