using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Conflicts;

/// <summary>
/// The conflict editor (ux.md, workflow 4, "Conflict editor"): <c>Enter</c> (or <c>&lt;space&gt;to</c>) on a
/// conflicted file of the Unstaged list reads it off the UI thread and opens a modal view of one conflict hunk at a
/// time. Each hunk gets a pick (ours, theirs, both), text typed by the user (<c>e</c>, in Insert mode) or none, and
/// every change can be undone and redone; <c>B</c> shows the base of the hunk and <c>R</c> re-creates the conflict of
/// the file after a Confirm. <c>Enter</c> saves the result to the working tree file,
/// after a Confirm when hunks are unresolved (they keep their conflict markers), and then offers to mark the file
/// resolved with the usual Confirm. The editor owns the keys while it is open; a Confirm opens on top of it.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="reader">Reads the conflicted file into hunks.</param>
/// <param name="writer">Writes the result into the working tree.</param>
/// <param name="recreator">Re-creates the conflict of the file on request.</param>
/// <param name="statuses">Where refusals, failures and the outcome are reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="askResolved">
/// Opens the "mark resolved" Confirm for a path of a tab; returns a notice, empty when it asked.
/// </param>
/// <param name="refresh">Reads a tab again (refs, graph, status, operation); false when it cannot.</param>
/// <param name="isOtherOverlayOpen">True while an overlay other than this editor is open.</param>
/// <param name="screenSize">The current terminal size.</param>
/// <param name="glyphs">The characters the editor is drawn with.</param>
/// <param name="setMode">Switches the keymap mode (Insert while the text of a hunk is edited).</param>
/// <param name="redraw">Redraws the overlay layer and the status bar.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with staging, discard, reset and stash.</param>
/// <param name="keys">Where the chips take their key text from; null uses the default keymap.</param>
public sealed class ConflictEditorController(
    WorkspaceHost host,
    IConflictReader reader,
    IConflictWriter writer,
    IConflictRecreator recreator,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<RepositoryState, string, string> askResolved,
    Func<TabId, bool> refresh,
    Func<bool> isOtherOverlayOpen,
    Func<ScreenSize> screenSize,
    GraphGlyphSet glyphs,
    Action<KeyMode> setMode,
    Action redraw,
    TabOperationGate gate,
    ChipKeys? keys = null)
{
    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was saved";
    private const string NothingPickedNotice = "No hunk is picked yet: nothing to save";
    private const string NotConflictedNotice = "Select a conflicted file (!) in the Unstaged list of the working tree (<space>s)";
    private const string ChangedNotice = "The tab changed while the file was read: the conflict editor was not opened";
    private const string CoveredNotice =
        "Another view opened while the file was read: the conflict editor was not opened";
    private const string FileChangedNotice = "The file changed on disk since it was opened: nothing was saved";
    private const string MergetoolHint = "use <space>tm for the mergetool";

    private int _generation;
    private bool _isWorking;

    /// <summary>The open editor, null while it is closed.</summary>
    public ConflictEditorState? State { get; private set; }

    public bool IsOpen => State is not null;

    /// <summary>True while the text of a hunk is edited (the keymap is in Insert mode).</summary>
    public bool IsEditing => State is { IsEditing: true };

    /// <summary>The frame to draw, or null when closed, covered by a dialog or the screen is too small.</summary>
    public OverlayFrame? CurrentFrame()
    {
        if (State is not { } state || isOtherOverlayOpen())
        {
            return null;
        }

        var size = screenSize();

        return ConflictEditorFrameBuilder.Build(state, size.Width, size.Height, glyphs, keys);
    }

    /// <summary>
    /// Handles an action: opens the editor on <see cref="ActionIds.ConflictOpen"/>, or on <c>Enter</c> over a
    /// conflicted file of the Unstaged list; while it is open and no dialog covers it, consumes every action (nothing
    /// behind it may move). Returns true when the action was consumed.
    /// </summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (State is not { } state)
        {
            return TryOpen(action);
        }

        if (isOtherOverlayOpen())
        {
            return false;
        }

        if (host.Workspace.FindTab(state.TabId) is null)
        {
            Close();

            return false;
        }

        Execute(state, action);

        return true;
    }

    /// <summary>
    /// A click on a frame line and column: a chip returns its mouse view for the caller to send through the mouse
    /// map. Returns null when the click hit no chip.
    /// </summary>
    public string? OnLineClicked(int frameLine, int frameColumn, out bool handled)
    {
        handled = false;
        if (State is null || isOtherOverlayOpen())
        {
            return null;
        }

        var size = screenSize();
        var view = ConflictEditorFrameBuilder.ChipAt(State, frameLine, frameColumn, size.Width, size.Height, keys);
        handled = view is not null;

        return view;
    }

    /// <summary>Handles a key typed while a hunk's text is edited; false when none is.</summary>
    public bool HandleTextKey(KeyEvent key)
    {
        if (State is not { Edit: { } edit } state || host.Workspace.ActiveTabId != state.TabId)
        {
            return false;
        }

        edit.Editor.Apply(key);
        redraw();

        return true;
    }

    public void Close()
    {
        _generation++;
        if (IsEditing)
        {
            setMode(KeyMode.Normal);
        }

        State = null;
        redraw();
    }

    private bool TryOpen(ActionId action)
    {
        var isOpenKey = action == ActionIds.ConflictOpen;
        if (!isOpenKey && action != ActionIds.WorkingTreeOpenDiff)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is not { } tab || isOtherOverlayOpen())
        {
            return false;
        }

        var entry = ConflictedUnderCursor(tab);
        if (entry is null)
        {
            // Enter on any other row keeps its meaning; only the dedicated key explains itself.
            if (isOpenKey)
            {
                statuses.Warning(NotConflictedNotice);
            }

            return isOpenKey;
        }

        Begin(tab, entry);

        return true;
    }

    private void Begin(RepositoryState tab, StatusEntry entry)
    {
        var name = WorkingTreeRowRenderer.Printable(entry.Path);
        if (_isWorking)
        {
            return;
        }

        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning(BusyNotice);

            return;
        }

        if (entry.Conflict != ConflictKind.BothModified)
        {
            statuses.Warning($"{name}: {Describe(entry.Conflict)}; {MergetoolHint}");

            return;
        }

        var generation = ++_generation;
        var token = cancellationOf(tab.Id);
        _isWorking = true;
        _ = Task.Run(() => ReadAsync(tab.Id, tab.Location, entry.Path, generation, token), CancellationToken.None);
    }

    private async Task ReadAsync(
        TabId tabId,
        RepositoryLocation location,
        string path,
        int generation,
        CancellationToken token)
    {
        try
        {
            var result = await reader.ReadAsync(location, path, token).ConfigureAwait(false);
            postToUi(() => Show(generation, tabId, path, result));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _isWorking = false);
        }
        catch (Exception exception)
        {
            var text = $"Could not read {WorkingTreeRowRenderer.Printable(path)}: {OperationText.Reason(exception)}";
            postToUi(() => Fail(text));
        }
    }

    private void Show(int generation, TabId tabId, string path, ConflictReadResult result)
    {
        _isWorking = false;
        if (generation != _generation)
        {
            return;
        }

        if (host.Workspace.FindTab(tabId) is null)
        {
            statuses.Warning(ChangedNotice);

            return;
        }

        if (isOtherOverlayOpen())
        {
            statuses.Warning(CoveredNotice);

            return;
        }

        if (result.Kind != ConflictReadKind.Text)
        {
            statuses.Warning($"{WorkingTreeRowRenderer.Printable(path)}: {result.Reason}");

            return;
        }

        State = ConflictEditorState.Create(tabId, path, result.Parts);
        redraw();
    }

    private void Fail(string text)
    {
        _isWorking = false;
        statuses.Error(text);
        redraw();
    }

    private void Execute(ConflictEditorState state, ActionId action)
    {
        if (state.IsEditing)
        {
            ExecuteInEdit(state, action);
        }
        else if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit)
        {
            RequestClose(state);
        }
        else if (action == ActionIds.ConflictEditorNext || action == ActionIds.ListDown)
        {
            Update(state.Next());
        }
        else if (action == ActionIds.ConflictEditorPrevious || action == ActionIds.ListUp)
        {
            Update(state.Previous());
        }
        else if (action == ActionIds.ConflictEditorNextDiff)
        {
            Update(state.NextDifference(ScrollLimit(state)));
        }
        else if (action == ActionIds.ConflictEditorPreviousDiff)
        {
            Update(state.PreviousDifference(ScrollLimit(state)));
        }
        else if (action == ActionIds.ConflictEditorScrollDown || action == ActionIds.ConflictEditorScrollUp)
        {
            ScrollSides(state, action == ActionIds.ConflictEditorScrollDown ? 1 : -1);
        }
        else if (action == ActionIds.ConflictEditorOurs)
        {
            Update(state.Pick(ConflictPick.Ours));
        }
        else if (action == ActionIds.ConflictEditorTheirs)
        {
            Update(state.Pick(ConflictPick.Theirs));
        }
        else if (action == ActionIds.ConflictEditorBoth)
        {
            Update(state.Pick(ConflictPick.Both));
        }
        else if (action == ActionIds.ConflictEditorReset)
        {
            Update(state.Pick(ConflictPick.None));
        }
        else if (action == ActionIds.ConflictEditorAllOurs)
        {
            Update(state.PickAllUnresolved(ConflictPick.Ours));
        }
        else if (action == ActionIds.ConflictEditorAllTheirs)
        {
            Update(state.PickAllUnresolved(ConflictPick.Theirs));
        }
        else if (action == ActionIds.ConflictEditorUndo)
        {
            Update(state.Undo());
        }
        else if (action == ActionIds.ConflictEditorRedo)
        {
            Update(state.Redo());
        }
        else if (action == ActionIds.ConflictEditorEdit)
        {
            BeginEdit(state);
        }
        else if (action == ActionIds.ConflictEditorBase)
        {
            if (!ConflictEditorFrameBuilder.IsThreePaneWidth(screenSize().Width))
            {
                Update(state.ToggleBase());
            }
        }
        else if (action == ActionIds.ConflictEditorRecreate)
        {
            AskRecreate(state);
        }
        else if (action == ActionIds.ConflictEditorSave)
        {
            Save(state);
        }
    }

    // Esc keeps the text like Ctrl-g does: the edit is one step of undo, so nothing is lost by leaving it.
    private void ExecuteInEdit(ConflictEditorState state, ActionId action)
    {
        if (action == ActionIds.ConflictEditorEditNewLine)
        {
            state.Edit!.Editor.Apply(new KeyEvent(KeyNames.Enter));
            redraw();
        }
        else if (action == ActionIds.ConflictEditorEditDone
            || action == ActionIds.Cancel
            || action == ActionIds.NavigateBack)
        {
            setMode(KeyMode.Normal);
            Update(state.AcceptEdit());
        }
    }

    private int ScrollLimit(ConflictEditorState state)
    {
        var size = screenSize();

        return ConflictEditorFrameBuilder.MaxSideScroll(state, size.Width, size.Height, keys);
    }

    // A half page of the side panes, at least one line.
    private void ScrollSides(ConflictEditorState state, int direction)
    {
        var size = screenSize();
        var rows = ConflictEditorFrameBuilder.SideRowCount(state, size.Width, size.Height, keys);
        var maximum = ConflictEditorFrameBuilder.MaxSideScroll(state, size.Width, size.Height, keys);
        Update(state.ScrollSides(direction * Math.Max(1, rows / 2), maximum));
    }

    private void BeginEdit(ConflictEditorState state)
    {
        var edited = state.BeginEdit();
        if (!edited.IsEditing)
        {
            return;
        }

        setMode(KeyMode.Insert);
        Update(edited);
    }

    private void Update(ConflictEditorState state)
    {
        State = state;
        redraw();
    }

    // Picks are not thrown away by one stray Esc: the first press warns, the second closes.
    private void RequestClose(ConflictEditorState state)
    {
        if (state.IsModified && !state.IsDiscardPending)
        {
            Update(state.AskDiscard());

            return;
        }

        Close();
    }

    private void Save(ConflictEditorState state)
    {
        if (!state.IsModified)
        {
            statuses.Warning(NothingPickedNotice);

            return;
        }

        if (state.IsComplete)
        {
            Write(state);

            return;
        }

        var name = WorkingTreeRowRenderer.Printable(state.Path);
        var prompt = new ConfirmPrompt(
            "Save with unresolved hunks",
            [
                $"Save {name} with {Plural(state.UnresolvedCount, "unresolved hunk")} out of {state.Hunks.Count}?",
                "The unresolved hunks keep their conflict markers and the file stays conflicted.",
            ],
            "Undo: git checkout --merge -- <file> re-creates the whole conflict (it discards what you resolved).",
            "Save");
        if (!confirm(prompt, () => Write(state)))
        {
            statuses.Warning(TooSmallNotice);
        }
    }

    // The file is read again first: a file edited since it was opened (an editor, a tool) is never overwritten.
    private void Write(ConflictEditorState state)
    {
        if (host.Workspace.FindTab(state.TabId) is not { } tab)
        {
            return;
        }

        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was saved");

            return;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => WriteAsync(tab, state, token), CancellationToken.None);
    }

    private async Task WriteAsync(RepositoryState tab, ConflictEditorState state, CancellationToken token)
    {
        var name = WorkingTreeRowRenderer.Printable(state.Path);
        try
        {
            var fresh = await reader.ReadAsync(tab.Location, state.Path, token).ConfigureAwait(false);
            if (fresh.Kind != ConflictReadKind.Text || ConflictEditorState.TextOf(fresh.Parts) != state.OriginalText)
            {
                postToUi(() => Done(tab.Id, FileChangedNotice, StatusSeverity.Warning, state, false));

                return;
            }

            await writer.WriteAsync(tab.Location, state.Path, state.ToText(), token).ConfigureAwait(false);
            var message = SavedMessage(name, state);
            postToUi(() => Done(tab.Id, message, StatusSeverity.Info, state, true));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception)
        {
            var text = $"Could not save {name}: {OperationText.Reason(exception)}; your picks are kept";
            postToUi(() => Done(tab.Id, text, StatusSeverity.Error, state, false));
        }
    }

    private void Done(TabId tabId, string message, StatusSeverity severity, ConflictEditorState state, bool isSaved)
    {
        statuses.Post(message, severity);
        gate.End(tabId);
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            Close();

            return;
        }

        if (!isSaved)
        {
            redraw();

            return;
        }

        Close();
        refresh(tabId);
        if (state.IsComplete)
        {
            var notice = askResolved(tab, state.Path);
            if (notice.Length > 0)
            {
                statuses.Warning(notice);
            }
        }
    }

    private void AskRecreate(ConflictEditorState state)
    {
        var name = WorkingTreeRowRenderer.Printable(state.Path);
        var prompt = new ConfirmPrompt(
            "Re-create the conflict",
            [
                $"Re-create the conflict of {name}?",
                "The file goes back to its conflict markers, also when it was saved or marked resolved; "
                + "what this editor holds is discarded.",
            ],
            "No undo for the file's current content: git checkout --merge -- <file> overwrites it.",
            "Re-create");
        if (!confirm(prompt, () => Recreate(state)))
        {
            statuses.Warning(TooSmallNotice);
        }
    }

    private void Recreate(ConflictEditorState state)
    {
        if (host.Workspace.FindTab(state.TabId) is not { } tab)
        {
            return;
        }

        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; the conflict was not re-created");

            return;
        }

        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RecreateAsync(tab, state, token), CancellationToken.None);
    }

    private async Task RecreateAsync(RepositoryState tab, ConflictEditorState state, CancellationToken token)
    {
        var name = WorkingTreeRowRenderer.Printable(state.Path);
        try
        {
            await recreator.RecreateAsync(tab.Location, state.Path, token).ConfigureAwait(false);
            var fresh = await ReadAfterRecreateAsync(tab.Location, state.Path, token).ConfigureAwait(false);
            postToUi(() => Recreated(tab.Id, state, fresh));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception)
        {
            var text = $"Could not re-create the conflict of {name}: {OperationText.Reason(exception)}";
            postToUi(() => Done(tab.Id, text, StatusSeverity.Error, state, false));
        }
    }

    // The file is already rewritten when this runs, so a failed read is reported as such, not as a failed re-create.
    private async Task<ConflictReadResult> ReadAfterRecreateAsync(
        RepositoryLocation location,
        string path,
        CancellationToken token)
    {
        try
        {
            return await reader.ReadAsync(location, path, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ConflictReadResult.Unsupported($"it could not be read: {OperationText.Reason(exception)}");
        }
    }

    private void Recreated(TabId tabId, ConflictEditorState old, ConflictReadResult result)
    {
        var name = WorkingTreeRowRenderer.Printable(old.Path);
        gate.End(tabId);
        if (host.Workspace.FindTab(tabId) is null)
        {
            Close();

            return;
        }

        refresh(tabId);
        if (State is null)
        {
            statuses.Post($"Re-created the conflict of {name}");

            return;
        }

        if (result.Kind != ConflictReadKind.Text)
        {
            statuses.Warning($"{name}: the conflict was re-created, but {result.Reason}");
            Close();

            return;
        }

        if (IsEditing)
        {
            setMode(KeyMode.Normal);
        }

        statuses.Post($"Re-created the conflict of {name}");
        State = ConflictEditorState.Create(tabId, old.Path, result.Parts) with { IsBaseShown = old.IsBaseShown };
        redraw();
    }

    private static StatusEntry? ConflictedUnderCursor(RepositoryState tab)
    {
        if (tab.View.FocusedPane is PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff)
        {
            return ConflictedSelection.Find(tab.WorkingTree);
        }

        return null;
    }

    private static string SavedMessage(string name, ConflictEditorState state)
    {
        if (state.IsComplete)
        {
            return $"Saved {name}: every hunk resolved";
        }

        return $"Saved {name}: {Plural(state.UnresolvedCount, "hunk")} still unresolved";
    }

    private static string Describe(ConflictKind kind) => kind switch
    {
        ConflictKind.BothAdded => "both sides added it (add/add)",
        ConflictKind.BothDeleted => "both sides deleted it",
        ConflictKind.AddedByUs => "only we added it",
        ConflictKind.AddedByThem => "only they added it",
        ConflictKind.DeletedByUs => "we deleted it (delete/modify)",
        ConflictKind.DeletedByThem => "they deleted it (delete/modify)",
        _ => "this conflict cannot be resolved hunk by hunk",
    };

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
