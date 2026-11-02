using Gitunia.Tui.Views;
using Gitunia.App;
using Gitunia.App.Shell;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Screen;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Connects the <see cref="OverlayState"/> of the window (held by the <see cref="OverlayStore"/>) to the keys: opens the
/// help and the palette, consumes the actions and typed text while one is open, and shows the which-key popup for a chord
/// that stays pending. The state transitions themselves are the pure methods of <see cref="OverlayState"/> and
/// <see cref="PaletteState"/>.
/// </summary>
public sealed class OverlayController : IActionHandler
{
    /// <summary>How long a chord stays pending before the which-key popup appears.</summary>
    public static readonly TimeSpan WhichKeyDelay = TimeSpan.FromMilliseconds(250);

    private readonly OverlayStore _store;
    private readonly KeyController _keys;
    private readonly IActionRoute _route;
    private readonly Dispatcher _host;
    private readonly OverlayServices _services;

    /// <param name="store">The overlay state this controller changes.</param>
    /// <param name="keys">The key controller whose mode this controller switches.</param>
    /// <param name="route">Runs an action chosen in the palette.</param>
    /// <param name="host">Holds the key mode, the confirm dialog and the output box.</param>
    /// <param name="services">The keymap engine, the screen size, the settings and the menu targets.</param>
    public OverlayController(
        OverlayStore store,
        KeyController keys,
        IActionRoute route,
        Dispatcher host,
        OverlayServices services)
    {
        _store = store;
        _keys = keys;
        _route = route;
        _host = host;
        _services = services;
        keys.PendingChanged += OnPendingChanged;
        keys.TextKeyPressed += OnTextKey;
    }

    private OverlayState State
    {
        get => _store.State;
        set => _store.State = value;
    }

    private KeymapEngine Engine => _services.Engine;

    private ScreenSize ScreenSize => _services.Chrome.ScreenSize;

    private CommandLevel Level => _services.Config.Options.CommandLevel;

    private string BaseView => _store.BaseView;

    /// <summary>The frame to draw over the screen, or null when nothing is showing.</summary>
    public OverlayFrame? CurrentFrame()
    {
        var size = ScreenSize;
        var width = size.Width;
        var height = size.Height;
        var pending = _keys.State.PendingSequence;
        var rows = WhichKeyRowsFor(pending);
        var bannerShown = OperationInProgress() && height >= ScreenLayout.MinimumHeightWithBanner;

        return OverlayFrameFactory.Create(
            State,
            _store.HelpTabs,
            _keys.PendingText,
            rows,
            width,
            height,
            _services.Config.Glyphs,
            bannerShown);
    }

    bool IActionHandler.TryExecute(ActionId action, int? count) => Handle(action, count);

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
            _store.RaiseChanged();
        }
        else if (IsCloseAction(action))
        {
            _store.Close();
        }
        else if (State.Kind == OverlayKind.Help && TrySwitchHelpTab(action, count))
        {
            _store.RaiseChanged();
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
    /// Call when the terminal size changed. The menu hangs on a row of the old layout, so it closes. A confirm dialog
    /// that no longer fits is closed by the shell reducer (<see cref="ScreenResizedCommand"/>).
    /// </summary>
    public void OnScreenResized()
    {
        if (State.Kind == OverlayKind.ContextMenu)
        {
            _store.Close();
        }
    }

    /// <summary>
    /// Takes the which-key setting of the options (<c>hints.whichKey</c>, #837). Off hides only the popup: the status bar
    /// still shows the pending prefix and every key works. Turning it off drops a wait or popup in progress.
    /// </summary>
    public void OnOptionsChanged()
    {
        if (_services.Config.Options.WhichKeyPopup)
        {
            return;
        }

        _store.CancelWhichKey();
        if (State.WhichKeyVisible)
        {
            State = State.WithWhichKey(false);
            _store.RaiseChanged();
        }
    }

    private bool IsCloseAction(ActionId action) =>
        action == ActionIds.Cancel
        || action == ActionIds.NavigateBack
        || action == ActionIds.AppQuit
        || (State.Kind == OverlayKind.Help && action == ActionIds.HelpShow)
        || (State.Kind == OverlayKind.Messages && action == ActionIds.StatusHistory);

    private bool OperationInProgress() => _host.Workspace.ActiveTab?.Operation is not null;

    private bool OpenFor(ActionId action)
    {
        if (action == ActionIds.HelpShow)
        {
            _store.HelpTabs = HelpBuilder.Build(Engine.Table, BaseView, Level);
            _store.Open(OverlayState.OpenHelp(BaseView, _keys.Mode), KeyMode.Normal);
            return true;
        }

        if (action == ActionIds.StatusHistory)
        {
            _store.HelpTabs = [new HelpTab("Newest first", [_store.MessageSection()])];
            _store.Open(OverlayState.OpenMessages(_keys.Mode), KeyMode.Normal);
            return true;
        }

        if (action == ActionIds.PaletteOpen)
        {
            var palette = new PaletteState(PaletteEntryBuilder.Build(Engine.Table, BaseView, Level));
            _store.Open(OverlayState.OpenPalette(palette, _keys.Mode), KeyMode.Command);
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

    private void OpenContextMenu(ActionId action)
    {
        var view = BaseView;
        if (_services.Menus.TargetOf(view, action) is not { } target)
        {
            if (action == ActionIds.DiffOpenViewOptions)
            {
                _services.Statuses.Post("No view options for a file's content");
            }
            else if (action == ActionIds.ListOpenViewOptions)
            {
                _services.Statuses.Post("No view options for this tab");
            }

            return;
        }

        var entries = ContextMenuBuilder.Build(
            Engine.Table,
            view,
            target.Actions,
            grouped: target.Title is null,
            Level);
        if (entries.Count == 0)
        {
            _services.Statuses.Post("No actions for this row");
            return;
        }

        var menu = new PaletteState(entries);
        var size = ScreenSize;
        if (ContextMenuFrameBuilder.Build(
                menu,
                target.Anchor,
                size.Width,
                size.Height,
                _services.Config.Glyphs,
                target.Title ?? ContextMenuFrameBuilder.DefaultTitle) is null)
        {
            _services.Statuses.Warning("The screen is too small for the actions menu");
            return;
        }

        _store.Open(OverlayState.OpenContextMenu(menu, target.Anchor, _keys.Mode, target.Title), KeyMode.Normal);
    }

    // Only the answers act; every other action is swallowed so nothing behind the dialog moves, and Enter never confirms.
    private void HandleConfirm(ActionId action)
    {
        if (action == ActionIds.ConfirmYes)
        {
            _host.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        }
        else if (action == ActionIds.ConfirmNo || action == ActionIds.Cancel)
        {
            _host.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.No));
        }
    }

    private IReadOnlyList<WhichKeyRow> WhichKeyRowsFor(KeySequence pending)
    {
        if (!State.WhichKeyVisible)
        {
            return [];
        }

        return WhichKeyBuilder.Build(
            Engine.Continuations(_keys.State, _store.ViewName, HelpBuilder.IsModalScreen(_store.ViewName) ? null : Level),
            pending,
            OperationInProgress());
    }

    // The pages the help shows now: the filtered rows as one page, else the pages themselves.
    private IReadOnlyList<HelpTab> ShownHelpTabs() =>
        State.IsHelpFiltered ? [HelpBuilder.Filter(_store.HelpTabs, State.HelpFilter)] : _store.HelpTabs;

    // "/" opens the filter line; Enter keeps the filter; Esc clears it (closing the help is the Esc after that).
    private bool TryHandleHelpFilter(ActionId action)
    {
        if (action == ActionIds.HelpSearch)
        {
            State = State.OpenHelpFilter();
            _host.SetKeyMode(KeyMode.Insert);
            return true;
        }

        if (!State.IsHelpFiltered)
        {
            return false;
        }

        if (action == ActionIds.Cancel)
        {
            State = State.ClearHelpFilter();
            _host.SetKeyMode(KeyMode.Normal);
            return true;
        }

        if (action == ActionIds.InputAccept && State.IsEditingHelpFilter)
        {
            State = State.AcceptHelpFilter();
            _host.SetKeyMode(KeyMode.Normal);
            return true;
        }

        // The filtered rows are one page: the section keys do nothing, scrolling works once the filter is kept.
        return action == ActionIds.HelpTabNext || action == ActionIds.HelpTabPrevious || action == ActionIds.HelpTabSelect;
    }

    private bool TrySwitchHelpTab(ActionId action, int? count)
    {
        var tabs = _store.HelpTabs.Count;
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
        var size = ScreenSize;
        var width = size.Width;
        var height = size.Height;
        var shown = ShownHelpTabs();
        var tab = State.IsHelpFiltered ? 0 : State.HelpTab;
        var maximum = HelpFrameBuilder.MaximumScroll(shown, tab, width, height);
        var visible = HelpFrameBuilder.VisibleRows(shown, tab, width, height);
        if (ListMotion.TryMove(action, count, State.HelpScroll, maximum + 1, visible, out var target))
        {
            State = State.ScrollHelpTo(target);
            _store.RaiseChanged();
        }
    }

    private void HandlePalette(PaletteState palette, ActionId action)
    {
        if (action == ActionIds.PaletteNext)
        {
            State = State.WithPalette(palette.Move(1));
            _store.RaiseChanged();
        }
        else if (action == ActionIds.PalettePrevious)
        {
            State = State.WithPalette(palette.Move(-1));
            _store.RaiseChanged();
        }
        else if (action == ActionIds.CommandExecute)
        {
            var chosen = ExactActionOf(palette.Query) ?? palette.Selected?.Action;
            _store.Close();
            if (chosen is { } command)
            {
                _route.Route(command, null);
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

        var bound = Engine.Table.Bindings.FirstOrDefault(binding => binding.Action.Value == identifier);
        if (bound is not null)
        {
            return bound.Action;
        }

        return ActionCatalog.PaletteOnly.Contains(new ActionId(identifier)) ? new ActionId(identifier) : null;
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

            _store.RaiseChanged();
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

        _store.RaiseChanged();
    }

    private void OnPendingChanged()
    {
        if (State.IsOpen)
        {
            return;
        }

        if (!_keys.State.HasPending)
        {
            _store.CancelWhichKey();
            if (State.WhichKeyVisible)
            {
                State = State.WithWhichKey(false);
                _store.RaiseChanged();
            }

            return;
        }

        if (State.WhichKeyVisible)
        {
            _store.RaiseChanged();
            return;
        }

        if (!_services.Config.Options.WhichKeyPopup)
        {
            return;
        }

        _store.StartWhichKey(ShowWhichKey);
    }

    private void ShowWhichKey()
    {
        if (State.IsOpen || !_keys.State.HasPending)
        {
            return;
        }

        State = State.WithWhichKey(true);
        _store.RaiseChanged();
    }
}
