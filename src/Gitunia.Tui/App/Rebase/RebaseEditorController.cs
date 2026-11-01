using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Core.Rebase;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Rebase;

/// <summary>
/// The interactive rebase's todo editor (ux-workflows.md, workflow 3, "Interactive rebase"): <c>&lt;space&gt;bri</c> reads the
/// commits the target does not have and opens a modal list of them in replay order. Each commit gets an action
/// (pick, reword, squash, fixup, drop), commits move up and down, a reword opens the multi-line message editor, and
/// <c>Enter</c> asks for a Confirm that names what is kept and dropped and the undo, then hands the todo to the engine
/// (<see cref="IInteractiveRebaseWriter"/>). A stop on conflicts is the in-progress rebase of workflow 4. The editor
/// owns the keys while it is open; the Confirm opens on top of it, so declining keeps the todo.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Reads the range and runs the rebase.</param>
/// <param name="details">Reads the full message of a commit that is reworded.</param>
/// <param name="statuses">Where refusals, failures and the outcome are reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="refresh">Reads a tab again (refs, graph, status, operation); false when it cannot.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
/// <param name="isOtherOverlayOpen">True while an overlay other than this editor is open.</param>
/// <param name="screenSize">The current terminal size.</param>
/// <param name="glyphs">The characters the editor is drawn with.</param>
/// <param name="setMode">Switches the keymap mode (Insert while a message is edited).</param>
/// <param name="redraw">Redraws the overlay layer and the status bar.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with staging, discard, reset and stash.</param>
/// <param name="keys">Where the chips take their key text from; null uses the default keymap.</param>
public sealed class RebaseEditorController(
    Dispatcher host,
    IInteractiveRebaseWriter writer,
    ICommitDetailsReader details,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<TabId, bool> refresh,
    Func<BranchSelection> selection,
    Func<bool> isOtherOverlayOpen,
    Func<ScreenSize> screenSize,
    GraphGlyphSet glyphs,
    Action<KeyMode> setMode,
    Action redraw,
    TabOperationGate gate,
    ChipKeys? keys = null)
{
    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) to rebase onto";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was rebased";
    private const string ChangedNotice = "The tab changed while the todo was open: nothing was rebased";
    private const string RootLabel = "the root";
    private const string EmptyRangeNotice = "Nothing to rebase: the branch has no commits the target lacks";

    private int _generation;
    private bool _isWorking;

    /// <summary>The open editor, null while it is closed.</summary>
    public RebaseEditorState? State { get; private set; }

    public bool IsOpen => State is not null;

    /// <summary>True while the reword's message editor is open (the keymap is in Insert mode).</summary>
    public bool IsEditingMessage => State?.Edit is not null;

    /// <summary>The frame to draw, or null when closed, covered by a dialog or the screen is too small.</summary>
    public OverlayFrame? CurrentFrame()
    {
        if (State is not { } state || isOtherOverlayOpen())
        {
            return null;
        }

        var size = screenSize();
        return RebaseEditorFrameBuilder.Build(state, size.Width, size.Height, glyphs, keys);
    }

    /// <summary>
    /// Handles an action: opens the editor on <see cref="ActionIds.RebaseInteractive"/> or
    /// <see cref="ActionIds.RebaseInteractiveRoot"/> and, while it is open and no dialog covers it, consumes every action (nothing behind it may move). Returns true when the action was consumed.
    /// </summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (State is not { } state)
        {
            if (action != ActionIds.RebaseInteractive && action != ActionIds.RebaseInteractiveRoot)
            {
                return false;
            }

            Begin(action == ActionIds.RebaseInteractiveRoot);
            return true;
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

        if (state.Edit is not null)
        {
            ExecuteInMessage(state, action);
        }
        else
        {
            ExecuteInList(state, action, count);
        }

        return true;
    }

    /// <summary>
    /// A click on a frame line and column: a commit row is highlighted (handled, no view); a chip returns its mouse
    /// view for the caller to send through the mouse map. Returns null when the click hit no chip.
    /// </summary>
    public string? OnLineClicked(int frameLine, int frameColumn, out bool handled)
    {
        handled = false;
        if (State is not { Edit: null } state || isOtherOverlayOpen())
        {
            return null;
        }

        var size = screenSize();
        if (RebaseEditorFrameBuilder.EntryIndexAt(state, frameLine, size.Width, size.Height, keys) is { } index)
        {
            State = state.MoveTo(index);
            handled = true;
            redraw();
            return null;
        }

        var view = RebaseEditorFrameBuilder.ChipAt(state, frameLine, frameColumn, size.Width, size.Height, keys);
        handled = view is not null;
        return view;
    }

    /// <summary>Handles a key typed while a message is edited; false when no message is being edited.</summary>
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
        if (State is { Edit: not null })
        {
            setMode(KeyMode.Normal);
        }

        State = null;
        redraw();
    }

    // Why a rebase onto the selection (or of the whole history) cannot start: the same refusals as the plain rebase.
    private void Begin(bool isRoot)
    {
        if (host.Workspace.ActiveTab is not { } tab || isOtherOverlayOpen() || _isWorking)
        {
            return;
        }

        var chosen = selection();
        var isTarget = chosen.RowKind is null or SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch
            or SidebarRowKind.Tag;
        if (!isRoot && (!isTarget || chosen.CommitHash is null))
        {
            statuses.Warning(SelectNotice);
            return;
        }

        var hash = isRoot ? null : chosen.CommitHash;

        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning(BusyNotice);
            return;
        }

        if (RebaseGuard.Refusal(tab, hash) is { } refusal)
        {
            statuses.Warning(refusal);
            return;
        }

        var label = isRoot ? RootLabel : chosen.Name ?? ShortHash.Of(hash!);
        var generation = ++_generation;
        var location = tab.Location;
        var token = cancellationOf(tab.Id);
        _isWorking = true;
        var branchName = tab.Head!.BranchName!;
        var oldTip = tab.Head.CommitHash!;
        _ = Task.Run(
            () => ReadRangeAsync(tab.Id, location, hash, label, branchName, oldTip, generation, token),
            CancellationToken.None);
    }

    private async Task ReadRangeAsync(
        TabId tabId,
        RepositoryLocation location,
        string? targetHash,
        string label,
        string branchName,
        string oldTip,
        int generation,
        CancellationToken token)
    {
        try
        {
            var rebaseBase = BaseOf(targetHash);
            var commits = await writer.ReadRangeAsync(location, rebaseBase, token).ConfigureAwait(false);
            var state = RebaseEditorState.Create(
                tabId,
                branchName,
                label,
                rebaseBase,
                oldTip,
                commits);
            postToUi(() => Show(generation, state, targetHash));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _isWorking = false);
        }
        catch (RebaseRangeException exception)
        {
            postToUi(() => Fail(exception.Message, StatusSeverity.Warning));
        }
        catch (Exception exception)
        {
            var text = $"Could not read the commits to rebase: {OperationText.Reason(exception)}";
            postToUi(() => Fail(text, StatusSeverity.Error));
        }
    }

    private void Show(int generation, RebaseEditorState state, string? targetHash)
    {
        _isWorking = false;
        if (generation != _generation)
        {
            return;
        }

        // The range was read for the state of a moment ago.
        if (host.Workspace.FindTab(state.TabId) is not { } tab
            || isOtherOverlayOpen()
            || !string.Equals(tab.Head?.CommitHash, state.OldTip, StringComparison.Ordinal)
            || RebaseGuard.Refusal(tab, targetHash) is not null)
        {
            statuses.Warning(ChangedNotice);
            return;
        }

        if (state.Rows.Count == 0)
        {
            statuses.Warning(EmptyRangeNotice);
            return;
        }

        State = state;
        redraw();
    }

    private void Fail(string text, StatusSeverity severity)
    {
        _isWorking = false;
        statuses.Post(text, severity);
        redraw();
    }

    private void ExecuteInList(RebaseEditorState state, ActionId action, int? count)
    {
        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit)
        {
            RequestClose(state);
        }
        else if (action == ActionIds.RebaseEditorPick)
        {
            Update(state.SetAction(RebaseTodoAction.Pick));
        }
        else if (action == ActionIds.RebaseEditorSquash)
        {
            Update(state.SetAction(RebaseTodoAction.Squash));
        }
        else if (action == ActionIds.RebaseEditorFixup)
        {
            Update(state.SetAction(RebaseTodoAction.Fixup));
        }
        else if (action == ActionIds.RebaseEditorDrop)
        {
            Update(state.SetAction(RebaseTodoAction.Drop));
        }
        else if (action == ActionIds.RebaseEditorMoveUp)
        {
            Update(state.MoveRow(-Math.Max(1, count ?? 1)));
        }
        else if (action == ActionIds.RebaseEditorMoveDown)
        {
            Update(state.MoveRow(Math.Max(1, count ?? 1)));
        }
        else if (action == ActionIds.RebaseEditorReword)
        {
            BeginReword(state);
        }
        else if (action == ActionIds.RebaseEditorStart)
        {
            Ask(state);
        }
        else
        {
            Move(state, action, count);
        }
    }

    private void ExecuteInMessage(RebaseEditorState state, ActionId action)
    {
        if (action == ActionIds.RebaseEditorMessageSave)
        {
            if (state.Edit!.Editor.IsBlank)
            {
                statuses.Warning("A reword needs a message: type one, or press Esc to cancel");
                return;
            }

            setMode(KeyMode.Normal);
            Update(state.SaveEdit());
        }
        else if (action == ActionIds.RebaseEditorMessageNewLine)
        {
            state.Edit!.Editor.Apply(new KeyEvent(KeyNames.Enter));
            redraw();
        }
        else if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            setMode(KeyMode.Normal);
            Update(state.CancelEdit());
        }
    }

    private void Update(RebaseEditorState state)
    {
        State = state;
        redraw();
    }

    // An edited todo is not thrown away by one stray Esc: the first press warns, the second closes.
    private void RequestClose(RebaseEditorState state)
    {
        if (state.IsModified && !state.IsDiscardPending)
        {
            Update(state.AskDiscard());
            return;
        }

        Close();
    }

    private void Move(RebaseEditorState state, ActionId action, int? count)
    {
        var size = screenSize();
        var rows = RebaseEditorFrameBuilder.VisibleRows(state, size.Width, size.Height, keys);
        if (ListMotion.TryMove(action, count, state.SelectedIndex, state.Rows.Count, rows, out var target))
        {
            Update(state.MoveTo(target));
        }
    }

    // The editor starts with the commit's full message, not only its subject, so the body is not lost by a reword.
    private void BeginReword(RebaseEditorState state)
    {
        if (state.Selected is not { } row || _isWorking)
        {
            return;
        }

        if (row.Message is { } message)
        {
            OpenMessage(state, message);
            return;
        }

        if (host.Workspace.FindTab(state.TabId) is not { } tab || !ObjectId.TryParse(row.Hash, out var id))
        {
            OpenMessage(state, row.Subject);
            return;
        }

        var generation = ++_generation;
        var location = tab.Location;
        var token = cancellationOf(tab.Id);
        _isWorking = true;
        _ = Task.Run(() => ReadMessageAsync(row, id, location, generation, token), CancellationToken.None);
    }

    private async Task ReadMessageAsync(
        RebaseEditorRow row,
        ObjectId id,
        RepositoryLocation location,
        int generation,
        CancellationToken token)
    {
        // The subject is a safe start when git cannot deliver the whole message.
        var text = row.Subject;
        try
        {
            var read = await details.ReadAsync(location, id, null, token).ConfigureAwait(false);
            text = read?.Message ?? text;
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _isWorking = false);
            return;
        }
        catch (Exception)
        {
            // Kept: the editor opens with the subject.
        }

        postToUi(() => MessageRead(generation, row.Hash, text));
    }

    private void MessageRead(int generation, string hash, string message)
    {
        _isWorking = false;
        if (generation != _generation || State is not { Edit: null } current || current.Selected?.Hash != hash)
        {
            return;
        }

        OpenMessage(current, message);
    }

    private void OpenMessage(RebaseEditorState state, string message)
    {
        State = state.BeginEdit(message);
        setMode(KeyMode.Insert);
        redraw();
    }

    private void Ask(RebaseEditorState state)
    {
        if (state.Problem is { } problem)
        {
            statuses.Warning(problem);
            return;
        }

        if (state.Rows.Count == 0)
        {
            return;
        }

        var tab = host.Workspace.FindTab(state.TabId);
        var current = tab?.Refs
            .FirstOrDefault(candidate => candidate.IsCurrent && candidate.Kind == RefKind.LocalBranch);
        var note = RebaseGuard.AutoStashNote(tab);
        if (!confirm(RebaseEditorTexts.ConfirmFor(state, current, note), () => Start(state)))
        {
            statuses.Warning(TooSmallNotice);
        }
    }

    // The Confirm outlives nothing but a moment, yet the tab can have changed under it.
    private void Start(RebaseEditorState state)
    {
        if (host.Workspace.FindTab(state.TabId) is not { } tab)
        {
            return;
        }

        if (gate.IsBusy(tab.Id))
        {
            statuses.Warning($"{BusyNotice}; nothing was rebased");
            return;
        }

        var moved = !string.Equals(tab.Head?.CommitHash, state.OldTip, StringComparison.Ordinal);
        var refusal = RebaseGuard.Refusal(tab, state.Base.Onto);
        if (moved || refusal is not null)
        {
            statuses.Warning(refusal ?? ChangedNotice);
            return;
        }

        Close();
        gate.Begin(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RebaseAsync(tab, state, token), CancellationToken.None);
    }

    private async Task RebaseAsync(RepositoryState tab, RebaseEditorState state, CancellationToken token)
    {
        try
        {
            var result = await writer.RebaseAsync(tab.Location, state.Base, state.ToEntries(), token)
                .ConfigureAwait(false);
            var report = RebaseEditorTexts.ReportOf(state, result);
            postToUi(() => Finish(tab.Id, report.Text, report.Severity));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            postToUi(() => gate.End(tab.Id));
        }
        catch (Exception exception) when (exception is RebaseRangeException or RebaseEnvironmentException)
        {
            // Nothing ran: the todo comes back so the work of editing it is not lost.
            var text = $"Could not start the rebase: {exception.Message}";
            postToUi(() => Reopen(state, text));
        }
        catch (Exception exception)
        {
            var reason = OperationText.Reason(exception);
            var text = $"Could not rebase {state.BranchName} onto {state.TargetLabel}: {reason}";
            postToUi(() => Finish(tab.Id, text, StatusSeverity.Error));
        }
    }

    private void Reopen(RebaseEditorState state, string text)
    {
        gate.End(state.TabId);
        statuses.Error(text);
        if (host.Workspace.FindTab(state.TabId) is not null && !isOtherOverlayOpen() && State is null)
        {
            State = state;
        }

        redraw();
    }

    private void Finish(TabId tabId, string message, StatusSeverity severity)
    {
        statuses.Post(message, severity);
        gate.End(tabId);
        if (host.Workspace.FindTab(tabId) is not null)
        {
            refresh(tabId);
        }
    }

    private static RebaseBase BaseOf(string? targetHash) =>
        (targetHash is null ? RebaseBase.Root : RebaseBase.OntoCommit(targetHash)).WithMerges();

}
