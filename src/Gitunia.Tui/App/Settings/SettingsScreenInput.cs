using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Shell;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;

namespace Gitunia.Tui.App.Settings;

/// <summary>
/// The view side of the settings screen (ux-settings.md, "Settings screen"): draws the screen that lives in the shell
/// state and turns keys, typed text, clicks and resizes into <see cref="SettingsCommand"/>s. It owns no state;
/// while the screen is open it consumes every action, so nothing behind it moves.
/// </summary>
/// <param name="host">The dispatcher that holds the state.</param>
/// <param name="glyphs">The characters the overlay is drawn with.</param>
public sealed class SettingsScreenInput(Dispatcher host, GraphGlyphSet glyphs) : IActionHandler, ITextKeyHandler
{
    private SettingsSession Session => host.State.Shell.Settings;

    private ScreenExtent Screen => host.State.Shell.Screen ?? new ScreenExtent(0, 0);

    public bool IsOpen => Session.IsOpen;

    /// <summary>The frame to draw, or null when the screen is closed or too small for even a title.</summary>
    public OverlayFrame? CurrentFrame()
    {
        var screen = Screen;

        return Session.Screen is { } state
            ? SettingsFrameBuilder.Build(state, screen.Width, screen.Height, glyphs)
            : null;
    }

    /// <summary>
    /// Handles an action: opens the screen on <see cref="ActionIds.SettingsOpen"/>, toggles the command level while it
    /// is closed and, while it is open, consumes every action. Returns true when the action was consumed.
    /// </summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    public bool TryExecute(ActionId action, int? count)
    {
        if (!SettingsScreenReducer.Consumes(action, Session.IsOpen))
        {
            return false;
        }

        host.Dispatch(new SettingsActionPressed(action, count));

        return true;
    }

    /// <summary>Handles a key typed into the number box, the folder box or the filter; false when none is.</summary>
    /// <param name="key">The key.</param>
    public bool HandleTextKey(KeyEvent key)
    {
        if (!Session.TakesText)
        {
            return false;
        }

        host.Dispatch(new SettingsKeyTyped(key));

        return true;
    }

    /// <summary>
    /// A click on the overlay. Selects what was hit and returns the mouse view whose action finishes the job (the edit
    /// of a row, the add or remove marker, an answer to the removal question), or null when a click only selects.
    /// </summary>
    /// <param name="line">The frame line (0 is the title).</param>
    /// <param name="column">The frame column (0 is the left border).</param>
    /// <param name="handled">True when the settings screen took the click.</param>
    public string? OnLineClicked(int line, int column, out bool handled)
    {
        handled = Session.IsOpen;
        if (Session.Screen is not { } state)
        {
            return null;
        }

        var screen = Screen;
        var hit = SettingsFrameBuilder.HitAt(state, column, line, screen.Width, screen.Height, glyphs);
        if (hit.Kind is SettingsHitKind.ConfirmYes or SettingsHitKind.ConfirmNo)
        {
            return hit.Kind == SettingsHitKind.ConfirmYes
                ? DefaultMouseMap.ConfirmYesButton
                : DefaultMouseMap.ConfirmNoButton;
        }

        if (state.IsTyping || state.Removal is not null)
        {
            return null;
        }

        if (hit.Kind == SettingsHitKind.Category)
        {
            host.Dispatch(new SettingsCategoryClicked(hit.Index));

            return null;
        }

        return ClickRow(state, hit);
    }

    /// <summary>Gives the cursor to the column under the pointer, so the wheel scrolls that column.</summary>
    /// <param name="column">The frame column of the pointer (0 is the left border).</param>
    public void FocusColumnAt(int column)
    {
        var screen = Screen;
        if (SettingsLayout.For(screen.Width, screen.Height) is not { IsTooSmall: false } layout)
        {
            return;
        }

        var target = column - 1 < layout.CategoryWidth ? SettingsColumn.Categories : SettingsColumn.Settings;
        host.Dispatch(new SettingsColumnFocused(target));
    }

    /// <summary>Call when the terminal size changed: the layout may collapse or expand and the pages change
    /// size.</summary>
    public void OnScreenResized()
    {
        var screen = Screen;
        var layout = SettingsLayout.For(screen.Width, screen.Height);
        var rows = Math.Max(1, SettingsFrameBuilder.VisibleRows(screen.Width, screen.Height));

        host.Dispatch(new SettingsViewportChanged(new SettingsViewport(rows, layout is { IsCollapsed: true })));
    }

    private string? ClickRow(SettingsState state, SettingsHit hit)
    {
        if (hit.Kind is SettingsHitKind.None || hit.Index >= state.Rows.Count)
        {
            return null;
        }

        var row = state.Rows[hit.Index];
        var isSecondClick = state.Column == SettingsColumn.Settings && state.Cursor == hit.Index;
        host.Dispatch(new SettingsRowClicked(hit.Index));
        if (hit.Kind == SettingsHitKind.Add)
        {
            return DefaultMouseMap.SettingsAddButton;
        }

        if (hit.Kind == SettingsHitKind.Remove)
        {
            return DefaultMouseMap.SettingsRemoveButton;
        }

        // A theme option is previewed by the first click and kept by the next on it; others are kept at once.
        var previewsOnly = row.Kind == SettingRowKind.Option && !isSecondClick
            && row.Definition.Id == SettingsRegistry.ThemeId;
        var edits = hit.Kind == SettingsHitKind.Control || (row.Kind == SettingRowKind.Option && !previewsOnly)
            || isSecondClick;

        return edits ? DefaultMouseMap.SettingsEditButton : null;
    }
}
