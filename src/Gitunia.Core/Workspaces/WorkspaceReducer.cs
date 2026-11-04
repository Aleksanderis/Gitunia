using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Workspaces;

/// <summary>Pure transitions of the workspace: <c>(Workspace, WorkspaceCommand) -> (Workspace, Effects)</c>. No global state.</summary>
public static class WorkspaceReducer
{
    /// <summary>Applies a command to the workspace and returns the new state and effects.</summary>
    public static WorkspaceUpdate Reduce(Workspace workspace, WorkspaceCommand command) => command switch
    {
        OpenRepositoryCommand open => Open(workspace, open),
        SwitchWorktreeCommand switchTo => SwitchWorktree(workspace, switchTo),
        CloseTabCommand close => Close(workspace, close.TabId),
        ActivateTabCommand activate => Unchanged(Activate(workspace, activate.TabId)),
        NextTabCommand => Unchanged(Step(workspace, 1)),
        PreviousTabCommand => Unchanged(Step(workspace, -1)),
        GoToTabCommand goTo => Unchanged(GoTo(workspace, goTo.Position)),
        MoveTabCommand move => Unchanged(Move(workspace, move.Offset)),
        ActivatePreviousTabCommand => Unchanged(workspace.PreviousTabId is { } previous ? Activate(workspace, previous) : workspace),
        TogglePinCommand pin => Unchanged(TogglePin(workspace, pin.RootPath)),
        RemoveRecentCommand remove => Unchanged(workspace with { Recents = Remove(workspace.Recents, remove.RootPath) }),
        TabCommand tab => Unchanged(Route(workspace, tab)),
        _ => Unchanged(workspace),
    };

    private static WorkspaceUpdate Unchanged(Workspace workspace) => new(workspace, []);

    private static WorkspaceUpdate Open(Workspace workspace, OpenRepositoryCommand command)
    {
        var rootPath = command.Location.RootPath;
        var recents = Remove(workspace.Recents, rootPath).Insert(0, rootPath);
        if (recents.Length > Workspace.MaximumRecents)
        {
            recents = recents.RemoveRange(Workspace.MaximumRecents, recents.Length - Workspace.MaximumRecents);
        }

        var existing = workspace.FindTabByRootPath(rootPath);
        if (existing is not null)
        {
            return Unchanged(Activate(workspace with { Recents = recents }, existing.Id));
        }

        var id = new TabId(workspace.NextTabNumber);
        var tab = RepositoryState.Create(id, command.Location);
        var opened = workspace with
        {
            Tabs = workspace.Tabs.Add(tab),
            NextTabNumber = workspace.NextTabNumber + 1,
            Recents = recents,
        };
        return new WorkspaceUpdate(Activate(opened, id), [new LoadRepositoryEffect(id, tab.CommandPath)]);
    }

    private static WorkspaceUpdate SwitchWorktree(Workspace workspace, SwitchWorktreeCommand command)
    {
        var index = IndexOf(workspace, command.TabId);
        var rootPath = command.Location.RootPath;
        if (index < 0 || IsShownByAnotherTab(workspace, command.TabId, rootPath))
        {
            return Unchanged(workspace);
        }

        var tab = workspace.Tabs[index];
        if (RepositoryPathComparer.Instance.Equals(tab.Location.RootPath, rootPath))
        {
            return Unchanged(workspace);
        }

        // What belongs to the old working directory goes; the view, refs and sidebar are the repository's own.
        var moved = tab with
        {
            Location = command.Location,
            Head = null,
            Status = null,
            WorkingTree = null,
            WorktreeStatuses = null,
            IsMissing = false,
        };
        var recents = Remove(workspace.Recents, rootPath).Insert(0, rootPath);
        if (recents.Length > Workspace.MaximumRecents)
        {
            recents = recents.RemoveRange(Workspace.MaximumRecents, recents.Length - Workspace.MaximumRecents);
        }

        var switched = workspace with { Tabs = workspace.Tabs.SetItem(index, moved), Recents = recents };
        return new WorkspaceUpdate(switched, [new LoadRepositoryEffect(command.TabId, moved.CommandPath)]);
    }

    private static bool IsShownByAnotherTab(Workspace workspace, TabId tabId, string rootPath) =>
        workspace.Tabs.Any(
            other => other.Id != tabId && RepositoryPathComparer.Instance.Equals(other.Location.RootPath, rootPath));

    private static WorkspaceUpdate Close(Workspace workspace, TabId id)
    {
        var index = IndexOf(workspace, id);
        if (index < 0)
        {
            return Unchanged(workspace);
        }

        var tabs = workspace.Tabs.RemoveAt(index);
        var previous = workspace.PreviousTabId == id ? null : workspace.PreviousTabId;
        var active = workspace.ActiveTabId;
        if (active == id)
        {
            active = tabs.IsEmpty ? null : tabs[Math.Min(index, tabs.Length - 1)].Id;
            previous = null;
        }

        return new WorkspaceUpdate(workspace with { Tabs = tabs, ActiveTabId = active, PreviousTabId = previous }, [new CancelLoadEffect(id)]);
    }

    private static Workspace Activate(Workspace workspace, TabId id)
    {
        if (IndexOf(workspace, id) < 0 || workspace.ActiveTabId == id)
        {
            return workspace;
        }

        return workspace with { ActiveTabId = id, PreviousTabId = workspace.ActiveTabId };
    }

    private static Workspace Step(Workspace workspace, int direction)
    {
        if (workspace.Tabs.IsEmpty)
        {
            return workspace;
        }

        var current = workspace.ActiveTabId is { } activeId ? IndexOf(workspace, activeId) : -1;
        var count = workspace.Tabs.Length;
        var next = (current + direction + count) % count;
        if (current < 0 && direction > 0)
        {
            next = 0;
        }
        else if (current < 0)
        {
            next = count - 1;
        }

        return Activate(workspace, workspace.Tabs[next].Id);
    }

    private static Workspace GoTo(Workspace workspace, int position) =>
        position >= 1 && position <= workspace.Tabs.Length ? Activate(workspace, workspace.Tabs[position - 1].Id) : workspace;

    // Only the array order changes: the active and previous tab are ids, so they follow their tabs.
    private static Workspace Move(Workspace workspace, int offset)
    {
        var from = workspace.ActiveTabId is { } activeId ? IndexOf(workspace, activeId) : -1;
        if (from < 0 || offset == 0)
        {
            return workspace;
        }

        var to = Math.Clamp(from + offset, 0, workspace.Tabs.Length - 1);
        if (to == from)
        {
            return workspace;
        }

        var tab = workspace.Tabs[from];

        return workspace with { Tabs = workspace.Tabs.RemoveAt(from).Insert(to, tab) };
    }

    private static Workspace TogglePin(Workspace workspace, string rootPath)
    {
        if (workspace.IsPinned(rootPath))
        {
            return workspace with { Pins = Remove(workspace.Pins, rootPath) };
        }

        return workspace with { Pins = workspace.Pins.Add(rootPath) };
    }

    private static Workspace Route(Workspace workspace, TabCommand command)
    {
        var index = IndexOf(workspace, command.TabId);
        if (index < 0)
        {
            return workspace;
        }

        var updated = RepositoryReducer.Reduce(workspace.Tabs[index], command.Command);
        return workspace with { Tabs = workspace.Tabs.SetItem(index, updated) };
    }

    private static int IndexOf(Workspace workspace, TabId id)
    {
        for (var index = 0; index < workspace.Tabs.Length; index++)
        {
            if (workspace.Tabs[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static ImmutableArray<string> Remove(ImmutableArray<string> paths, string rootPath) =>
        [.. paths.Where(path => !RepositoryPathComparer.Instance.Equals(path, rootPath))];
}
