using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Picker;

/// <summary>
/// The picker session as pure transitions: opening and closing, the typed query, the background listing and scan (as
/// effects), and what accepting a row means (a workspace command and the follow-up commands). Pure: no file system, no
/// clock (the time arrives in <see cref="PickerOpenRequested"/>).
/// </summary>
public static class PickerSessionReducer
{
    private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(30);

    private const string MovedDraftBothMessage =
        "Both tabs hold an unsent commit message; the missing tab stays open (close it with Ctrl-w c)";

    /// <summary>Applies a command to the session; <paramref name="workspace"/> is what the rows are built from.</summary>
    public static PickerUpdate Reduce(PickerSession session, PickerCommand command, Workspace workspace)
    {
        var effects = new List<AppEffect>();
        var next = new List<AppCommand>();
        var reduced = Apply(session, command, workspace, effects, next);

        return new PickerUpdate(reduced, [.. effects], [.. next]);
    }

    /// <summary>Where relative paths are taken from: the active repository, or the home directory.</summary>
    public static string BaseDirectory(PickerSession session, Workspace workspace) =>
        workspace.ActiveTab?.Location.RootPath ?? session.HomeDirectory;

    private static PickerSession Apply(
        PickerSession session,
        PickerCommand command,
        Workspace workspace,
        List<AppEffect> effects,
        List<AppCommand> next)
    {
        switch (command)
        {
            case PickerOpenRequested open:
                return Open(session, open, workspace, effects);
            case PickerCloseRequested:
                return Close(session, effects);
            case PickerCancelPressed cancel:
                return Cancel(session, cancel, workspace, effects, next);
            case PickerAcceptPressed:
                return Accept(session, workspace, effects, next);
            case PickerPinPressed:
                return Pin(session, next);
            case PickerRemoveRecentPressed:
                return RemoveRecent(session, workspace, effects, next);
            case PickerScanRootsChanged roots:
                return session with { ScanRoots = roots.Roots, LastScan = null };
            case PickerScanFinished scan:
                return Edit(
                    session with { IsScanning = false, Discovered = scan.Found },
                    new RefreshRowsCommand(),
                    workspace,
                    effects);
            case PickerRepositoryFound found:
                return Found(session, found, next);
            case PickerDiscoveryFailed failed:
                return failed.Request == session.OpenRequest && session.IsOpen
                    ? Edit(session, new SetMessageCommand(failed.Message), workspace, effects)
                    : session;
            case PickerRepositoryOpened opened:
                return Opened(session, opened, workspace, effects, next);
            case PickerDraftMoved moved:
                return DraftMoved(session, moved, workspace, effects, next);
            case PickerRepositoryCreated created:
                next.Add(new OpenRepositoryCommand(created.Location));

                return Close(session with { OpenRequest = session.OpenRequest + 1 }, effects);
            default:
                return Edit(session, command, workspace, effects);
        }
    }

    private static PickerSession Open(
        PickerSession session,
        PickerOpenRequested open,
        Workspace workspace,
        List<AppEffect> effects)
    {
        if (open.KeepIfOpen && session.IsOpen || open.OnlyWithoutTabs && !workspace.Tabs.IsEmpty)
        {
            return session;
        }

        var context = ContextOf(session, workspace);
        var state = PickerReducer.Open(context, open.LocatingTabId, open.InitialQuery);
        var opened = session with { State = state, CompleteAfterListing = false };
        if (state.IsListing)
        {
            effects.Add(new ListPickerDirectoriesEffect(state.Query, session.HomeDirectory, context.BaseDirectory));
        }
        else if (session.State is { IsListing: true })
        {
            effects.Add(new CancelPickerListingEffect());
        }

        if (opened.IsScanning || opened.LastScan is { } last && open.Now - last < RescanInterval)
        {
            return opened;
        }

        effects.Add(new ScanRepositoriesEffect(opened.ScanRoots, opened.HomeDirectory));

        return opened with { IsScanning = true, LastScan = open.Now };
    }

    private static PickerSession Close(PickerSession session, List<AppEffect> effects)
    {
        if (session.State is { IsListing: true })
        {
            effects.Add(new CancelPickerListingEffect());
        }

        return session with { State = null, CompleteAfterListing = false, OpenRequest = session.OpenRequest + 1 };
    }

    private static PickerSession Cancel(
        PickerSession session,
        PickerCancelPressed cancel,
        Workspace workspace,
        List<AppEffect> effects,
        List<AppCommand> next)
    {
        if (!workspace.Tabs.IsEmpty)
        {
            return Close(session, effects);
        }

        next.Add(new QuitRequested(cancel.AnyUnsentMessage, false));

        return session;
    }

    private static PickerSession Accept(
        PickerSession session,
        Workspace workspace,
        List<AppEffect> effects,
        List<AppCommand> next)
    {
        if (session.State is not { } state)
        {
            return session;
        }

        if (state.IsPathMode)
        {
            var path = PathQuery.Resolve(state.Query, session.HomeDirectory, BaseDirectory(session, workspace));

            return OpenPath(session, path, state.LocatingTabId, workspace, effects);
        }

        if (state.HighlightedRow is not { } row)
        {
            var example = PathDisplay.ExamplePath(OperatingSystem.IsWindows());

            return Edit(session, new SetMessageCommand($"nothing to open: type a path such as {example}"), workspace, effects);
        }

        if (row.IsOpen && state.LocatingTabId is null && workspace.FindTabByRootPath(row.Path) is { } tab)
        {
            next.Add(new ActivateTabCommand(tab.Id));

            return Close(session, effects);
        }

        return OpenPath(session, row.Path, state.LocatingTabId, workspace, effects);
    }

    private static PickerSession OpenPath(
        PickerSession session,
        string path,
        TabId? replacing,
        Workspace workspace,
        List<AppEffect> effects)
    {
        var request = session.OpenRequest + 1;
        var counted = session with { OpenRequest = request };
        effects.Add(new DiscoverRepositoryEffect(path, request, replacing));

        return Edit(counted, new SetMessageCommand($"opening {path}..."), workspace, effects);
    }

    private static PickerSession Pin(PickerSession session, List<AppCommand> next)
    {
        if (session.State?.HighlightedRow is { } row && !row.Tags.HasFlag(PickerTags.Directory))
        {
            next.Add(new TogglePinCommand(row.Path));
            next.Add(new RefreshRowsCommand());
        }

        return session;
    }

    private static PickerSession RemoveRecent(
        PickerSession session,
        Workspace workspace,
        List<AppEffect> effects,
        List<AppCommand> next)
    {
        if (session.State?.HighlightedRow is not { } row || row.Tags.HasFlag(PickerTags.Directory))
        {
            return session;
        }

        if (!row.IsRecent)
        {
            return Edit(session, new SetMessageCommand("not in recents"), workspace, effects);
        }

        next.Add(new RemoveRecentCommand(row.Path));
        next.Add(new RefreshRowsCommand());
        next.Add(new SetMessageCommand("removed from recents (nothing was deleted on disk)"));

        return session;
    }

    private static PickerSession Found(PickerSession session, PickerRepositoryFound found, List<AppCommand> next)
    {
        if (found.Request != session.OpenRequest)
        {
            return session;
        }

        next.Add(new OpenRepositoryCommand(found.Location));
        next.Add(new PickerRepositoryOpened(found.Replacing, found.Location));

        return session;
    }

    // The draft of the missing tab moves to the replacing tab; when that tab already has its own message, the missing tab
    // stays so nothing is dropped (#369).
    private static PickerSession Opened(
        PickerSession session,
        PickerRepositoryOpened opened,
        Workspace workspace,
        List<AppEffect> effects,
        List<AppCommand> next)
    {
        if (opened.Replacing is not { } missing)
        {
            return Close(session, effects);
        }

        var target = workspace.FindTabByRootPath(opened.Location.RootPath);
        if (target is null)
        {
            next.Add(new CloseTabCommand(missing));

            return Close(session, effects);
        }

        if (target.Id == missing)
        {
            // The repository is back at the same path: the tab is its own replacement and keeps its draft.
            return Close(session, effects);
        }

        effects.Add(new MoveCommitDraftEffect(missing, target.Id));

        return session;
    }

    private static PickerSession DraftMoved(
        PickerSession session,
        PickerDraftMoved moved,
        Workspace workspace,
        List<AppEffect> effects,
        List<AppCommand> next)
    {
        if (!moved.Moved)
        {
            return Edit(session, new SetMessageCommand(MovedDraftBothMessage), workspace, effects);
        }

        next.Add(new CloseTabCommand(moved.Missing));

        return Close(session, effects);
    }

    // The edits of the open picker; a listing of a typed path is started or dropped as the state asks.
    private static PickerSession Edit(
        PickerSession session,
        PickerCommand command,
        Workspace workspace,
        List<AppEffect> effects)
    {
        if (session.State is not { } state)
        {
            return session;
        }

        if (state.IsListing && command is CompletePathCommand)
        {
            // Tab during a pending listing is applied once the candidates arrive.
            return session with { CompleteAfterListing = true };
        }

        var context = ContextOf(session, workspace);
        var reduced = PickerReducer.Reduce(state, command, context);
        var updated = session with { State = reduced };
        if (reduced.IsListing && (!state.IsListing || reduced.Query != state.Query))
        {
            effects.Add(new ListPickerDirectoriesEffect(reduced.Query, session.HomeDirectory, context.BaseDirectory));
        }
        else if (!reduced.IsListing && state.IsListing && command is not ListingCompletedCommand)
        {
            effects.Add(new CancelPickerListingEffect());
        }

        if (command is ListingCompletedCommand && session.CompleteAfterListing && !reduced.IsListing)
        {
            return Edit(updated with { CompleteAfterListing = false }, new CompletePathCommand(), workspace, effects);
        }

        return updated;
    }

    private static PickerContext ContextOf(PickerSession session, Workspace workspace) =>
        new(workspace, session.Discovered, BaseDirectory(session, workspace));
}
