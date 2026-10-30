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
/// The conflict editor (ux.md, workflow 4, "Conflict editor"): <c>Enter</c> on a conflicted file of the Unstaged list,
/// or <c>&lt;space&gt;to</c> from any pane (the file under the Unstaged cursor, else the first conflicted one), reads
/// it off the UI thread and opens the full-screen editor: the whole file in the base, ours and theirs panes and in the
/// result below, which is a text editor of its own (<see cref="ConflictEditorState"/>). A cursor moves in a pane;
/// on a conflict the pick keys copy that pane's version into the result, in a Visual selection only the selected
/// lines; the result can be typed in (Normal and Insert mode as in the commit box); every change can be undone and
/// redone, and <c>R</c> re-creates the conflict of the file after a Confirm. <c>Enter</c> saves the result to the
/// working tree file, after a Confirm when conflicts are unresolved (they keep their conflict markers), and then
/// offers to mark the file resolved with the usual Confirm. The editor owns the keys while it is open; a Confirm
/// opens on top of it.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="reader">Reads the conflicted file into conflicts.</param>
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
/// <param name="setMode">Switches the keymap mode (Insert while the result is typed in, Visual while lines are selected).</param>
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
    private const string NothingPickedNotice = "Nothing is picked or edited yet: nothing to save";
    private const string NoBaseNotice = "This conflict has no common ancestor: set merge.conflictStyle=diff3, then R";
    private const string NotInVersionNotice = "Move to the base, ours or theirs pane to take a version (Tab)";
    private const string NoLinesNotice = "The selection holds no line of the current conflict: nothing was taken";
    private const string SelectInVersionNotice = "Lines are selected in the base, ours or theirs pane (Tab)";
    private const string NoConflictsNotice = "No conflicts to resolve: no file is conflicted";
    private const string NotLoadedNotice = "The working tree is not loaded yet: try again in a moment";
    private const string ChangedNotice = "The tab changed while the file was read: the conflict editor was not opened";
    private const string CoveredNotice =
        "Another view opened while the file was read: the conflict editor was not opened";
    private const string FileChangedNotice = "The file changed on disk since it was opened: nothing was saved";
    private const string MergetoolHint = "use <space>tm for the mergetool";
    private const int StatusBarRows = 1;
    private const int DefaultPaneRows = 10;

private static readonly Dictionary<ActionId, ConflictResultCommand> ResultCommands = new()
    {
        [ActionIds.WorkingTreeMessageLeft] = ConflictResultCommand.Left,
        [ActionIds.WorkingTreeMessageRight] = ConflictResultCommand.Right,
        [ActionIds.WorkingTreeMessageWordNext] = ConflictResultCommand.WordNext,
        [ActionIds.WorkingTreeMessageWordPrevious] = ConflictResultCommand.WordPrevious,
        [ActionIds.WorkingTreeMessageLineStart] = ConflictResultCommand.LineStart,
        [ActionIds.WorkingTreeMessageLineEnd] = ConflictResultCommand.LineEnd,
        [ActionIds.WorkingTreeMessageDeleteChar] = ConflictResultCommand.DeleteChar,
        [ActionIds.WorkingTreeMessageDeleteLine] = ConflictResultCommand.DeleteLine,
        [ActionIds.WorkingTreeMessageOpenLine] = ConflictResultCommand.OpenLine,
        [ActionIds.WorkingTreeMessageAppend] = ConflictResultCommand.Append,
        [ActionIds.WorkingTreeMessageAppendAtEnd] = ConflictResultCommand.AppendAtEnd,
        [ActionIds.WorkingTreeMessageInsertAtStart] = ConflictResultCommand.InsertAtStart,
    };

    private int _generation;
    private bool _isWorking;
    private KeyMode _mode = KeyMode.Normal;

    /// <summary>The open editor, null while it is closed.</summary>
    public ConflictEditorState? State { get; private set; }

    public bool IsOpen => State is not null;

    /// <summary>True while the result is typed in (the keymap is in Insert mode).</summary>
    public bool IsEditing => State is { IsEditing: true };

    /// <summary>True while the cursor is in the result pane.</summary>
    public bool IsResultActive => State is { ActivePane: ConflictPane.Result };

    /// <summary>True while lines are selected in a version pane.</summary>
    public bool IsVisual => State is { IsVisual: true };

    /// <summary>The keymap view of the pane the cursor is in (the result's editor keys, or the versions' pick keys).</summary>
    public string ViewName => IsResultActive ? DefaultKeymap.ConflictResultView : DefaultKeymap.ConflictSideView;

    /// <summary>The frame to draw, or null when closed, covered by a dialog or the screen is too small.</summary>
    public OverlayFrame? CurrentFrame()
    {
        if (State is not { } state || isOtherOverlayOpen())
        {
            return null;
        }

        var size = EditorArea();

        return ConflictEditorFrameBuilder.Build(state, size.Width, size.Height, glyphs, keys);
    }

    /// <summary>
    /// Handles an action: opens the editor on <see cref="ActionIds.ConflictOpen"/> (any pane), or on <c>Enter</c>
    /// over a conflicted file of the Unstaged list; while it is open and no dialog covers it, consumes every action
    /// (nothing behind it may move). Returns true when the action was consumed.
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

        Execute(state, action, count);

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

        var size = EditorArea();
        var view = ConflictEditorFrameBuilder.ChipAt(State, frameLine, frameColumn, size.Width, size.Height, keys);
        handled = view is not null;

        return view;
    }

    /// <summary>Handles a key typed while the result is in Insert mode; false when it is not.</summary>
    public bool HandleTextKey(KeyEvent key)
    {
        if (State is not { IsEditing: true } state || host.Workspace.ActiveTabId != state.TabId)
        {
            return false;
        }

        Update(state.TypeKey(key));

        return true;
    }

    public void Close()
    {
        _generation++;
        State = null;
        SetMode(KeyMode.Normal);
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

        // <space>to works from any pane: the file under the Unstaged cursor, else the first conflicted one. Enter keeps
        // its meaning on every other row.
        var entry = isOpenKey ? ConflictedToOpen(tab) : ConflictedUnderCursor(tab);
        if (entry is null)
        {
            if (isOpenKey)
            {
                statuses.Warning(tab.WorkingTree is null ? NotLoadedNotice : NoConflictsNotice);
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

        if (!ConflictEditorSupport.CanOpen(entry.Conflict))
        {
            statuses.Warning(
                $"{name}: {ConflictEditorSupport.Refusal(entry.Conflict)}, not for the editor; {MergetoolHint}");

            return;
        }

        var generation = ++_generation;
        var token = cancellationOf(tab.Id);
        _isWorking = true;
        _ = Task.Run(() => ReadAsync(tab.Id, tab.Location, entry, generation, token), CancellationToken.None);
    }

    private async Task ReadAsync(
        TabId tabId,
        RepositoryLocation location,
        StatusEntry entry,
        int generation,
        CancellationToken token)
    {
        var path = entry.Path;
        try
        {
            var result = await reader.ReadAsync(location, path, token).ConfigureAwait(false);
            postToUi(() => Show(generation, tabId, entry, result));
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

    private void Show(int generation, TabId tabId, StatusEntry entry, ConflictReadResult result)
    {
        var path = entry.Path;
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

        var opened = ConflictEditorState.Create(tabId, path, result.Parts);
        var name = WorkingTreeRowRenderer.Printable(path);
        if (opened.Format.HadMixedLineEndings)
        {
            var ending = opened.Format.LineEnding == "\n" ? "LF" : "CRLF";
            statuses.Warning($"{name} mixes CRLF and LF line endings: saving writes {ending} everywhere");
        }
        else if (entry.Conflict == ConflictKind.BothAdded)
        {
            statuses.Post($"{name}: both sides added it, so the base is empty; pick a side per conflict");
        }

        Update(opened);
    }

    private void Fail(string text)
    {
        _isWorking = false;
        statuses.Error(text);
        redraw();
    }

    private void Execute(ConflictEditorState state, ActionId action, int? count)
    {
        if (state.IsEditing)
        {
            ExecuteInEdit(state, action);
        }
        else if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit)
        {
            ExecuteEscape(state, action);
        }
        else if (!ExecuteMove(state, action, count) && !ExecutePick(state, action) && !ExecuteResult(state, action))
        {
            ExecuteFile(state, action);
        }
    }

    // Esc ends a Visual selection first, then closes (a changed file asks for a second Esc).
    private void ExecuteEscape(ConflictEditorState state, ActionId action)
    {
        if (state.IsVisual && action != ActionIds.AppQuit)
        {
            Update(state.EndVisual());
        }
        else
        {
            RequestClose(state.EndVisual());
        }
    }

    private bool ExecuteMove(ConflictEditorState state, ActionId action, int? count)
    {
        var steps = Math.Max(1, count ?? 1);
        if (action == ActionIds.ListDown || action == ActionIds.ListUp)
        {
            Update(state.MoveCursor(action == ActionIds.ListDown ? steps : -steps));
        }
        else if (action == ActionIds.ListHalfPageDown || action == ActionIds.ListHalfPageUp)
        {
            var half = Math.Max(1, PaneRows(state) / 2);
            Update(state.MoveCursor(action == ActionIds.ListHalfPageDown ? half : -half));
        }
        else if (action == ActionIds.ListTop)
        {
            Update(state.MoveToTop());
        }
        else if (action == ActionIds.ListBottom)
        {
            Update(state.MoveToBottom());
        }
        else if (action == ActionIds.ConflictEditorNext)
        {
            Update(state.NextConflict());
        }
        else if (action == ActionIds.ConflictEditorPrevious)
        {
            Update(state.PreviousConflict());
        }
        else
        {
            return ExecutePane(state, action);
        }

        return true;
    }

    private bool ExecutePane(ConflictEditorState state, ActionId action)
    {
        if (action == ActionIds.PaneNext)
        {
            Update(state.NextPane());
        }
        else if (action == ActionIds.PanePrevious)
        {
            Update(state.PreviousPane());
        }
        else if (action == ActionIds.PaneFocusLeft)
        {
            Update(state.PaneLeft());
        }
        else if (action == ActionIds.PaneFocusRight)
        {
            Update(state.PaneRight());
        }
        else if (action == ActionIds.PaneFocusDown)
        {
            Update(state.PaneDown());
        }
        else if (action == ActionIds.PaneFocusUp)
        {
            Update(state.PaneUp());
        }
        else if (action == ActionIds.VisualToggle)
        {
            ToggleVisual(state);
        }
        else
        {
            return action == ActionIds.VisualCopy;
        }

        return true;
    }

    private void ToggleVisual(ConflictEditorState state)
    {
        if (!state.IsVersionPane)
        {
            statuses.Warning(SelectInVersionNotice);

            return;
        }

        Update(state.ToggleVisual());
    }

    private bool ExecutePick(ConflictEditorState state, ActionId action)
    {
        if (action == ActionIds.ConflictEditorTake)
        {
            Take(state);
        }
        else if (action == ActionIds.ConflictEditorOurs)
        {
            Take(state, ConflictPick.Ours);
        }
        else if (action == ActionIds.ConflictEditorTheirs)
        {
            Take(state, ConflictPick.Theirs);
        }
        else if (action == ActionIds.ConflictEditorBase)
        {
            Take(state, ConflictPick.Base);
        }
        else if (action == ActionIds.ConflictEditorBoth)
        {
            Take(state, ConflictPick.Both);
        }
        else if (action == ActionIds.ConflictEditorAllOurs)
        {
            Update(state.PickAllUnresolved(ConflictPick.Ours));
        }
        else if (action == ActionIds.ConflictEditorAllTheirs)
        {
            Update(state.PickAllUnresolved(ConflictPick.Theirs));
        }
        else if (action == ActionIds.ConflictEditorReset)
        {
            Update(state.Reset());
        }
        else if (action == ActionIds.ConflictEditorUndo)
        {
            Update(state.Undo());
        }
        else if (action == ActionIds.ConflictEditorRedo)
        {
            Update(state.Redo());
        }
        else
        {
            return false;
        }

        return true;
    }

    // The pick key of a version pane copies that pane's version (or the Visual selection's lines); the named picks work
    // from any pane.
    private void Take(ConflictEditorState state)
    {
        if (state.IsVisual)
        {
            TakeSelection(state);
        }
        else if (!state.IsVersionPane)
        {
            statuses.Warning(NotInVersionNotice);
        }
        else
        {
            Take(state, PickOf(state.ActivePane));
        }
    }

    private static ConflictPick PickOf(ConflictPane pane) => pane switch
    {
        ConflictPane.Base => ConflictPick.Base,
        ConflictPane.Ours => ConflictPick.Ours,
        _ => ConflictPick.Theirs,
    };

    private void Take(ConflictEditorState state, ConflictPick pick)
    {
        if (pick == ConflictPick.Base && !state.CanTake(pick))
        {
            statuses.Warning(NoBaseNotice);

            return;
        }

        Update(state.Take(pick));
    }

    private void TakeSelection(ConflictEditorState state)
    {
        if (state.SelectedLines().Count == 0)
        {
            statuses.Warning(NoLinesNotice);
        }

        Update(state.TakeSelectedLines());
    }

    private bool ExecuteResult(ConflictEditorState state, ActionId action)
    {
        if (action == ActionIds.ConflictEditorEdit)
        {
            BeginEdit(state);
        }
        else if (ResultCommands.TryGetValue(action, out var command))
        {
            Update(state.ApplyResultCommand(command));
        }
        else
        {
            return false;
        }

        return true;
    }

    private void ExecuteFile(ConflictEditorState state, ActionId action)
    {
        if (action == ActionIds.ConflictEditorRecreate)
        {
            AskRecreate(state);
        }
        else if (action == ActionIds.ConflictEditorSave)
        {
            Save(state);
        }
    }

    // Esc keeps the text like Ctrl-g does: a typing session is one step of undo, so nothing is lost by leaving it.
    private void ExecuteInEdit(ConflictEditorState state, ActionId action)
    {
        if (action == ActionIds.ConflictEditorEditNewLine)
        {
            Update(state.TypeKey(new KeyEvent(KeyNames.Enter)));
        }
        else if (action == ActionIds.ConflictEditorEditDone
            || action == ActionIds.Cancel
            || action == ActionIds.NavigateBack)
        {
            Update(state.EndEdit());
        }
    }

    // The whole screen but the status bar; the editor covers the tab row and the operation banner too.
    private ScreenSize EditorArea()
    {
        var size = screenSize();

        return size with { Height = Math.Max(0, size.Height - StatusBarRows) };
    }

    // Rows of the pane the cursor is in, for the half page keys.
    private int PaneRows(ConflictEditorState state)
    {
        var size = EditorArea();
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, size.Width, size.Height, keys);
        if (layout is null)
        {
            return DefaultPaneRows;
        }

        return state.IsVersionPane ? layout.VersionRows : layout.ResultRows;
    }

    private void BeginEdit(ConflictEditorState state)
    {
        var edited = state.BeginEdit();
        if (edited.IsEditing)
        {
            Update(edited);
        }
    }

    private void Update(ConflictEditorState state)
    {
        State = Followed(state);
        SetMode(DesiredMode(State));
        redraw();
    }

    // Scrolls the panes so the cursor shows at the current screen size.
    private ConflictEditorState Followed(ConflictEditorState state)
    {
        var size = EditorArea();
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, size.Width, size.Height, keys);

        return layout is null ? state : state.Follow(layout.VersionRows, layout.ResultRows);
    }

    private static KeyMode DesiredMode(ConflictEditorState state)
    {
        if (state.IsEditing)
        {
            return KeyMode.Insert;
        }

        return state.IsVisual ? KeyMode.Visual : KeyMode.Normal;
    }

    private void SetMode(KeyMode mode)
    {
        if (mode != _mode)
        {
            _mode = mode;
            setMode(mode);
        }
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
            "Save with unresolved conflicts",
            [
                $"Save {name} with {Plural(state.UnresolvedCount, "unresolved conflict")} out of {state.Hunks.Count}?",
                "The unresolved conflicts keep their conflict markers and the file stays conflicted.",
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

        statuses.Post($"Re-created the conflict of {name}");
        Update(ConflictEditorState.Create(tabId, old.Path, result.Parts));
    }

    private static StatusEntry? ConflictedToOpen(RepositoryState tab) =>
        ConflictedUnderCursor(tab) ?? ConflictedSelection.FindFirst(tab.WorkingTree);

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
            return $"Saved {name}: every conflict resolved";
        }

        return $"Saved {name}: {Plural(state.UnresolvedCount, "conflict")} still unresolved";
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
