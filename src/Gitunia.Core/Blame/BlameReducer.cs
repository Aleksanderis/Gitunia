using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Search;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Blame.Models;

namespace Gitunia.Core.Blame;

/// <summary>
/// The blame view as pure transitions <c>(session, command) -> (session, effects)</c>. While the view is open it
/// consumes every action, so nothing behind it moves. The file is read and blamed by an effect; the lines stay in the
/// blame store, so the reducer only counts them and moves the cursor.
/// </summary>
public static class BlameReducer
{
    /// <summary>The warning when the highlighted line has no commit yet.</summary>
    public const string NoCommitYetNotice = "Git has not found this line's commit yet: try again in a moment";

    /// <summary>The warning when the highlighted line is not committed.</summary>
    public const string UncommittedNotice = "This line is not committed yet";

    /// <summary>The warning when a search step is asked for and no search was typed.</summary>
    public const string NoSearchNotice = "No search: press / first";

    /// <summary>The title of a file read from the working tree.</summary>
    public const string WorkingTreeTitle = "working tree";

    /// <summary>Applies a command.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="command">What happened.</param>
    /// <param name="context">The tabs the command may need.</param>
    public static BlameUpdate Reduce(BlameSession session, BlameCommand command, BlameContext context) =>
        command switch
        {
            BlameActionPressed pressed => Press(session, pressed, context),
            BlameFailed failed => Failed(session, failed),
            BlameFinished finished => Progress(session, finished.Generation, finished.BlamedLines, true),
            BlameKeyTyped typed => TypeKey(session, typed.Key, context),
            BlameLoaded loaded => Loaded(session, loaded),
            BlameMatchFound found => Matched(session, found),
            BlameOpenRefused refused => Warn(session, refused.Text),
            BlameOpenRequested open => Open(session, open, context),
            BlameProgressed progressed => Progress(session, progressed.Generation, progressed.BlamedLines, false),
            BlameRowClicked clicked => ClickRow(session, clicked.Index),
            _ => throw new UnhandledCommandException(command),
        };

    /// <summary>The tab-closed cleanup of the session: closes the view when its tab is gone, else null.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="workspace">The workspace after the tab command.</param>
    public static BlameUpdate? OnTabClosed(BlameSession session, Workspace workspace) =>
        session.View is { } view && workspace.FindTab(view.TabId) is null ? Close(session) : null;

    private static BlameUpdate Open(BlameSession session, BlameOpenRequested open, BlameContext context)
    {
        if (context.ActiveTab is not { } tab || session.IsOpen)
        {
            return Done(session);
        }

        var generation = session.Generation + 1;
        var origin = open.Revision is { } revision ? $"@ {ShortHash.Of(revision)}" : WorkingTreeTitle;
        var view = new BlameState(tab.Id, open.Path, open.Revision, $"{open.Path} {origin}");
        var effect = new StartBlameEffect(tab.Id, tab.Location, open.Path, open.Revision, generation);

        return Done(new BlameSession(view, generation), effect);
    }

    private static BlameUpdate Press(BlameSession session, BlameActionPressed pressed, BlameContext context)
    {
        if (session.View is not { } view)
        {
            return Done(session);
        }

        if (context.ViewTab is null)
        {
            return Close(session);
        }

        var action = pressed.Action;
        if (view.IsEditingSearch)
        {
            return InSearchBox(session, view, action);
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            return view.HasSearch ? With(session, view.ClearSearch()) : Close(session);
        }

        if (action == ActionIds.AppQuit)
        {
            return Close(session);
        }

        if (action == ActionIds.SearchOpen)
        {
            return With(session, view.OpenSearch(), new SetKeyModeEffect(KeyMode.Insert));
        }

        if (action == ActionIds.SearchNext || action == ActionIds.SearchPrevious)
        {
            return StepMatch(session, view, action == ActionIds.SearchNext, pressed.Count);
        }

        if (action == ActionIds.BlameShowCommit)
        {
            return ShowCommit(session, view, pressed.CursorCommit);
        }

        return Move(session, view, pressed);
    }

    private static BlameUpdate ShowCommit(BlameSession session, BlameState view, string? commit)
    {
        if (commit is null)
        {
            return Warn(session, NoCommitYetNotice);
        }

        if (commit == BlameCommit.UncommittedId)
        {
            return Warn(session, UncommittedNotice);
        }

        if (!ObjectId.TryParse(commit, out var id))
        {
            return Warn(session, NoCommitYetNotice);
        }

        var closed = Close(session);

        return closed with
        {
            Effects = [.. closed.Effects, new LocateGraphCommitEffect(view.TabId, id, ShortHash.Of(commit))],
        };
    }

    private static BlameUpdate Move(BlameSession session, BlameState view, BlameActionPressed pressed)
    {
        var moved = ListMotion.TryMove(
            pressed.Action,
            pressed.Count,
            view.Cursor,
            view.LineCount,
            Math.Max(1, pressed.PageRows),
            out var target);

        return moved ? With(session, view.MoveTo(target)) : Done(session);
    }

    private static BlameUpdate InSearchBox(BlameSession session, BlameState view, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            return AcceptSearch(session, view);
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            return With(session, view.ClearSearch(), new SetKeyModeEffect(KeyMode.Normal));
        }

        return action == ActionIds.AppQuit ? Close(session) : Done(session);
    }

    private static BlameUpdate AcceptSearch(BlameSession session, BlameState view)
    {
        var accepted = view.AcceptSearch();
        var mode = new SetKeyModeEffect(KeyMode.Normal);
        if (accepted.SearchText.Length == 0)
        {
            return With(session, accepted.ClearSearch(), mode);
        }

        var find = new FindBlameMatchEffect(session.Generation, accepted.SearchText, view.Cursor - 1, true, 1);

        return With(session, accepted, mode, find);
    }

    private static BlameUpdate StepMatch(BlameSession session, BlameState view, bool forward, int? count)
    {
        if (view.SearchText.Length == 0)
        {
            return Warn(session, NoSearchNotice);
        }

        var find = new FindBlameMatchEffect(
            session.Generation,
            view.SearchText,
            view.Cursor,
            forward,
            Math.Max(1, count ?? 1));

        return Done(session, find);
    }

    private static BlameUpdate Matched(BlameSession session, BlameMatchFound found)
    {
        if (session.View is not { } view || found.Generation != session.Generation || found.Query != view.SearchText)
        {
            return Done(session);
        }

        if (found.Index < 0)
        {
            return Warn(session, $"No match for {found.Query}");
        }

        return With(session, view.MoveTo(found.Index));
    }

    private static BlameUpdate Loaded(BlameSession session, BlameLoaded loaded)
    {
        if (loaded.Generation != session.Generation || session.View is null)
        {
            return Done(session);
        }

        if (loaded.Kind == BlameContentKind.Text)
        {
            return With(session, session.View with { LineCount = loaded.LineCount });
        }

        var path = session.View.Path;
        var text = loaded.Kind == BlameContentKind.Binary
            ? $"{path} is a binary file: there are no lines to blame"
            : $"{path} is not in the working tree";
        var closed = Close(session);

        return closed with { Effects = [.. closed.Effects, new PostStatusEffect(text, StatusSeverity.Warning)] };
    }

    private static BlameUpdate Progress(BlameSession session, long generation, int blamedLines, bool isComplete)
    {
        if (generation != session.Generation || session.View is not { } view)
        {
            return Done(session);
        }

        return With(session, view with { BlamedLines = blamedLines, IsComplete = isComplete || view.IsComplete });
    }

    private static BlameUpdate Failed(BlameSession session, BlameFailed failed)
    {
        if (failed.Generation != session.Generation)
        {
            return Done(session);
        }

        var closed = Close(session);
        if (failed.Text.Length == 0)
        {
            return closed;
        }

        return closed with { Effects = [.. closed.Effects, new PostStatusEffect(failed.Text, StatusSeverity.Error)] };
    }

    private static BlameUpdate TypeKey(BlameSession session, KeyEvent key, BlameContext context)
    {
        if (!session.TakesText(context.ActiveTab?.Id) || session.View is not { } view)
        {
            return Done(session);
        }

        if (PromptTextEditor.Apply(view.SearchText, key) is { } edited && edited != view.SearchText)
        {
            return With(session, view with { SearchText = edited });
        }

        return Done(session);
    }

    private static BlameUpdate ClickRow(BlameSession session, int index)
    {
        if (session.View is { IsEditingSearch: false } view)
        {
            return With(session, view.MoveTo(index));
        }

        return Done(session);
    }

    private static BlameUpdate Close(BlameSession session)
    {
        var generation = session.Generation;
        var closed = session with { View = null, Generation = generation + 1 };
        var effects = new List<AppEffect> { new StopBlameEffect(generation) };
        if (session.View is { IsEditingSearch: true })
        {
            effects.Add(new SetKeyModeEffect(KeyMode.Normal));
        }

        return Done(closed, [.. effects]);
    }

    private static BlameUpdate Warn(BlameSession session, string text) =>
        Done(session, new PostStatusEffect(text, StatusSeverity.Warning));

    private static BlameUpdate With(BlameSession session, BlameState view, params AppEffect[] effects) =>
        Done(session with { View = view }, effects);

    private static BlameUpdate Done(BlameSession session, params AppEffect[] effects) =>
        new(session, [.. effects]);
}
