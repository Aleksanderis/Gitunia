using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.History.Models;

namespace Gitunia.Core.FileHistory;

/// <summary>
/// The file history overlay as pure transitions <c>(session, command) -> (session, effects)</c>. While the history is
/// open it consumes every action, so nothing behind it moves. Reads are effects; the jump to the graph is an effect the
/// terminal layer carries out.
/// </summary>
public static class FileHistoryReducer
{
    /// <summary>Commits read per open; older ones are not listed.</summary>
    public const int EntryLimit = 1000;

    /// <summary>The rows scrolled by one press of the line scroll keys.</summary>
    public const int ScrollStep = 1;

    /// <summary>The warning when the jump runs on an empty history.</summary>
    public const string NoCommitNotice = "No commit selected";

    /// <summary>The warning when a second open is asked for while one is read.</summary>
    public const string StillReadingNotice = "The file history is still being read";

    /// <summary>Applies a command.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="command">What happened.</param>
    /// <param name="context">The tabs the command may need.</param>
    public static FileHistoryUpdate Reduce(
        FileHistorySession session,
        FileHistoryCommand command,
        FileHistoryContext context) =>
        command switch
        {
            FileHistoryActionPressed pressed => Press(session, pressed, context),
            FileHistoryDiffCancelled => Done(session),
            FileHistoryDiffFailed failed => DiffFailed(session, failed),
            FileHistoryDiffLoaded loaded => DiffLoaded(session, loaded),
            FileHistoryEntryClicked clicked => Click(session, clicked.Index, context),
            FileHistoryJumpDone => Close(session),
            FileHistoryJumpRefused refused => Warn(session, refused.Text),
            FileHistoryLoaded loaded => Loaded(session, loaded, context),
            FileHistoryOpenRequested open => Open(session, open, context),
            FileHistoryReadCancelled => Done(session with { IsReading = false }),
            FileHistoryReadFailed failed => Failed(session, failed.Text),
            FileHistoryTabClosed => Close(session),
            _ => throw new UnhandledCommandException(command),
        };

    /// <summary>The tab-closed cleanup of the session: closes the history when its tab is gone, else null.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="workspace">The workspace after the tab command.</param>
    public static FileHistoryUpdate? OnTabClosed(FileHistorySession session, Workspace workspace) =>
        session.History is { } history && workspace.FindTab(history.TabId) is null ? Close(session) : null;

    private static FileHistoryUpdate Open(
        FileHistorySession session,
        FileHistoryOpenRequested open,
        FileHistoryContext context)
    {
        if (context.Tab is not { } tab || tab.Id != open.TabId)
        {
            return Done(session);
        }

        if (session.IsReading)
        {
            return Warn(session, StillReadingNotice);
        }

        return Read(session, tab, open.Revision, open.Path);
    }

    private static FileHistoryUpdate Press(
        FileHistorySession session,
        FileHistoryActionPressed pressed,
        FileHistoryContext context)
    {
        if (session.History is not { } history)
        {
            return Done(session);
        }

        if (context.Tab is not { } tab)
        {
            return Close(session);
        }

        var action = pressed.Action;
        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit)
        {
            return Close(session);
        }

        if (action == ActionIds.FileHistorySelectCommit)
        {
            return Jump(session, history);
        }

        if (action == ActionIds.RepositoryRefresh)
        {
            return session.IsReading ? Done(session) : Read(session, tab, history.Revision, history.Path);
        }

        return Scroll(session, history, pressed) ?? Move(session, history, pressed, tab);
    }

    private static FileHistoryUpdate? Scroll(
        FileHistorySession session,
        FileHistoryState history,
        FileHistoryActionPressed pressed)
    {
        var action = pressed.Action;
        var half = Math.Max(1, pressed.PageRows / 2);
        var steps = Math.Max(1, pressed.Count ?? 1);
        int? rows = null;
        if (action == ActionIds.DiffScrollDown)
        {
            rows = ScrollStep * steps;
        }
        else if (action == ActionIds.DiffScrollUp)
        {
            rows = -ScrollStep * steps;
        }
        else if (action == ActionIds.DiffScrollHalfPageDown)
        {
            rows = half * steps;
        }
        else if (action == ActionIds.DiffScrollHalfPageUp)
        {
            rows = -half * steps;
        }

        return rows is { } scrolled ? With(session, history.ScrollBy(scrolled)) : null;
    }

    private static FileHistoryUpdate Move(
        FileHistorySession session,
        FileHistoryState history,
        FileHistoryActionPressed pressed,
        RepositoryState tab)
    {
        var moved = ListMotion.TryMove(
            pressed.Action,
            pressed.Count,
            history.SelectedIndex,
            history.Entries.Count,
            Math.Max(1, pressed.PageRows),
            out var target);

        return moved ? MoveTo(session, target, tab) : Done(session);
    }

    private static FileHistoryUpdate Click(FileHistorySession session, int index, FileHistoryContext context) =>
        context.Tab is { } tab ? MoveTo(session, index, tab) : Done(session);

    private static FileHistoryUpdate MoveTo(FileHistorySession session, int index, RepositoryState tab)
    {
        if (session.History is not { } history)
        {
            return Done(session);
        }

        var moved = history.MoveTo(index);
        if (moved.SelectedIndex == history.SelectedIndex && history.Diff is not null)
        {
            return Done(session);
        }

        return ReadDiff(session, moved, tab);
    }

    private static FileHistoryUpdate Jump(FileHistorySession session, FileHistoryState history)
    {
        if (history.Selected is not { } entry)
        {
            return Warn(session, NoCommitNotice);
        }

        return Done(session, new FileHistoryJumpEffect(history.TabId, entry.Hash));
    }

    // The history opens (or updates) only if nothing was opened or closed meanwhile: the result echoes the generation.
    private static FileHistoryUpdate Read(FileHistorySession session, RepositoryState tab, string revision, string path)
    {
        var generation = session.Generation + 1;
        var effect = new ReadFileHistoryEffect(tab.Id, tab.Location, revision, path, generation);

        return Done(session with { Generation = generation, IsReading = true }, effect);
    }

    private static FileHistoryUpdate Loaded(
        FileHistorySession session,
        FileHistoryLoaded loaded,
        FileHistoryContext context)
    {
        var idle = session with { IsReading = false };
        if (loaded.Generation != session.Generation || context.Tab is not { } tab)
        {
            return Done(idle);
        }

        var history = new FileHistoryState(loaded.TabId, loaded.Path, loaded.Revision, loaded.Entries);
        if (session.History is { } current && current.TabId == loaded.TabId && current.Path == loaded.Path)
        {
            history = history.MoveTo(KeepIndex(current, loaded.Entries));
        }

        return ReadDiff(idle, history, tab);
    }

    // A refresh keeps the highlight on the same commit when it is still listed, else on the same row.
    private static int KeepIndex(FileHistoryState current, IReadOnlyList<FileHistoryEntry> entries)
    {
        if (current.Selected is not { } selected)
        {
            return 0;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Hash == selected.Hash)
            {
                return index;
            }
        }

        return current.SelectedIndex;
    }

    private static FileHistoryUpdate ReadDiff(FileHistorySession session, FileHistoryState history, RepositoryState tab)
    {
        var generation = session.DiffGeneration + 1;
        var next = session with { History = history, DiffGeneration = generation };
        if (history.Selected is not { } entry)
        {
            return Done(next);
        }

        return Done(next, new ReadFileHistoryDiffEffect(tab.Id, tab.Location, entry, generation));
    }

    private static FileHistoryUpdate DiffLoaded(FileHistorySession session, FileHistoryDiffLoaded loaded)
    {
        if (loaded.Generation != session.DiffGeneration || session.History is not { } history)
        {
            return Done(session);
        }

        return With(session, history with { Diff = loaded.Document, DiffFailure = string.Empty, DiffScroll = 0 });
    }

    private static FileHistoryUpdate DiffFailed(FileHistorySession session, FileHistoryDiffFailed failed)
    {
        if (failed.Generation != session.DiffGeneration || session.History is not { } history)
        {
            return Done(session);
        }

        return With(session, history with { Diff = null, DiffFailure = failed.Text });
    }

    private static FileHistoryUpdate Failed(FileHistorySession session, string text) =>
        Done(session with { IsReading = false }, new PostStatusEffect(text, StatusSeverity.Error));

    private static FileHistoryUpdate Close(FileHistorySession session) =>
        Done(session with
        {
            History = null,
            Generation = session.Generation + 1,
            DiffGeneration = session.DiffGeneration + 1,
        });

    private static FileHistoryUpdate Warn(FileHistorySession session, string text) =>
        Done(session, new PostStatusEffect(text, StatusSeverity.Warning));

    private static FileHistoryUpdate With(
        FileHistorySession session,
        FileHistoryState history,
        params AppEffect[] effects) =>
        Done(session with { History = history }, effects);

    private static FileHistoryUpdate Done(FileHistorySession session, params AppEffect[] effects) =>
        new(session, [.. effects]);
}
