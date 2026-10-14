using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;

namespace Gitunia.Tui.App;

/// <summary>
/// Owns the <see cref="OverlayState"/> of the window and connects it to the keys: opens the help and the palette,
/// consumes the actions and typed text while one is open, and shows the which-key popup for a chord that stays pending.
/// The state transitions themselves are the pure methods of <see cref="OverlayState"/> and <see cref="PaletteState"/>.
/// </summary>
public sealed class OverlayController
{
    /// <summary>How long a chord stays pending before the which-key popup appears.</summary>
    public static readonly TimeSpan WhichKeyDelay = TimeSpan.FromMilliseconds(500);

    private readonly KeymapEngine engine;
    private readonly KeyController keys;
    private readonly WhichKeyTimer whichKeyTimer;
    private readonly Action<ActionId, int?> run;
    private readonly Func<(int Width, int Height)> screenSize;
    private readonly GraphGlyphSet glyphs;
    private readonly Func<string> baseView;
    private readonly StatusChannel statuses;
    private IReadOnlyList<HelpSection> helpSections = [];

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
        Func<(int Width, int Height)> screenSize,
        GraphGlyphSet glyphs,
        Func<string> baseView,
        StatusChannel statuses)
    {
        this.engine = engine;
        this.keys = keys;
        this.whichKeyTimer = whichKeyTimer;
        this.run = run;
        this.screenSize = screenSize;
        this.glyphs = glyphs;
        this.baseView = baseView;
        this.statuses = statuses;
        keys.PendingChanged += OnPendingChanged;
        keys.TextKeyPressed += OnTextKey;
        statuses.Changed += OnStatusChanged;
    }

    /// <summary>Raised when what the overlay layer shows may have changed.</summary>
    public event Action? Changed;

    public OverlayState State { get; private set; } = OverlayState.None;

    /// <summary>The keymap view that is focused: the overlay's own while one is open, else the base view.</summary>
    public string ViewName => State.Kind switch
    {
        OverlayKind.Help => DefaultKeymap.HelpView,
        OverlayKind.Messages => DefaultKeymap.MessagesView,
        OverlayKind.Palette => DefaultKeymap.PaletteView,
        _ => baseView(),
    };

    /// <summary>The frame to draw over the screen, or null when nothing is showing.</summary>
    public OverlayFrame? CurrentFrame()
    {
        var (width, height) = screenSize();
        var rows = State.WhichKeyVisible ? WhichKeyBuilder.Build(engine.Continuations(keys.State, ViewName)) : [];
        return OverlayFrameFactory.Create(State, helpSections, keys.PendingText, rows, width, height, glyphs);
    }

    /// <summary>Handles an action while an overlay may be open; returns true when it was consumed.</summary>
    public bool Handle(ActionId action, int? count)
    {
        if (!State.IsOpen)
        {
            return OpenFor(action);
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || (State.Kind == OverlayKind.Help && action == ActionIds.HelpShow) || (State.Kind == OverlayKind.Messages && action == ActionIds.StatusHistory))
        {
            Close();
        }
        else if (State.Kind is OverlayKind.Help or OverlayKind.Messages)
        {
            ScrollHelp(action, count);
        }
        else if (State.Palette is { } palette)
        {
            HandlePalette(palette, action);
        }

        return true;
    }

    /// <summary>Highlights the palette entry drawn on a line of the overlay frame (a mouse click); false when the line is not an entry.</summary>
    public bool SelectPaletteEntryAt(int frameLine)
    {
        if (State is not { Kind: OverlayKind.Palette, Palette: { } palette })
        {
            return false;
        }

        if (PaletteFrameBuilder.EntryIndexAt(palette, frameLine, screenSize().Height) is not { } index)
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
            helpSections = HelpBuilder.Build(engine.Table, baseView());
            Open(OverlayState.OpenHelp(baseView(), keys.Mode), KeyMode.Normal);
            return true;
        }

        if (action == ActionIds.StatusHistory)
        {
            helpSections = [MessageSection()];
            Open(OverlayState.OpenMessages(keys.Mode), KeyMode.Normal);
            return true;
        }

        if (action == ActionIds.PaletteOpen)
        {
            var palette = new PaletteState(PaletteEntryBuilder.Build(engine.Table, baseView()));
            Open(OverlayState.OpenPalette(palette, keys.Mode), KeyMode.Command);
            return true;
        }

        return false;
    }

    // Newest first; the severity takes the keys column.
    private HelpSection MessageSection()
    {
        var rows = statuses.Log.History
            .Reverse()
            .Select(message => new HelpRow(message.Severity.ToString().ToLowerInvariant(), message.Text))
            .ToList();
        if (rows.Count == 0)
        {
            rows.Add(new HelpRow(string.Empty, "no messages yet"));
        }

        return new HelpSection("Newest first", rows);
    }

    private void Open(OverlayState state, KeyMode mode)
    {
        whichKeyTimer.Cancel();
        State = state;
        keys.SetMode(mode);
        Changed?.Invoke();
    }

    private void Close()
    {
        var returnMode = State.ReturnMode;
        State = OverlayState.None;
        keys.SetMode(returnMode);
        Changed?.Invoke();
    }

    private void ScrollHelp(ActionId action, int? count)
    {
        var (width, height) = screenSize();
        var maximum = HelpFrameBuilder.MaximumScroll(helpSections, width, height);
        var visible = HelpFrameBuilder.VisibleRows(helpSections, width, height);
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
            var chosen = palette.Selected;
            Close();
            if (chosen is not null)
            {
                run(chosen.Action, null);
            }
        }
    }

    private void OnStatusChanged()
    {
        if (State.Kind != OverlayKind.Messages)
        {
            return;
        }

        helpSections = [MessageSection()];
        Changed?.Invoke();
    }

    private void OnTextKey(KeyEvent key)
    {
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

        if (!keys.State.HasPending)
        {
            whichKeyTimer.Cancel();
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

        whichKeyTimer.Start(ShowWhichKey);
    }

    private void ShowWhichKey()
    {
        if (State.IsOpen || !keys.State.HasPending)
        {
            return;
        }

        State = State.WithWhichKey(true);
        Changed?.Invoke();
    }
}
