using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// The conflict editor (ux-workflows.md, workflow 4, "Conflict editor") as pure transitions
/// <c>(session, command, context) -> (session, effects)</c>. <c>Enter</c> on a conflicted file of the Unstaged list or
/// <c>&lt;space&gt;gr</c> from any pane reads the file (an effect) and opens the full-screen editor on it; the moves,
/// picks and edits of the open editor are <see cref="ConflictActions"/>. <c>Ctrl-s</c> saves the result to the working
/// tree file, after a confirm when conflicts are unresolved (they keep their conflict markers), and marks the file
/// resolved when none is left (no second confirm). <c>Ctrl-x</c> re-creates the conflict after a confirm. While the
/// editor is open it consumes every action; a confirm opens on top of it.
/// </summary>
public static class ConflictReducer
{
    /// <summary>The tab-closed cleanup of the session: closes the editor when its tab is gone, else null.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="workspace">The workspace after the tab command.</param>
    public static ConflictUpdate? OnTabClosed(ConflictSession session, Workspace workspace) =>
        session.Editor is { } editor && workspace.FindTab(editor.TabId) is null ? ConflictFlow.Close(session) : null;

    /// <summary>Applies a command.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="command">What happened.</param>
    /// <param name="context">The tabs and overlays the command may need.</param>
    public static ConflictUpdate Reduce(ConflictSession session, ConflictCommand command, ConflictContext context)
    {
        if (command is ConflictInputCommand input && !Equals(session.Viewports, input.Viewports))
        {
            session = session with { Viewports = input.Viewports };
        }

        return command switch
        {
            ConflictActionPressed pressed => Press(session, pressed, context),
            ConflictClicked clicked => Click(session, clicked, context),
            ConflictDragged dragged => Drag(session, dragged, context),
            ConflictDragStarted started => StartDrag(session, started, context),
            ConflictFileRead read => Read(session, read, context),
            ConflictKeyTyped typed => Type(session, typed),
            ConflictOpenRequested open => Open(session, open, context),
            ConflictOperationCancelled => ConflictFlow.Done(session),
            ConflictOperationFailed failed => Failed(session, failed, context),
            ConflictRecreateConfirmed confirmed => RecreateConfirmed(session, confirmed, context),
            ConflictRecreated recreated => Recreated(session, recreated, context),
            ConflictSaveConfirmed confirmed => SaveConfirmed(session, confirmed, context),
            ConflictSaved saved => Saved(session, saved, context),
            ConflictTabClosed => session.IsOpen ? ConflictFlow.Close(session) : ConflictFlow.Done(session),
            _ => throw new UnhandledCommandException(command),
        };
    }

    private static ConflictUpdate Open(ConflictSession session, ConflictOpenRequested open, ConflictContext context)
    {
        if (session.IsOpen || context.IsCovered || context.Tab is not { } tab)
        {
            return ConflictFlow.Done(session);
        }

        // The editor key works from any pane: the file under the Unstaged cursor, else the first conflicted one. Enter
        // keeps its meaning on every other row.
        var entry = open.FromOpenKey ? ConflictedSelection.ToOpen(tab) : ConflictedSelection.UnderCursor(tab);
        if (entry is null)
        {
            var notice = tab.WorkingTree is null ? ConflictNotices.NotLoaded : ConflictNotices.NoConflicts;

            return open.FromOpenKey ? ConflictFlow.Warn(session, notice) : ConflictFlow.Done(session);
        }

        if (context.IsBusy)
        {
            return ConflictFlow.Warn(session, ConflictNotices.Busy);
        }

        if (!ConflictEditorSupport.CanOpen(entry.Conflict))
        {
            var refusal = ConflictEditorSupport.Refusal(entry.Conflict);

            return ConflictFlow.Warn(
                session,
                $"{ConflictFlow.Name(entry.Path)}: {refusal}, not for the editor; {ConflictNotices.MergetoolHint}");
        }

        var generation = session.Generation + 1;
        var read = new ReadConflictEffect(tab.Id, tab.Location, entry.Path, generation, entry.Conflict);

        return ConflictFlow.Done(session with { Generation = generation }, read);
    }

    private static ConflictUpdate Read(ConflictSession session, ConflictFileRead read, ConflictContext context)
    {
        if (read.Generation != session.Generation)
        {
            return ConflictFlow.Done(session);
        }

        if (context.Tab is null)
        {
            return ConflictFlow.Warn(session, ConflictNotices.Changed);
        }

        if (context.IsCovered)
        {
            return ConflictFlow.Warn(session, ConflictNotices.Covered);
        }

        var name = ConflictFlow.Name(read.Path);
        if (read.Result.Kind != ConflictReadKind.Text)
        {
            return ConflictFlow.Warn(session, $"{name}: {read.Result.Reason}");
        }

        var opened = ConflictEditorState.Create(read.TabId, read.Path, read.Result.Parts);

        return ConflictFlow.ShowCentered(session, opened, OpenNotices(opened, read.Conflict, name));
    }

    private static AppEffect[] OpenNotices(ConflictEditorState opened, ConflictKind conflict, string name)
    {
        if (opened.Format.HadMixedLineEndings)
        {
            var ending = opened.Format.LineEnding == "\n" ? "LF" : "CRLF";

            return [ConflictFlow.Warning($"{name} mixes CRLF and LF line endings: saving writes {ending} everywhere")];
        }

        if (conflict == ConflictKind.BothAdded)
        {
            return [ConflictFlow.Post($"{name}: both sides added it, so the base is empty; pick a side per conflict")];
        }

        return [];
    }

    private static ConflictUpdate Press(ConflictSession session, ConflictActionPressed pressed, ConflictContext context)
    {
        if (session.Editor is not { } editor || context.IsCovered)
        {
            return ConflictFlow.Done(session);
        }

        // An editor whose tab was closed meanwhile closes and lets the action pass on.
        if (context.Tab is null)
        {
            return ConflictFlow.Close(session);
        }

        var action = pressed.Action;
        if (editor.IsEditing)
        {
            if (action == ActionIds.ConflictEditorSave)
            {
                return SaveWhileTyping(session, editor, context);
            }

            return ConflictActions.ExecuteInEdit(session, editor, action) ?? ConflictFlow.Done(session);
        }

        var executed = ConflictActions.Execute(session, editor, action, pressed.Count);
        if (executed is not null)
        {
            return executed;
        }

        if (action == ActionIds.ConflictEditorRecreate)
        {
            return AskRecreate(session, editor);
        }

        return action == ActionIds.ConflictEditorSave ? Save(session, editor, context) : ConflictFlow.Done(session);
    }

    // Typing then saving needs no Esc in between: the text is kept as Ctrl-g keeps it.
    private static ConflictUpdate SaveWhileTyping(
        ConflictSession session,
        ConflictEditorState editor,
        ConflictContext context)
    {
        var ended = ConflictFlow.Show(session, editor.EndEdit());

        return ConflictFlow.Then(ended, Save(ended.Session, ended.Session.Editor!, context));
    }

    private static ConflictUpdate Click(ConflictSession session, ConflictClicked clicked, ConflictContext context)
    {
        if (session.Editor is not { } editor || context.IsCovered)
        {
            return ConflictFlow.Done(session);
        }

        return ConflictActions.Click(session, editor, clicked.Target, clicked.IsDoubleClick);
    }

    private static ConflictUpdate StartDrag(
        ConflictSession session,
        ConflictDragStarted started,
        ConflictContext context)
    {
        if (session.Editor is not { IsEditing: false } editor || context.IsCovered)
        {
            return ConflictFlow.Done(session);
        }

        return ConflictFlow.Show(session, editor.ClickVersionRow(started.Pane, started.Row));
    }

    private static ConflictUpdate Drag(ConflictSession session, ConflictDragged dragged, ConflictContext context)
    {
        if (session.Editor is not { IsVisual: true } editor || context.IsCovered)
        {
            return ConflictFlow.Done(session);
        }

        return ConflictFlow.Show(session, editor.DragToVersionRow(dragged.Row));
    }

    private static ConflictUpdate Type(ConflictSession session, ConflictKeyTyped typed)
    {
        if (session.Editor is not { IsEditing: true } editor)
        {
            return ConflictFlow.Done(session);
        }

        return ConflictFlow.Show(session, editor.TypeKey(typed.Key));
    }

    private static ConflictUpdate Save(ConflictSession session, ConflictEditorState editor, ConflictContext context)
    {
        if (!editor.IsModified)
        {
            return ConflictFlow.Warn(session, ConflictNotices.NothingPicked);
        }

        if (editor.IsComplete)
        {
            return Write(session, editor, context);
        }

        var name = ConflictFlow.Name(editor.Path);
        var unresolved = Plural(editor.UnresolvedCount, "unresolved conflict");
        var prompt = new ConfirmPrompt(
            "Save with unresolved conflicts",
            [
                $"Save {name} with {unresolved} out of {editor.Hunks.Count}?",
                "The unresolved conflicts keep their conflict markers and the file stays conflicted.",
            ],
            "Afterwards you can use {key:conflict-editor.recreate|re-create} to rebuild the whole conflict " +
            "(it discards what you resolved).",
            "Save");
        var accepted = new ConflictSaveConfirmed(editor.TabId, session.Generation);

        return ConflictFlow.Done(session, new ShowConfirmEffect(prompt, accepted, ConflictNotices.TooSmall));
    }

    private static ConflictUpdate SaveConfirmed(
        ConflictSession session,
        ConflictSaveConfirmed confirmed,
        ConflictContext context)
    {
        if (session.Editor is not { } editor || confirmed.Generation != session.Generation || context.Tab is null)
        {
            return ConflictFlow.Done(session);
        }

        return Write(session, editor, context);
    }

    // The result buffer keeps changing while the worker runs: what is written, and whether it counts as resolved, is
    // fixed here, at the moment the user confirmed. The handler reads the file again first: a file edited since it was
    // opened (an editor, a tool) is never overwritten.
    private static ConflictUpdate Write(ConflictSession session, ConflictEditorState editor, ConflictContext context)
    {
        if (context.Tab is not { } tab)
        {
            return ConflictFlow.Done(session);
        }

        if (context.IsBusy)
        {
            return ConflictFlow.Warn(session, $"{ConflictNotices.Busy}; nothing was saved");
        }

        var write = new WriteConflictEffect(
            tab.Id,
            tab.Location,
            editor.Path,
            editor.OriginalText,
            editor.ToText(),
            SavedMessage(ConflictFlow.Name(editor.Path), editor),
            editor.IsComplete);

        return ConflictFlow.Done(session, write);
    }

    private static ConflictUpdate Saved(ConflictSession session, ConflictSaved saved, ConflictContext context)
    {
        var message = ConflictFlow.Post(saved.Message);
        AppEffect next = new RefreshTabEffect(saved.TabId);
        if (context.Tab is not null && saved.IsComplete)
        {
            // Staging reads the status itself, so the lists and the banner count are current when it reports.
            next = new StageResolvedConflictEffect(saved.TabId, saved.Path);
        }

        // The editor of another tab (opened while this write ran) is not this save's to close.
        if (!OwnsEditor(session, saved.TabId))
        {
            return ConflictFlow.Done(session, message, next);
        }

        return ConflictFlow.Close(session, message, next);
    }

    private static ConflictUpdate Failed(
        ConflictSession session,
        ConflictOperationFailed failed,
        ConflictContext context)
    {
        var message = ConflictFlow.Post(failed.Text, failed.Severity);

        if (context.Tab is null && OwnsEditor(session, failed.TabId))
        {
            return ConflictFlow.Close(session, message);
        }

        return ConflictFlow.Done(session, message);
    }

    private static ConflictUpdate AskRecreate(ConflictSession session, ConflictEditorState editor)
    {
        var name = ConflictFlow.Name(editor.Path);
        var prompt = new ConfirmPrompt(
            "Re-create the conflict",
            [
                $"Re-create the conflict of {name}?",
                "The file goes back to its conflict markers, also when it was saved or marked resolved; "
                + "what this editor holds is discarded.",
            ],
            "No undo for the file's current content: it is overwritten.",
            "Re-create");
        var accepted = new ConflictRecreateConfirmed(editor.TabId, session.Generation);

        return ConflictFlow.Done(session, new ShowConfirmEffect(prompt, accepted, ConflictNotices.TooSmall));
    }

    private static ConflictUpdate RecreateConfirmed(
        ConflictSession session,
        ConflictRecreateConfirmed confirmed,
        ConflictContext context)
    {
        if (session.Editor is not { } editor
            || confirmed.Generation != session.Generation
            || context.Tab is not { } tab)
        {
            return ConflictFlow.Done(session);
        }

        if (context.IsBusy)
        {
            return ConflictFlow.Warn(session, $"{ConflictNotices.Busy}; the conflict was not re-created");
        }

        return ConflictFlow.Done(session, new RecreateConflictEffect(tab.Id, tab.Location, editor.Path));
    }

    private static ConflictUpdate Recreated(
        ConflictSession session,
        ConflictRecreated recreated,
        ConflictContext context)
    {
        if (context.Tab is null)
        {
            return ConflictFlow.Close(session);
        }

        var name = ConflictFlow.Name(recreated.Path);
        var refresh = new RefreshTabEffect(recreated.TabId);
        var done = ConflictFlow.Post($"Re-created the conflict of {name}");
        if (!OwnsEditor(session, recreated.TabId))
        {
            return ConflictFlow.Done(session, refresh, done);
        }

        if (recreated.Result.Kind != ConflictReadKind.Text)
        {
            var warning = ConflictFlow.Warning($"{name}: the conflict was re-created, but {recreated.Result.Reason}");

            return ConflictFlow.Close(session, refresh, warning);
        }

        var fresh = ConflictEditorState.Create(recreated.TabId, recreated.Path, recreated.Result.Parts);

        return ConflictFlow.ShowCentered(session, fresh, refresh, done);
    }

    private static bool OwnsEditor(ConflictSession session, TabId tabId) => session.Editor?.TabId == tabId;

    private static string SavedMessage(string name, ConflictEditorState editor)
    {
        if (editor.IsComplete)
        {
            return $"Saved {name}";
        }

        return $"Saved {name}: {Plural(editor.UnresolvedCount, "conflict")} still unresolved";
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
