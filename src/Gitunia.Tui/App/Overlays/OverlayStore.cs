using Gitunia.App;
using Gitunia.App.Shell;
using Gitunia.App.Status;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Holds the <see cref="OverlayState"/> of the window (help, messages, output, palette, menu, confirm dialog) and keeps it
/// in step with the shell state: the confirm dialog and the output box are shell state and follow it at once, so a
/// callback of an answered dialog already sees the dialog closed. The controller changes it, the screen draws it.
/// </summary>
public sealed class OverlayStore
{
    private readonly Dispatcher _host;
    private readonly StatusReporter _statuses;
    private readonly WhichKeyTimer _whichKeyTimer;
    private readonly IFocusedView _baseView;
    private OverlayState _state = OverlayState.None;
    private bool _shellChanged;
    private ConfirmDialog? _shownConfirm;
    private OutputOverlay? _shownOutput;
    private StatusLog _seenStatus;

    /// <param name="host">Holds the key mode, the confirm dialog and the output box this store follows.</param>
    /// <param name="statuses">The status messages the history overlay lists.</param>
    /// <param name="whichKeyTimer">Delays the which-key popup; an overlay that opens cancels it.</param>
    /// <param name="baseView">The keymap view that is focused when no overlay is open (it follows the focused pane).</param>
    public OverlayStore(
        Dispatcher host,
        StatusReporter statuses,
        WhichKeyTimer whichKeyTimer,
        IFocusedView baseView)
    {
        _host = host;
        _statuses = statuses;
        _whichKeyTimer = whichKeyTimer;
        _baseView = baseView;
        _seenStatus = host.State.Shell.Status;
        host.Changed += OnHostChanged;
    }

    /// <summary>Raised when what the overlay layer shows may have changed.</summary>
    public event Action? Changed;

    /// <summary>The pages the help, the messages list and the output box show.</summary>
    public IReadOnlyList<HelpTab> HelpTabs { get; set; } = [];

    /// <summary>The overlay state, with the shell's confirm dialog and output box already in it.</summary>
    public OverlayState State
    {
        get
        {
            if (SyncShell())
            {
                _shellChanged = true;
            }

            return _state;
        }

        set
        {
            _state = value;
            TellShell();
        }
    }

    /// <summary>The keymap view that is focused when no overlay is open.</summary>
    public string BaseView => _baseView.Name;

    /// <summary>The keymap view that is focused: the overlay's own while one is open, else the base view.</summary>
    public string ViewName => State.Kind switch
    {
        OverlayKind.Help => DefaultKeymap.HelpView,
        OverlayKind.Messages or OverlayKind.Output => DefaultKeymap.MessagesView,
        OverlayKind.Palette => DefaultKeymap.PaletteView,
        OverlayKind.ContextMenu => DefaultKeymap.ContextMenuView,
        OverlayKind.Confirm => DefaultKeymap.ConfirmView,
        _ => _baseView.Name,
    };

    /// <summary>Tells the subscribers the overlay layer changed.</summary>
    public void RaiseChanged() => Changed?.Invoke();

    /// <summary>Stops a which-key popup that is about to show.</summary>
    public void CancelWhichKey() => _whichKeyTimer.Cancel();

    /// <summary>Starts the wait for the which-key popup.</summary>
    /// <param name="elapsed">Runs on the UI thread when the wait is over.</param>
    public void StartWhichKey(Action elapsed) => _whichKeyTimer.Start(elapsed);

    /// <summary>Opens an overlay and switches the key mode.</summary>
    /// <param name="state">The overlay.</param>
    /// <param name="mode">The key mode while it is open.</param>
    public void Open(OverlayState state, KeyMode mode)
    {
        _whichKeyTimer.Cancel();
        State = state;
        _host.SetKeyMode(mode);
        Changed?.Invoke();
    }

    /// <summary>Closes the open overlay and restores the key mode it was opened from.</summary>
    public void Close()
    {
        var wasOutput = State.Kind == OverlayKind.Output;
        var returnMode = State.ReturnMode;
        State = OverlayState.None;
        if (wasOutput)
        {
            _host.Dispatch(new OutputClosedCommand());
        }
        else
        {
            _host.SetKeyMode(returnMode);
        }

        Changed?.Invoke();
    }

    /// <summary>The messages list: newest first; the severity takes the keys column.</summary>
    public HelpSection MessageSection()
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

    // The state learns which overlay of this layer is open (#1020); confirm and output are shell state already.
    private void TellShell()
    {
        var kind = _state.Kind is OverlayKind.Confirm or OverlayKind.Output ? OverlayKind.None : _state.Kind;
        if (_host.State.Shell.Overlay != kind)
        {
            _host.Dispatch(new OverlayChangedCommand(kind));
        }
    }

    // The confirm dialog and the output box are shell state: this store follows them and keeps the scroll of the box.
    // A dialog or box replaces an overlay that is open (it is the newer fact).
    private void OnHostChanged()
    {
        var changed = SyncShell() || _shellChanged;
        _shellChanged = false;
        var status = _host.State.Shell.Status;
        if (!ReferenceEquals(status, _seenStatus))
        {
            _seenStatus = status;
            changed |= RefreshMessages();
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private bool SyncShell()
    {
        var shell = _host.State.Shell;
        var changed = false;
        if (!ReferenceEquals(shell.Confirm, _shownConfirm))
        {
            changed |= ShowConfirm(shell.Confirm);
        }

        if (!ReferenceEquals(shell.Output, _shownOutput))
        {
            changed |= ShowOutput(shell.Output);
        }

        return changed;
    }

    private bool ShowConfirm(ConfirmDialog? dialog)
    {
        _shownConfirm = dialog;
        if (dialog is not null)
        {
            _whichKeyTimer.Cancel();
            _state = OverlayState.OpenConfirm(dialog.Prompt, dialog.ReturnMode);

            return true;
        }

        if (_state.Kind != OverlayKind.Confirm)
        {
            return false;
        }

        _state = OverlayState.None;

        return true;
    }

    private bool ShowOutput(OutputOverlay? output)
    {
        _shownOutput = output;
        if (output is not null)
        {
            var rows = output.Lines.Select(line => new HelpRow(string.Empty, line));
            HelpTabs = [new HelpTab(output.Heading, [new HelpSection(output.Heading, [.. rows])])];
            _whichKeyTimer.Cancel();
            _state = OverlayState.OpenOutput(output.Title, output.ReturnMode);

            return true;
        }

        if (_state.Kind != OverlayKind.Output)
        {
            return false;
        }

        _state = OverlayState.None;

        return true;
    }

    private bool RefreshMessages()
    {
        if (_state.Kind != OverlayKind.Messages)
        {
            return false;
        }

        HelpTabs = [new HelpTab("Newest first", [MessageSection()])];

        return true;
    }
}
