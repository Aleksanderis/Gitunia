using System.Collections.Immutable;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Owns the open repository picker: applies picker commands through the pure reducer, scans for repositories in the background
/// and resolves what the user chose into workspace commands. UI thread only; results of background work are posted back.
/// </summary>
public sealed class PickerController(RepositoryDiscovery discovery, WorkspaceHost host, string homeDirectory, Action<Action> postToUi,
    TimeSpan? listingTimeout = null,
    Func<string, IReadOnlyList<string>>? listDirectories = null,
    TimeSpan? scanningDelay = null,
    Func<TabId, TabId, bool>? moveDraft = null,
    ImmutableArray<string>? scanRoots = null)
{
    private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan DefaultListingTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan DefaultScanningDelay = TimeSpan.FromMilliseconds(150);

    private readonly TimeSpan _listingTimeoutValue = listingTimeout ?? DefaultListingTimeout;
    private readonly TimeSpan _scanningDelayValue = scanningDelay ?? DefaultScanningDelay;
    private readonly Func<string, IReadOnlyList<string>> _listDirectoriesFunction = listDirectories ?? ListDirectories;
    private CancellationTokenSource? _listingCancellation;
    private bool _completeAfterListing;
    private ImmutableArray<string> _discovered = [];
    private ImmutableArray<string>? _scanRoots = scanRoots;
    private DateTime _lastScan = DateTime.MinValue;
    private bool _isScanning;
    private int _openRequest;

    /// <summary>Raised after the picker opened, closed or changed, so the view can redraw.</summary>
    public event Action? Changed;

    /// <summary>
    /// The folders the next scan looks in, null for the conventional ones. The settings screen changes it while the
    /// app runs; the next open of the picker rescans.
    /// </summary>
    public ImmutableArray<string>? ScanRoots
    {
        get => _scanRoots;
        set
        {
            _scanRoots = value;
            _lastScan = DateTime.MinValue;
        }
    }

    /// <summary>The open picker, null when closed.</summary>
    public PickerState? State { get; private set; }

    public bool IsOpen => State is not null;

    /// <summary>Opens the picker, or resets it when it is already open.</summary>
    /// <param name="locatingTabId">The missing tab the chosen repository should replace.</param>
    /// <param name="initialQuery">Text to start with.</param>
    public void Open(TabId? locatingTabId = null, string initialQuery = "")
    {
        CancelListing();
        _completeAfterListing = false;
        State = PickerReducer.Open(CreateContext(), locatingTabId, initialQuery);
        if (State.IsListing)
        {
            StartListing(State.Query);
        }

        StartScan();
        Changed?.Invoke();
    }

    public void Close()
    {
        _openRequest++;
        CancelListing();
        _completeAfterListing = false;
        State = null;
        Changed?.Invoke();
    }

    public void Apply(PickerCommand command)
    {
        if (State is { IsListing: true } && command is CompletePathCommand)
        {
            // Tab during a pending listing is applied once the candidates arrive.
            _completeAfterListing = true;
            return;
        }

        if (State is { } state)
        {
            var reduced = PickerReducer.Reduce(state, command, CreateContext());
            State = reduced;
            if (reduced.IsListing && (!state.IsListing || reduced.Query != state.Query))
            {
                StartListing(reduced.Query);
            }
            else if (!reduced.IsListing)
            {
                CancelListing();
            }

            Changed?.Invoke();
            if (command is ListingCompletedCommand && _completeAfterListing && !reduced.IsListing)
            {
                _completeAfterListing = false;
                Apply(new CompletePathCommand());
            }
        }
    }

    /// <summary>Rebuilds the rows after the workspace changed behind the picker (a pin, a removed recent).</summary>
    public void Refresh() => Apply(new RefreshRowsCommand());

    /// <summary>Opens the highlighted repository or the typed path; a repository that is already open just gets focus.</summary>
    public void Accept()
    {
        if (State is not { } state)
        {
            return;
        }

        if (state.IsPathMode)
        {
            OpenPath(PathQuery.Resolve(state.Query, homeDirectory, BaseDirectory()), state.LocatingTabId);
            return;
        }

        if (state.HighlightedRow is not { } row)
        {
            Apply(new SetMessageCommand($"nothing to open: type a path such as {PathDisplay.ExamplePath(OperatingSystem.IsWindows())}"));
            return;
        }

        if (row.IsOpen && state.LocatingTabId is null && host.Workspace.FindTabByRootPath(row.Path) is { } tab)
        {
            host.Dispatch(new ActivateTabCommand(tab.Id));
            Close();
            return;
        }

        OpenPath(row.Path, state.LocatingTabId);
    }

    /// <summary>Pins or unpins the highlighted repository.</summary>
    public void TogglePin()
    {
        if (State?.HighlightedRow is { } row && !row.Tags.HasFlag(PickerTags.Directory))
        {
            host.Dispatch(new TogglePinCommand(row.Path));
            Refresh();
        }
    }

    /// <summary>Forgets the highlighted repository in the recents list. Nothing on disk is touched.</summary>
    public void RemoveRecent()
    {
        if (State?.HighlightedRow is not { } row || row.Tags.HasFlag(PickerTags.Directory))
        {
            return;
        }

        if (!row.IsRecent)
        {
            Apply(new SetMessageCommand("not in recents"));
            return;
        }

        host.Dispatch(new RemoveRecentCommand(row.Path));
        Refresh();
        Apply(new SetMessageCommand("removed from recents (nothing was deleted on disk)"));
    }

    /// <summary>Resolves a directory to its repository in the background, then opens it in a tab (or says why not).</summary>
    public void OpenPath(string path, TabId? replacing = null)
    {
        var request = ++_openRequest;
        Apply(new SetMessageCommand($"opening {path}..."));
        _ = Task.Run(async () =>
        {
            try
            {
                var location = await discovery.DiscoverAsync(path, CancellationToken.None).ConfigureAwait(false);
                postToUi(() => Opened(request, location, replacing));
            }
            catch (GitException exception)
            {
                postToUi(() => Failed(request, exception.Message));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                postToUi(() => Failed(request, exception.Message));
            }
        });
    }

    private void Opened(int request, RepositoryLocation location, TabId? replacing)
    {
        if (request != _openRequest)
        {
            return;
        }

        host.Dispatch(new OpenRepositoryCommand(location));
        if (replacing is { } missing)
        {
            // The commit draft of the missing tab moves to the replacing tab; when that tab already has its own message, the
            // missing tab stays so nothing is dropped (#369).
            var target = host.Workspace.FindTabByRootPath(location.RootPath);
            if (target?.Id == missing)
            {
                // The repository is back at the same path: the tab is its own replacement and keeps its draft.
            }
            else if (target is null || moveDraft is null || moveDraft(missing, target.Id))
            {
                host.Dispatch(new CloseTabCommand(missing));
            }
            else
            {
                Apply(new SetMessageCommand("Both tabs hold an unsent commit message; the missing tab stays open (close it with Ctrl-w c)"));
                return;
            }
        }

        Close();
    }

    private void Failed(int request, string message)
    {
        if (request == _openRequest && IsOpen)
        {
            Apply(new SetMessageCommand(message));
        }
    }

    /// <summary>
    /// Opens a repository the app just created (clone, init) in a new tab and closes the picker; the location is
    /// known, so nothing is discovered again.
    /// </summary>
    public void OpenLocation(RepositoryLocation location) => Opened(++_openRequest, location, null);

    /// <summary>Where relative paths are taken from: the active repository, or the home directory.</summary>
    public string BaseDirectory() => host.Workspace.ActiveTab?.Location.RootPath ?? homeDirectory;

    private PickerContext CreateContext() =>
        new(host.Workspace, _discovered, homeDirectory, BaseDirectory(), _listDirectoriesFunction, IsRepository);

    // Lists the candidates of a typed path on the thread pool; a newer keystroke cancels the older listing and a slow file system
    // times out (the blocked directory call itself cannot be interrupted, it just finishes unobserved).
    private void StartListing(string query)
    {
        CancelListing();
        var cancellation = new CancellationTokenSource();
        _listingCancellation = cancellation;
        var context = CreateContext();
        var timeout = _listingTimeoutValue;
        var parentToken = cancellation.Token;

        // The source is disposed on the UI thread (CancelListing or the completion callback), so a late Cancel never hits a disposed source.
        _ = Task.Run(async () =>
        {
            var rows = ImmutableArray<PickerRow>.Empty;
            var message = string.Empty;
            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            timeoutCancellation.CancelAfter(timeout);
            var token = timeoutCancellation.Token;
            try
            {
                var work = Task.Run(() => PathCompleter.ListCandidates(query, context, token), CancellationToken.None);
                var first = await Task.WhenAny(work, Task.Delay(_scanningDelayValue, token)).ConfigureAwait(false);
                if (first != work)
                {
                    postToUi(() =>
                    {
                        if (ReferenceEquals(_listingCancellation, cancellation) && IsOpen)
                        {
                            Apply(new ShowScanningCommand(query));
                        }
                    });
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

            postToUi(() =>
            {
                if (ReferenceEquals(_listingCancellation, cancellation) && IsOpen)
                {
                    _listingCancellation = null;
                    cancellation.Dispose();
                    Apply(new ListingCompletedCommand(query, rows, message));
                }
            });
        });
    }

    private void CancelListing()
    {
        if (_listingCancellation is { } cancellation)
        {
            _listingCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private void StartScan()
    {
        if (_isScanning || DateTime.UtcNow - _lastScan < RescanInterval)
        {
            return;
        }

        _isScanning = true;
        _lastScan = DateTime.UtcNow;
        var roots = _scanRoots ?? RepositoryScanner.DefaultRoots(homeDirectory, Directory.Exists);
        _ = Task.Run(() =>
        {
            var found = ImmutableArray<string>.Empty;
            try
            {
                found = RepositoryScanner.Scan(roots, RepositoryScanner.DefaultMaximumDepth, RepositoryScanner.DefaultMaximumResults, CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // A scan that fails finds nothing; the next picker open tries again.
            }

            postToUi(() =>
            {
                _isScanning = false;
                _discovered = found;
                Refresh();
            });
        });
    }

    private static IReadOnlyList<string> ListDirectories(string directory)
    {
        try
        {
            return [.. Directory.EnumerateDirectories(directory)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsRepository(string directory)
    {
        var marker = Path.Combine(directory, ".git");
        return Directory.Exists(marker) || File.Exists(marker);
    }
}
