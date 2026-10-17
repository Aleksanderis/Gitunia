using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Reflog;
using Gitunia.Core.Search;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Reflog;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Reflog;

/// <summary>
/// The reflog list (ux.md, workflow 3, "Reflog"): a modal list of the reflog of HEAD, or of the local branch under the
/// sidebar cursor, read from git when it opens and on refresh. It owns the keys while it is open. An entry can be
/// selected in the graph, reset to (the reset flow of #392), branched from or checked out detached; those three close
/// the list first because their prompts and dialogs live in the status bar and the confirm overlay.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="reader">Reads the reflog.</param>
/// <param name="reset">The reset flow an entry starts.</param>
/// <param name="branches">The branch actions an entry starts.</param>
/// <param name="statuses">Where refusals and failures are reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="selectCommit">Selects a commit in the tab's graph; returns why it could not, empty when it did.</param>
/// <param name="selection">What the sidebar cursor is on, to pick the branch whose reflog opens.</param>
/// <param name="screenSize">The current terminal size.</param>
/// <param name="glyphs">The characters the list is drawn with.</param>
/// <param name="setMode">Switches the keymap mode (Insert while the search box or the ref picker takes text).</param>
/// <param name="redraw">Redraws the overlay layer and the status bar.</param>
public sealed class ReflogController(
    WorkspaceHost host,
    IReflogReader reader,
    ResetActions reset,
    BranchActions branches,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<TabId, string, string> selectCommit,
    Func<BranchSelection> selection,
    Func<ScreenSize> screenSize,
    GraphGlyphSet glyphs,
    Action<KeyMode> setMode,
    Action redraw)
{
    /// <summary>Entries read per open; older ones are not listed.</summary>
    public const int EntryLimit = 1000;

    private const string HeadName = "HEAD";
    private const string NoEntryNotice = "No reflog entry selected";

    private int _generation;
    private bool _isReading;

    /// <summary>The open list, null while it is closed.</summary>
    public ReflogState? State { get; private set; }

    public bool IsOpen => State is not null;

    /// <summary>The frame to draw, or null when the list is closed or the screen is too small for it.</summary>
    public OverlayFrame? CurrentFrame()
    {
        var size = screenSize();
        return State is { } state ? ReflogFrameBuilder.Build(state, size.Width, size.Height, glyphs) : null;
    }

    /// <summary>
    /// Handles an action: opens the list on <see cref="ActionIds.ReflogOpen"/> and, while it is open, consumes every
    /// action (nothing behind it may move). Returns true when the action was consumed.
    /// </summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (State is not { } state)
        {
            if (action != ActionIds.ReflogOpen)
            {
                return false;
            }

            Open();
            return true;
        }

        if (host.Workspace.FindTab(state.TabId) is null)
        {
            Close();
            return false;
        }

        if (state.Picker is { } picker)
        {
            ExecuteInPicker(state, picker, action);
        }
        else if (state.IsEditingSearch)
        {
            ExecuteInSearchBox(state, action);
        }
        else if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            if (state.HasSearch)
            {
                State = state.ClearSearch();
                redraw();
            }
            else
            {
                Close();
            }
        }
        else if (action == ActionIds.AppQuit)
        {
            Close();
        }
        else if (action == ActionIds.SearchOpen)
        {
            State = state.OpenSearch();
            setMode(KeyMode.Insert);
            redraw();
        }
        else if (action == ActionIds.SearchNext || action == ActionIds.SearchPrevious)
        {
            StepMatch(state, action == ActionIds.SearchNext, count);
        }
        else if (action == ActionIds.ReflogOpen)
        {
            OpenPicker(state);
        }
        else if (action == ActionIds.ReflogSelectCommit)
        {
            SelectCommit(state);
        }
        else if (action == ActionIds.RepositoryRefresh)
        {
            Read(state.TabId, state.ReferenceName, state.Title, state);
        }
        else if (action == ActionIds.ResetBranch
            || action == ActionIds.BranchCreate
            || action == ActionIds.BranchSwitch)
        {
            StartOnEntry(state, action);
        }
        else
        {
            Move(state, action, count);
        }

        return true;
    }

    /// <summary>Highlights the entry drawn on a frame line (a mouse click); false when none.</summary>
    public bool SelectEntryAt(int frameLine)
    {
        if (State is not { Picker: null } state
            || ReflogFrameBuilder.EntryIndexAt(state, frameLine, screenSize().Height) is not { } index)
        {
            return false;
        }

        State = state.MoveTo(index);
        redraw();
        return true;
    }

    /// <summary>A click on a frame line: highlights an entry, picks a ref or runs an action; false when none.</summary>
    public bool OnLineClicked(int frameLine) => SelectEntryAt(frameLine) || ActivateLine(frameLine);

    /// <summary>Runs the action drawn on a frame line (a click on one of the entry actions); false when none.</summary>
    public bool ActivateLine(int frameLine)
    {
        if (State is not { } state)
        {
            return false;
        }

        if (state.Picker is { } picker)
        {
            if (ReflogFrameBuilder.PickerIndexAt(picker, frameLine, screenSize().Height) is not { } index)
            {
                return false;
            }

            var moved = picker.MoveTo(index);
            State = state with { Picker = moved };
            Accept(moved);
            return true;
        }

        if (state.IsEditingSearch
            || ReflogFrameBuilder.ActionAt(state, frameLine, screenSize().Height) is not { } action)
        {
            return false;
        }

        return TryExecute(action, null);
    }

    /// <summary>Handles a key typed while the search box or the ref picker is open; false when neither is.</summary>
    public bool HandleTextKey(KeyEvent key)
    {
        if (State is not { } state || host.Workspace.ActiveTabId != state.TabId)
        {
            return false;
        }

        if (state.Picker is { } picker)
        {
            var normalized = key.Normalize();
            if (normalized is { Name: KeyNames.Up or KeyNames.Down or KeyNames.Tab, Modifiers: KeyModifiers.None })
            {
                State = state with { Picker = picker.Move(normalized.Name == KeyNames.Up ? -1 : 1) };
            }
            else if (PromptTextEditor.Apply(picker.Text, key) is { } text && text != picker.Text)
            {
                State = state with { Picker = picker.WithText(text) };
            }

            redraw();
            return true;
        }

        if (!state.IsEditingSearch)
        {
            return false;
        }

        if (PromptTextEditor.Apply(state.SearchText, key) is { } edited && edited != state.SearchText)
        {
            State = state.EditSearch(edited);
            redraw();
        }

        return true;
    }

    public void Close()
    {
        _generation++;
        if (State is { IsEditingSearch: true } or { Picker: not null })
        {
            setMode(KeyMode.Normal);
        }

        State = null;
        redraw();
    }

    private void ExecuteInPicker(ReflogState state, ReflogRefPicker picker, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            Accept(picker);
        }
        else if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            State = state with { Picker = null };
            setMode(KeyMode.Normal);
            redraw();
        }
        else if (action == ActionIds.AppQuit)
        {
            Close();
        }
    }

    private void ExecuteInSearchBox(ReflogState state, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            State = state.AcceptSearch();
            setMode(KeyMode.Normal);
            redraw();
        }
        else if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            State = state.ClearSearch();
            setMode(KeyMode.Normal);
            redraw();
        }
        else if (action == ActionIds.AppQuit)
        {
            Close();
        }
    }

    private void StepMatch(ReflogState state, bool forward, int? count)
    {
        if (state.MatchIndexes.Count == 0)
        {
            statuses.Warning(state.HasSearch ? $"No match for {state.SearchText}" : "No search: press / first");
            return;
        }

        State = state.StepMatch(forward, count ?? 1);
        redraw();
    }

    private void OpenPicker(ReflogState state)
    {
        var names = new List<string> { HeadName };
        if (host.Workspace.FindTab(state.TabId) is { } tab)
        {
            names.AddRange(tab.Refs
                .Where(reference => reference.Kind == RefKind.LocalBranch && reference.SymbolicTarget is null)
                .Select(reference => reference.Name));
        }

        State = state with { Picker = new ReflogRefPicker(names) };
        setMode(KeyMode.Insert);
        redraw();
    }

    private void Accept(ReflogRefPicker picker)
    {
        if (State is not { } state || picker.Selected is not { } name)
        {
            return;
        }

        if (_isReading)
        {
            statuses.Warning("The reflog is still being read: choose again in a moment");
            return;
        }

        setMode(KeyMode.Normal);
        State = state with { Picker = null };
        var isHead = name == HeadName;
        Read(state.TabId, isHead ? HeadName : $"{GitRefPrefixes.Heads}{name}", name, null);
        redraw();
    }

    private void Open()
    {
        if (host.Workspace.ActiveTab is not { } tab || _isReading)
        {
            return;
        }

        var chosen = selection();
        var isBranch = chosen.RowKind == SidebarRowKind.LocalBranch && chosen.Name is not null;
        var reference = isBranch ? $"{GitRefPrefixes.Heads}{chosen.Name}" : HeadName;
        Read(tab.Id, reference, isBranch ? chosen.Name! : HeadName, null);
    }

    // Reads on a worker; the list opens (or updates) only if nothing was opened or closed meanwhile.
    private void Read(TabId tabId, string reference, string title, ReflogState? current)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || _isReading)
        {
            return;
        }

        var generation = ++_generation;
        var location = tab.Location;
        var token = cancellationOf(tabId);
        _isReading = true;
        _ = Task.Run(
            () => ReadAsync(tabId, location, reference, title, current, generation, token),
            CancellationToken.None);
    }

    private async Task ReadAsync(
        TabId tabId,
        RepositoryLocation location,
        string reference,
        string title,
        ReflogState? current,
        int generation,
        CancellationToken token)
    {
        try
        {
            var result = await reader.ReadAsync(location, reference, EntryLimit, token).ConfigureAwait(false);
            postToUi(() => Show(generation, current is null
                ? new ReflogState(tabId, reference, title, result.Items)
                : (State ?? current).WithEntries(result.Items)));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _isReading = false);
        }
        catch (Exception exception)
        {
            var text = $"Could not read the reflog of {title}: {OperationText.Reason(exception)}";
            postToUi(() => Fail(text));
        }
    }

    private void Show(int generation, ReflogState state)
    {
        _isReading = false;
        if (generation != _generation)
        {
            return;
        }

        State = state;
        redraw();
    }

    private void Fail(string text)
    {
        _isReading = false;
        statuses.Error(text);
        redraw();
    }

    private void Move(ReflogState state, ActionId action, int? count)
    {
        var rows = ReflogFrameBuilder.VisibleRows(state, screenSize().Height);
        if (ListMotion.TryMove(action, count, state.SelectedIndex, state.Entries.Count, rows, out var target))
        {
            State = state.MoveTo(target);
            redraw();
        }
    }

    private void SelectCommit(ReflogState state)
    {
        if (state.Selected is not { } entry)
        {
            statuses.Warning(NoEntryNotice);
            return;
        }

        var refusal = selectCommit(state.TabId, entry.Hash);
        if (refusal.Length > 0)
        {
            statuses.Warning(refusal);
        }
    }

    private void StartOnEntry(ReflogState state, ActionId action)
    {
        if (state.Selected is not { } entry)
        {
            statuses.Warning(NoEntryNotice);
            return;
        }

        var refusal = action == ActionIds.ResetBranch
            ? reset.Begin(state.TabId, entry.Hash)
            : action == ActionIds.BranchCreate
                ? branches.BeginCreate(state.TabId, entry.Hash, checkout: true)
                : branches.CheckoutDetached(state.TabId, entry.Hash);
        if (refusal.Length > 0)
        {
            // A refusal keeps the list, so the user does not lose the position.
            statuses.Warning(refusal);
            redraw();
            return;
        }

        Close();
    }
}
