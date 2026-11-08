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
/// <param name="tasks">Remembers the running work so the shutdown can wait for it; the default is a private set.</param>
public sealed class CreationEffectHandler(
    IRepositoryCreator creator,
    IDirectoryLister files,
    IDispatcher dispatcher,
    ShutdownTasks? tasks = null)
{
    private const string PromptsOffMarker = "terminal prompts disabled";
    private const string DeniedMarker = "Permission denied";
    private const string AuthMarker = "Authentication failed";
    private const string CredentialHint =
        "set up a credential helper or an ssh-agent key, credential prompts are not supported yet";

    private readonly ShutdownTasks _tasks = tasks ?? new ShutdownTasks();
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
                _tasks.Run(() =>
                {
                    ResolveParent(parent);

                    return Task.CompletedTask;
                });
                break;
            case CloneRepositoryEffect clone:
                StartClone(clone, cancellationToken);
                break;
            case CancelCloneEffect:
                CancelClone();
                break;
            case InitRepositoryEffect init:
                _tasks.Run(() => InitAsync(init, cancellationToken));
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    /// <summary>
    /// Stops a running clone and waits for it to clean up (at the end of the program, so a half-made directory does
    /// not stay); gives up after <paramref name="timeout"/>.
    /// </summary>
    /// <returns>True when no clone runs any more.</returns>
    /// <param name="timeout">What is left of the shared shutdown bound.</param>
    public bool CancelAndWait(TimeSpan timeout)
    {
        CancelClone();

        return _cloneTask?.Wait(timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout) ?? true;
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
        try
        {
            dispatcher.Post(new CloneParentResolved(effect.Source, FindParent(effect)));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The default folder is only a suggestion: home does when the lookup fails.
            dispatcher.Post(new CloneParentResolved(effect.Source, effect.HomeDirectory));
        }
    }

    private string FindParent(ResolveCloneParentEffect effect)
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

        return parent;
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

    private Task CloneAsync(CloneRepositoryEffect effect, CancellationTokenSource cancellation) =>
        EffectWork.RunAsync(
            token => CloneCoreAsync(effect, token),
            () => new CloneCancelled(),
            exception => new CloneFailed($"Clone failed: {FailureText(exception)}"),
            result => Finish(cancellation, result),
            cancellation.Token);

    private async Task<CreationCommand> CloneCoreAsync(CloneRepositoryEffect effect, CancellationToken token)
    {
        var progress = new CloneProgress(dispatcher);
        var location = await creator.CloneAsync(effect.Request, progress, token).ConfigureAwait(false);

        return new CloneSucceeded(location);
    }

    private void Finish(CancellationTokenSource cancellation, CreationCommand result)
    {
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
