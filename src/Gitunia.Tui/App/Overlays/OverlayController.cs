using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Owns the <see cref="OverlayState"/> of the window and connects it to the keys: opens the help and the palette,
/// consumes the actions and typed text while one is open, and shows the which-key popup for a chord that stays pending.
/// The state transitions themselves are the pure methods of <see cref="OverlayState"/> and <see cref="PaletteState"/>.
/// </summary>
public sealed class OverlayController
{
    /// <summary>How long a chord stays pending before the which-key popup appears.</summary>
    public static readonly TimeSpan WhichKeyDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Whether a pending chord opens the which-key popup (<c>hints.whichKey</c>, #837). Off hides only the popup: the
    /// status bar still shows the pending prefix and every key works. Turning it off drops a wait or popup in progress.
    /// </summary>
    public bool WhichKeyEnabled
    {
        get;
        set
        {
            field = value;
            if (!value)
            {
                _whichKeyTimer.Cancel();
                if (State.WhichKeyVisible)
                {
                    State = State.WithWhichKey(false);
                    Changed?.Invoke();
                }
            }
        }
    } = true;

    /// <summary>
    /// The command level (#839): what the help, the palette, the which-key popup and the actions menus list. Read
    /// each time one opens or redraws, so a change shows at once.
    /// </summary>
    public Func<CommandLevel> Level { get; set; } = static () => CommandLevel.AdvancedPlus;

    private readonly KeymapEngine _engine;
    private readonly KeyController _keys;
    private readonly WhichKeyTimer _whichKeyTimer;
    private readonly Action<ActionId, int?> _run;
    private readonly Func<ScreenSize> _screenSize;
    private readonly GraphGlyphSet _glyphs;
    private readonly Func<string> _baseView;
    private readonly StatusChannel _statuses;
    private IReadOnlyList<HelpTab> _helpTabs = [];
    private Action? _onConfirmed;

    /// <param name="engine">The keymap engine (its table feeds the help and the palette).</param>
    /// <param name="keys">The key controller whose mode this controller switches.</param>
    /// <param name="whichKeyTimer">Delays the which-key popup.</param>
    /// <param name="run">Runs an action chosen in the palette.</param>
    /// <param name="screenSize">The current terminal size.</param>
    /// <param name="glyphs">The characters overlays are drawn with.</param>
    /// <param name="baseView">The keymap view that is focused when no overlay is open (asked each time: it follows the focused pane).</param>
    /// <param name="statuses">The status messages the history overlay lists.</param>
    public OverlayController(
        KeymapEngine engine,
        KeyController keys,
        WhichKeyTimer whichKeyTimer,
        Action<ActionId, int?> run,
        Func<ScreenSize> screenSize,
        GraphGlyphSet glyphs,
        Func<string> baseView,
        StatusChannel statuses)
    {
        _engine = engine;
        _keys = keys;
        _whichKeyTimer = whichKeyTimer;
        _run = run;
        _screenSize = screenSize;
        _glyphs = glyphs;
        _baseView = baseView;
        _statuses = statuses;
        keys.PendingChanged += OnPendingChanged;
        keys.TextKeyPressed += OnTextKey;
        statuses.Changed += OnStatusChanged;
    }

    /// <summary>Raised when what the overlay layer shows may have changed.</summary>
    public event Action? Changed;

    public OverlayState State { get; private set; } = OverlayState.None;

    /// <summary>
    /// What the actions menu of the focused row offers: asked with the keymap view it opens from (the graph or the
    /// sidebar), it returns the row's actions and where the row is on screen, or null when there is no row to act on.
    /// </summary>
    public Func<string, RowMenuTarget?> MenuTarget { get; set; } = _ => null;

    /// <summary>Whether the active tab has a merge, rebase, cherry-pick or revert stopped (the which-key popup then lists continue, abort and skip).</summary>
    public Func<bool> OperationInProgress { get; set; } = () => false;

    /// <summary>The keymap view that is focused: the overlay's own while one is open, else the base view.</summary>
    public string ViewName => State.Kind switch
    {
        OverlayKind.Help => DefaultKeymap.HelpView,
        OverlayKind.Messages or OverlayKind.Output => DefaultKeymap.MessagesView,
        OverlayKind.Palette => DefaultKeymap.PaletteView,
        OverlayKind.ContextMenu => DefaultKeymap.ContextMenuView,
        OverlayKind.Confirm => DefaultKeymap.ConfirmView,
        _ => _baseView(),
    };

    /// <summary>The frame to draw over the screen, or null when nothing is showing.</summary>
    public OverlayFrame? CurrentFrame()
    {
        var size = _screenSize();
        var width = size.Width;
        var height = size.Height;
        var pending = _keys.State.PendingSequence;
        var rows = WhichKeyRowsFor(pending);
        var bannerShown = OperationInProgress() && height >= ScreenLayout.MinimumHeightWithBanner;

        return OverlayFrameFactory.Create(
            State,
            _helpTabs,
            _keys.PendingText,
            rows,
            width,
            height,
            _glyphs,
            bannerShown);
    }

    /// <summary>Handles an action while an overlay may be open; returns true when it was consumed.</summary>
    public bool Handle(ActionId action, int? count)
    {
        if (!State.IsOpen)
        {
            return OpenFor(action);
        }

        if (State.Kind == OverlayKind.Confirm)
        {
            HandleConfirm(action);
        }
        else if (State.Kind == OverlayKind.Help && TryHandleHelpFilter(action))
        {
            Changed?.Invoke();
        }
        else if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit || (State.Kind == OverlayKind.Help && action == ActionIds.HelpShow) || (State.Kind == OverlayKind.Messages && action == ActionIds.StatusHistory))
        {
            Close();
        }
        else if (State.Kind == OverlayKind.Help && TrySwitchHelpTab(action, count))
        {
            Changed?.Invoke();
        }
        else if (State.Kind is OverlayKind.Help or OverlayKind.Messages or OverlayKind.Output)
        {
            ScrollHelp(action, count);
        }
        else if (State.Palette is { } palette)
        {
            HandlePalette(palette, action);
        }

        return true;
    }

    /// <summary>
    /// Shows the output of a failed command (a failing commit hook) in a scrollable box; <c>Esc</c> closes it. An open overlay is
    /// replaced (the output is the newer fact).
    /// </summary>
    /// <param name="title">The box title.</param>
    /// <param name="heading">The heading of the text section.</param>
    /// <param name="lines">The output, one entry per line.</param>
    public void ShowOutput(string title, string heading, IReadOnlyList<string> lines)
    {
        var returnMode = State.IsOpen ? State.ReturnMode : _keys.Mode;
        var rows = lines.Select(line => new HelpRow(string.Empty, line));
        _helpTabs = [new HelpTab(heading, [new HelpSection(heading, [.. rows])])];
        Open(OverlayState.OpenOutput(title, returnMode), KeyMode.Normal);
    }

    /// <summary>
    /// Asks the user before a destructive action: shows the dialog and switches to Confirm mode, where only <c>y</c> runs
    /// <paramref name="onConfirmed"/>; <c>n</c> and <c>Esc</c> drop it. Returns false (and shows nothing) when the dialog does not fit
    /// the screen: the caller must then not start the action. An open overlay is replaced.
    /// </summary>
    /// <param name="prompt">What the dialog says.</param>
    /// <param name="onConfirmed">Runs after the dialog is closed, on <c>y</c> only.</param>
    public bool ShowConfirm(ConfirmPrompt prompt, Action onConfirmed)
    {
        var size = _screenSize();
        if (ConfirmFrameBuilder.Build(prompt, size.Width, size.Height, _glyphs) is null)
        {
            return false;
        }

        var returnMode = State.IsOpen ? State.ReturnMode : _keys.Mode;
        _onConfirmed = onConfirmed;
        Open(OverlayState.OpenConfirm(prompt, returnMode), KeyMode.Confirm);
        return true;
    }

    /// <summary>
    /// Call when the terminal size changed. A confirm dialog that no longer fits is closed without answering (its callback is
    /// dropped and a warning says nothing happened): a dialog nobody can read must not be answerable with a stray <c>y</c>.
    /// </summary>
    public void OnScreenResized()
    {
        CloseMenuOnResize();
        if (State is not { Kind: OverlayKind.Confirm, Confirm: { } prompt })
        {
            return;
        }

        var size = _screenSize();
        if (ConfirmFrameBuilder.Build(prompt, size.Width, size.Height, _glyphs) is not null)
        {
            return;
        }

        _statuses.Warning("The screen became too small for the question: nothing was done");
        HandleConfirm(ActionIds.ConfirmNo);
    }

    /// <summary>The answer drawn on a frame line of the open confirm dialog (a mouse click); null when the line is none or no dialog is open.</summary>
    public ConfirmAnswer? ConfirmAnswerAt(int frameLine)
    {
        if (State.Kind == OverlayKind.Confirm && CurrentFrame() is { } frame)
        {
            return ConfirmFrameBuilder.AnswerAt(frameLine, frame.Height);
        }

        return null;
    }

    /// <summary>Highlights the palette entry drawn on a line of the overlay frame (a mouse click); false when the line is not an entry.</summary>
    public bool SelectPaletteEntryAt(int frameLine)
    {
        if (State is not { Kind: OverlayKind.Palette, Palette: { } palette })
        {
            return false;
        }

        if (PaletteFrameBuilder.EntryIndexAt(palette, frameLine, _screenSize().Height) is not { } index)
        {
            return false;
        }

        State = State.WithPalette(palette with { SelectedIndex = index });
        Changed?.Invoke();
        return true;
    }

    private bool OpenFor(ActionId action)
    {
        if (action == ActionIds.HelpShow)
        {
            _helpTabs = HelpBuilder.Build(_engine.Table, _baseView(), Level());
            Open(OverlayState.OpenHelp(_baseView(), _keys.Mode), KeyMode.Normal);
            return true;
        }

        if (action == ActionIds.StatusHistory)
        {
            _helpTabs = [new HelpTab("Newest first", [MessageSection()])];
            Open(OverlayState.OpenMessages(_keys.Mode), KeyMode.Normal);
            return true;
        }

        if (action == ActionIds.PaletteOpen)
        {
            var palette = new PaletteState(PaletteEntryBuilder.Build(_engine.Table, _baseView(), Level()));
            Open(OverlayState.OpenPalette(palette, _keys.Mode), KeyMode.Command);
            return true;
        }

        if (action == ActionIds.MenuOpenForRow
            || action == ActionIds.DiffOpenViewOptions
            || action == ActionIds.ListOpenViewOptions)
        {
            OpenContextMenu(action);
            return true;
        }

        return false;
    }

    // The menu hangs on a row of the old layout, so a resize closes it rather than leaving it on the wrong row.
    private void CloseMenuOnResize()
    {
        if (State.Kind == OverlayKind.ContextMenu)
        {
            Close();
        }
    }

    private void OpenContextMenu(ActionId action)
    {
        var view = _baseView();
        if (MenuTarget(view) is not { } target)
        {
            if (action == ActionIds.DiffOpenViewOptions)
            {
                _statuses.Post("No view options for a file's content");
            }
            else if (action == ActionIds.ListOpenViewOptions)
            {
                _statuses.Post("No view options for this tab");
            }

            return;
        }

        var entries = ContextMenuBuilder.Build(
            _engine.Table,
            view,
            target.Actions,
            grouped: target.Title is null,
            Level());
        if (entries.Count == 0)
        {
            _statuses.Post("No actions for this row");
            return;
        }

        var menu = new PaletteState(entries);
        var size = _screenSize();
        if (ContextMenuFrameBuilder.Build(
                menu,
                target.Anchor,
                size.Width,
                size.Height,
                _glyphs,
                target.Title ?? ContextMenuFrameBuilder.DefaultTitle) is null)
        {
            _statuses.Warning("The screen is too small for the actions menu");
            return;
        }

        Open(OverlayState.OpenContextMenu(menu, target.Anchor, _keys.Mode, target.Title), KeyMode.Normal);
    }

    /// <summary>Highlights the menu entry drawn on a frame line (a click); false for any other line.</summary>
    public bool SelectMenuEntryAt(int frameLine)
    {
        if (State is not { Kind: OverlayKind.ContextMenu, Palette: { } menu })
        {
            return false;
        }

        if (ContextMenuFrameBuilder.EntryIndexAt(menu, frameLine, _screenSize().Height) is not { } index)
        {
            return false;
        }

        State = State.WithPalette(menu with { SelectedIndex = index });
        Changed?.Invoke();
        return true;
    }

    // Newest first; the severity takes the keys column.
    private HelpSection MessageSection()
    {
        var rows = _statuses.Log.History
            .Reverse()
            .Select(message => new HelpRow(message.Severity.ToString().ToLowerInvariant(), message.Text))
            .ToList();
        if (rows.Count == 0)
        {
            rows.Add(new HelpRow(string.Empty, "no messages yet"));
        }

        return new HelpSection("Newest first", rows);
    }

    // Only the answers act; every other action is swallowed so nothing behind the dialog moves, and Enter never confirms.
    private void HandleConfirm(ActionId action)
    {
        if (action != ActionIds.ConfirmYes && action != ActionIds.ConfirmNo && action != ActionIds.Cancel)
        {
            return;
        }

        var onConfirmed = action == ActionIds.ConfirmYes ? _onConfirmed : null;
        _onConfirmed = null;
        Close();
        onConfirmed?.Invoke();
    }

    private void Open(OverlayState state, KeyMode mode)
    {
        _whichKeyTimer.Cancel();
        State = state;
        _keys.SetMode(mode);
        Changed?.Invoke();
    }

    private void Close()
    {
        var returnMode = State.ReturnMode;
        State = OverlayState.None;
        _keys.SetMode(returnMode);
        Changed?.Invoke();
    }

    /// <summary>
    /// The help tab under a click on the overlay line <paramref name="frameLine"/> at <paramref name="frameColumn"/>
    /// (both inside the frame), as the number the click action takes (from 1), or null when it is not on a tab label.
    /// </summary>
    public int? HelpTabNumberAt(int frameLine, int frameColumn)
    {
        if (State.Kind == OverlayKind.Help
            && !State.IsHelpFiltered
            && HelpFrameBuilder.TabAt(_helpTabs, State.HelpTab, _screenSize().Width, _screenSize().Height, frameLine, frameColumn)
                is { } index)
        {
            return index + 1;
        }

        return null;
    }

    private IReadOnlyList<WhichKeyRow> WhichKeyRowsFor(KeySequence pending)
    {
        if (!State.WhichKeyVisible)
        {
            return [];
        }

        return WhichKeyBuilder.Build(
            _engine.Continuations(_keys.State, ViewName, HelpBuilder.IsModalScreen(ViewName) ? null : Level()),
            pending,
            OperationInProgress());
    }

    // The pages the help shows now: the filtered rows as one page, else the pages themselves.
    private IReadOnlyList<HelpTab> ShownHelpTabs() =>
        State.IsHelpFiltered ? [HelpBuilder.Filter(_helpTabs, State.HelpFilter)] : _helpTabs;

    // "/" opens the filter line; Enter keeps the filter; Esc clears it (closing the help is the Esc after that).
    private bool TryHandleHelpFilter(ActionId action)
    {
        if (action == ActionIds.HelpSearch)
        {
            State = State.OpenHelpFilter();
            _keys.SetMode(KeyMode.Insert);
            return true;
        }

        if (!State.IsHelpFiltered)
        {
            return false;
        }

        if (action == ActionIds.Cancel)
        {
            State = State.ClearHelpFilter();
            _keys.SetMode(KeyMode.Normal);
            return true;
        }

        if (action == ActionIds.InputAccept && State.IsEditingHelpFilter)
        {
            State = State.AcceptHelpFilter();
            _keys.SetMode(KeyMode.Normal);
            return true;
        }

        // The filtered rows are one page: the section keys do nothing, scrolling works once the filter is kept.
        return action == ActionIds.HelpTabNext || action == ActionIds.HelpTabPrevious || action == ActionIds.HelpTabSelect;
    }

    private bool TrySwitchHelpTab(ActionId action, int? count)
    {
        var tabs = _helpTabs.Count;
        int target;
        if (action == ActionIds.HelpTabNext)
        {
            target = (State.HelpTab + 1) % Math.Max(1, tabs);
        }
        else if (action == ActionIds.HelpTabPrevious)
        {
            target = (State.HelpTab + tabs - 1) % Math.Max(1, tabs);
        }
        else if (action == ActionIds.HelpTabSelect && count is >= 1 && count <= tabs)
        {
            target = count.Value - 1;
        }
        else
        {
            return false;
        }

        if (target != State.HelpTab)
        {
            State = State.ShowHelpTab(target);
        }

        return true;
    }

    private void ScrollHelp(ActionId action, int? count)
    {
        var size = _screenSize();
        var width = size.Width;
        var height = size.Height;
        var shown = ShownHelpTabs();
        var tab = State.IsHelpFiltered ? 0 : State.HelpTab;
        var maximum = HelpFrameBuilder.MaximumScroll(shown, tab, width, height);
        var visible = HelpFrameBuilder.VisibleRows(shown, tab, width, height);
        if (ListMotion.TryMove(action, count, State.HelpScroll, maximum + 1, visible, out var target))
        {
            State = State.ScrollHelpTo(target);
            Changed?.Invoke();
        }
    }

    private void HandlePalette(PaletteState palette, ActionId action)
    {
        if (action == ActionIds.PaletteNext)
        {
            State = State.WithPalette(palette.Move(1));
            Changed?.Invoke();
        }
        else if (action == ActionIds.PalettePrevious)
        {
            State = State.WithPalette(palette.Move(-1));
            Changed?.Invoke();
        }
        else if (action == ActionIds.CommandExecute)
        {
            var chosen = ExactActionOf(palette.Query) ?? palette.Selected?.Action;
            Close();
            if (chosen is { } command)
            {
                _run(command, null);
            }
        }
    }

    // A command hidden by the level still runs when its exact id is typed (#839): hidden, never disabled.
    private ActionId? ExactActionOf(string query)
    {
        var identifier = query.Trim();
        if (identifier.Length == 0 || ActionCatalog.IsModalOnly(new ActionId(identifier)))
        {
            return null;
        }

        var bound = _engine.Table.Bindings.FirstOrDefault(binding => binding.Action.Value == identifier);
        if (bound is not null)
        {
            return bound.Action;
        }

        return ActionCatalog.PaletteOnly.Contains(new ActionId(identifier)) ? new ActionId(identifier) : null;
    }

    private void OnStatusChanged()
    {
        if (State.Kind != OverlayKind.Messages)
        {
            return;
        }

        _helpTabs = [new HelpTab("Newest first", [MessageSection()])];
        Changed?.Invoke();
    }

    private void OnTextKey(KeyEvent key)
    {
        if (State is { Kind: OverlayKind.Help, IsEditingHelpFilter: true })
        {
            if (key.Name == KeyNames.Backspace)
            {
                State = State.DeleteHelpFilterLast();
            }
            else if (key.IsPrintable)
            {
                State = State.TypeHelpFilter(key.Name);
            }
            else
            {
                return;
            }

            Changed?.Invoke();
            return;
        }

        if (State is not { Kind: OverlayKind.Palette, Palette: { } palette })
        {
            return;
        }

        if (key.Name == KeyNames.Backspace)
        {
            State = State.WithPalette(palette.DeleteLast());
        }
        else if (key.IsPrintable)
        {
            State = State.WithPalette(palette.Type(key.Name));
        }
        else
        {
            return;
        }

        Changed?.Invoke();
    }

    private void OnPendingChanged()
    {
        if (State.IsOpen)
        {
            return;
        }

        if (!_keys.State.HasPending)
        {
            _whichKeyTimer.Cancel();
            if (State.WhichKeyVisible)
            {
                State = State.WithWhichKey(false);
                Changed?.Invoke();
            }

            return;
        }

        if (State.WhichKeyVisible)
        {
            Changed?.Invoke();
            return;
        }

        if (!WhichKeyEnabled)
        {
            return;
        }

        _whichKeyTimer.Start(ShowWhichKey);
    }

    private void ShowWhichKey()
    {
        if (State.IsOpen || !_keys.State.HasPending)
        {
            return;
        }

        State = State.WithWhichKey(true);
        Changed?.Invoke();
    }
}
