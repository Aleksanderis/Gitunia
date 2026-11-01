using System.Collections.Immutable;
using Gitunia.Core.Picker;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;

namespace Gitunia.App.Picker;

/// <summary>
/// Does the file system and git work of the repository picker off the UI thread: the listing of a typed path (a newer
/// one replaces the older, a slow file system times out), the scan for repositories and the discovery of a chosen
/// directory. Each posts its result command through the dispatcher; it never reads or changes state.
/// </summary>
/// <param name="discovery">Resolves a directory to its repository.</param>
/// <param name="files">The file system questions.</param>
/// <param name="dispatcher">Where the result commands go.</param>
/// <param name="timings">The listing delays; <see cref="PickerTimings.Default"/> in the app.</param>
public sealed class PickerEffectHandler(
    RepositoryDiscovery discovery,
    IDirectoryLister files,
    IDispatcher dispatcher,
    PickerTimings timings)
{
    private CancellationTokenSource? _listing;

    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    /// <param name="effect">What to do.</param>
    /// <param name="cancellationToken">Ends the work when the app is closing.</param>
    public void Handle(PickerEffect effect, CancellationToken cancellationToken)
    {
        switch (effect)
        {
            case ListPickerDirectoriesEffect list:
                StartListing(list, cancellationToken);
                break;
            case CancelPickerListingEffect:
                CancelListing();
                break;
            case ScanRepositoriesEffect scan:
                _ = Task.Run(() => Scan(scan, cancellationToken), CancellationToken.None);
                break;
            case DiscoverRepositoryEffect discover:
                _ = Task.Run(() => DiscoverAsync(discover, cancellationToken), CancellationToken.None);
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    // A newer keystroke cancels the older listing and a slow file system times out (the blocked directory call itself
    // cannot be interrupted, it just finishes unobserved). The task that runs a listing disposes its source.
    private void StartListing(ListPickerDirectoriesEffect effect, CancellationToken shutdown)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var previous = Interlocked.Exchange(ref _listing, cancellation);
        Cancel(previous);
        _ = Task.Run(() => ListAsync(effect, cancellation), CancellationToken.None);
    }

    private void CancelListing() => Cancel(Interlocked.Exchange(ref _listing, null));

    private static void Cancel(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The listing ended meanwhile.
        }
    }

    private async Task ListAsync(ListPickerDirectoriesEffect effect, CancellationTokenSource cancellation)
    {
        using var owned = cancellation;
        var rows = ImmutableArray<PickerRow>.Empty;
        var message = string.Empty;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        timeout.CancelAfter(timings.ListingTimeout);
        var token = timeout.Token;
        try
        {
            var work = Task.Run(
                () => PathCandidates.List(effect.Query, effect.HomeDirectory, effect.BaseDirectory, files, token),
                CancellationToken.None);
            var first = await Task.WhenAny(work, Task.Delay(timings.ScanningDelay, token)).ConfigureAwait(false);
            if (first != work)
            {
                dispatcher.Post(new ShowScanningCommand(effect.Query));
            }

            rows = [.. await work.WaitAsync(token).ConfigureAwait(false)];
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            message = "listing timed out: the file system is slow";
        }
        catch (Exception exception)
        {
            // Whatever went wrong, the picker must leave the scanning state.
            message = exception.Message;
        }

        dispatcher.Post(new ListingCompletedCommand(effect.Query, rows, message));
    }

    private void Scan(ScanRepositoriesEffect effect, CancellationToken token)
    {
        var found = ImmutableArray<string>.Empty;
        try
        {
            var roots = effect.Roots ?? RepositoryScanner.DefaultRoots(effect.HomeDirectory, files.DirectoryExists);
            found = RepositoryScanner.Scan(
                roots,
                RepositoryScanner.DefaultMaximumDepth,
                RepositoryScanner.DefaultMaximumResults,
                token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A scan that fails finds nothing; the next picker open tries again.
        }
        catch (OperationCanceledException)
        {
            return;
        }

        dispatcher.Post(new PickerScanFinished(found));
    }

    private async Task DiscoverAsync(DiscoverRepositoryEffect effect, CancellationToken token)
    {
        PickerCommand result;
        try
        {
            var location = await discovery.DiscoverAsync(effect.Path, token).ConfigureAwait(false);
            result = new PickerRepositoryFound(effect.Request, location, effect.Replacing);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (exception is GitException or IOException or UnauthorizedAccessException
            or ArgumentException)
        {
            result = new PickerDiscoveryFailed(effect.Request, exception.Message);
        }

        dispatcher.Post(result);
    }
}
