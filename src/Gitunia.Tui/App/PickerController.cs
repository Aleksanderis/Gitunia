using System.Collections.Immutable;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git;

namespace Gitunia.Tui.App;

/// <summary>
/// Owns the open repo picker: applies picker commands through the pure reducer, scans for repositories in the background
/// and resolves what the user chose into workspace commands. UI thread only; results of background work are posted back.
/// </summary>
public sealed class PickerController(GitRunner runner, WorkspaceHost host, string homeDirectory, Action<Action> postToUi)
{
    private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(30);

    private ImmutableArray<string> discovered = [];
    private DateTime lastScan = DateTime.MinValue;
    private bool isScanning;
    private int openRequest;

    /// <summary>Raised after the picker opened, closed or changed, so the view can redraw.</summary>
    public event Action? Changed;

    /// <summary>The open picker, null when closed.</summary>
    public PickerState? State { get; private set; }

    public bool IsOpen => State is not null;

    /// <summary>Opens the picker, or resets it when it is already open.</summary>
    /// <param name="locatingTabId">The missing tab the chosen repository should replace.</param>
    /// <param name="initialQuery">Text to start with.</param>
    public void Open(TabId? locatingTabId = null, string initialQuery = "")
    {
        State = PickerReducer.Open(CreateContext(), locatingTabId, initialQuery);
        StartScan();
        Changed?.Invoke();
    }

    public void Close()
    {
        openRequest++;
        State = null;
        Changed?.Invoke();
    }

    public void Apply(PickerCommand command)
    {
        if (State is { } state)
        {
            State = PickerReducer.Reduce(state, command, CreateContext());
            Changed?.Invoke();
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
            Apply(new SetMessageCommand("nothing to open: type a path such as ~/projects/app"));
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
        var request = ++openRequest;
        Apply(new SetMessageCommand("opening " + path + "..."));
        _ = Task.Run(async () =>
        {
            try
            {
                var location = await RepositoryDiscovery.DiscoverAsync(runner, path, CancellationToken.None).ConfigureAwait(false);
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
        if (request != openRequest)
        {
            return;
        }

        host.Dispatch(new OpenRepositoryCommand(location));
        if (replacing is { } missing)
        {
            host.Dispatch(new CloseTabCommand(missing));
        }

        Close();
    }

    private void Failed(int request, string message)
    {
        if (request == openRequest && IsOpen)
        {
            Apply(new SetMessageCommand(message));
        }
    }

    private string BaseDirectory() => host.Workspace.ActiveTab?.Location.RootPath ?? homeDirectory;

    private PickerContext CreateContext() =>
        new(host.Workspace, discovered, homeDirectory, BaseDirectory(), ListDirectories, IsRepository);

    private void StartScan()
    {
        if (isScanning || DateTime.UtcNow - lastScan < RescanInterval)
        {
            return;
        }

        isScanning = true;
        lastScan = DateTime.UtcNow;
        var roots = RepositoryScanner.DefaultRoots(homeDirectory, Directory.Exists);
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
                isScanning = false;
                discovered = found;
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
