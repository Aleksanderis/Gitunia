using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Mouse;
using Gitunia.Core.Search;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Status;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Settings;

/// <summary>
/// The settings screen (ux.md, "Settings screen"): a modal overlay over every tab that edits <c>config.json</c> through
/// the config writer and applies each change to the running app at once. It owns the keys while it is open. State
/// changes are the pure <see cref="SettingsReducer"/>; this class reads and writes the file off the UI thread, carries
/// out the effects and reports problems in the status bar.
/// </summary>
/// <param name="writer">Reads and edits the config file.</param>
/// <param name="environment">The home folder and the folder check the reducer needs.</param>
/// <param name="statuses">Where saves, refusals and failures are reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="screenSize">The current terminal size.</param>
/// <param name="glyphs">The characters the overlay is drawn with.</param>
/// <param name="setMode">Switches the keymap mode (Insert while typing, Confirm for the removal question).</param>
/// <param name="redraw">Redraws the overlay layer and the status bar.</param>
/// <param name="applyOptions">Applies the options of the file to the running app.</param>
/// <param name="launchEditor">Opens a file in the user's editor and calls back when it closes; returns why it cannot.</param>
/// <param name="definitions">The settings on offer; the registry unless a test brings its own.</param>
/// <param name="cancellationToken">Ends the file work when the app is closing.</param>
public sealed class SettingsController(
    ConfigWriter writer,
    SettingsEnvironment environment,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<ScreenSize> screenSize,
    GraphGlyphSet glyphs,
    Action<KeyMode> setMode,
    Action redraw,
    Action<ConfigOptions> applyOptions,
    Func<string, Action, string> launchEditor,
    IReadOnlyList<SettingDefinition>? definitions = null,
    CancellationToken cancellationToken = default)
{
    private static readonly Dictionary<ActionId, SettingsCommandKind> Commands = new()
    {
        [ActionIds.Cancel] = SettingsCommandKind.Back,
        [ActionIds.NavigateBack] = SettingsCommandKind.Back,
        [ActionIds.AppQuit] = SettingsCommandKind.Close,
        [ActionIds.SettingsOpen] = SettingsCommandKind.Close,
        [ActionIds.SettingsActivate] = SettingsCommandKind.Activate,
        [ActionIds.SettingsToggle] = SettingsCommandKind.Toggle,
        [ActionIds.SettingsAdd] = SettingsCommandKind.Add,
        [ActionIds.SettingsRemove] = SettingsCommandKind.Remove,
        [ActionIds.SettingsMoveDown] = SettingsCommandKind.ItemDown,
        [ActionIds.SettingsMoveUp] = SettingsCommandKind.ItemUp,
        [ActionIds.SettingsIncrease] = SettingsCommandKind.Increase,
        [ActionIds.SettingsDecrease] = SettingsCommandKind.Decrease,
        [ActionIds.SettingsFilter] = SettingsCommandKind.FilterOpen,
        [ActionIds.InputAccept] = SettingsCommandKind.InputAccept,
        [ActionIds.ConfirmYes] = SettingsCommandKind.ConfirmYes,
        [ActionIds.ConfirmNo] = SettingsCommandKind.ConfirmNo,
    };

    private readonly IReadOnlyList<SettingDefinition> _definitions = definitions ?? SettingsRegistry.All;
    private ConfigSnapshot _snapshot = new(false, string.Empty, string.Empty);
    private KeyMode _mode = KeyMode.Normal;
    private bool _isBusy;

    /// <summary>The open screen, null while it is closed.</summary>
    public SettingsState? State { get; private set; }

    public bool IsOpen => State is not null;

    /// <summary>The frame to draw, or null when the screen is closed or too small for even a title.</summary>
    public OverlayFrame? CurrentFrame()
    {
        var size = screenSize();

        return State is { } state ? SettingsFrameBuilder.Build(state, size.Width, size.Height, glyphs) : null;
    }

    /// <summary>
    /// Handles an action: opens the screen on <see cref="ActionIds.SettingsOpen"/> and, while it is open, consumes
    /// every action (nothing behind it may move). Returns true when the action was consumed.
    /// </summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    public bool TryExecute(ActionId action, int? count)
    {
        if (State is null)
        {
            if (action != ActionIds.SettingsOpen)
            {
                return false;
            }

            Open();

            return true;
        }

        if (Commands.TryGetValue(action, out var command))
        {
            Run(command);
        }
        else
        {
            Move(action, count);
        }

        return true;
    }

    /// <summary>Handles a key typed into the number box, the folder box or the filter; false when none is.</summary>
    /// <param name="key">The key.</param>
    public bool HandleTextKey(KeyEvent key)
    {
        if (State is not { IsTyping: true } state)
        {
            return false;
        }

        if (state.Input is { } input)
        {
            if (PromptTextEditor.Apply(input.Text, key) is { } text && text != input.Text)
            {
                State = state.WithInputText(text);
                redraw();
            }
        }
        else if (PromptTextEditor.Apply(state.Filter, key) is { } filter && filter != state.Filter)
        {
            State = state.WithFilterText(filter);
            redraw();
        }

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
        handled = State is not null;
        if (State is not { } state)
        {
            return null;
        }

        var size = screenSize();
        var hit = SettingsFrameBuilder.HitAt(state, column, line, size.Width, size.Height, glyphs);
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

        return hit.Kind == SettingsHitKind.Category ? SelectCategory(state, hit.Index) : ClickRow(state, hit);
    }

    /// <summary>Gives the cursor to the column under the pointer, so the wheel scrolls that column.</summary>
    /// <param name="column">The frame column of the pointer (0 is the left border).</param>
    public void FocusColumnAt(int column)
    {
        if (State is not { IsCollapsed: false, IsTyping: false, Removal: null, Choice: null } state
            || state.IsFiltering)
        {
            return;
        }

        var size = screenSize();
        if (SettingsLayout.For(size.Width, size.Height) is not { IsTooSmall: false } layout)
        {
            return;
        }

        var target = column - 1 < layout.CategoryWidth ? SettingsColumn.Categories : SettingsColumn.Settings;
        if (target != state.Column)
        {
            State = state with { Column = target };
            redraw();
        }
    }

    /// <summary>Call when the terminal size changed: the layout may collapse or expand.</summary>
    public void OnScreenResized()
    {
        if (State is null)
        {
            return;
        }

        State = State.WithLayout(IsCollapsed());
        redraw();
    }

    private string? SelectCategory(SettingsState state, int index)
    {
        if (!state.IsFiltering)
        {
            State = state with { Column = SettingsColumn.Categories, CategoryIndex = index, Cursor = 0 };
            redraw();
        }

        return null;
    }

    private string? ClickRow(SettingsState state, SettingsHit hit)
    {
        if (hit.Kind is SettingsHitKind.None)
        {
            return null;
        }

        var rows = state.Rows;
        if (hit.Index >= rows.Count)
        {
            return null;
        }

        var row = rows[hit.Index];
        var isSecondClick = state.Column == SettingsColumn.Settings && state.Cursor == hit.Index;
        var leavesChoice = state.Choice is not null && row.Kind != SettingRowKind.Option;
        State = (leavesChoice ? state with { Choice = null } : state) with
        {
            Column = SettingsColumn.Settings,
            Cursor = hit.Index,
        };
        State = State.Clamped();
        redraw();
        if (hit.Kind == SettingsHitKind.Add)
        {
            return DefaultMouseMap.SettingsAddButton;
        }

        if (hit.Kind == SettingsHitKind.Remove)
        {
            return DefaultMouseMap.SettingsRemoveButton;
        }

        var edits = hit.Kind == SettingsHitKind.Control || row.Kind == SettingRowKind.Option || isSecondClick;

        return edits ? DefaultMouseMap.SettingsEditButton : null;
    }

    private bool IsCollapsed()
    {
        var size = screenSize();

        return SettingsLayout.For(size.Width, size.Height) is { IsCollapsed: true };
    }

    private void Move(ActionId action, int? count)
    {
        if (State is not { IsTyping: false, Removal: null } state)
        {
            return;
        }

        var size = screenSize();
        var page = Math.Max(1, SettingsFrameBuilder.VisibleRows(size.Width, size.Height));
        if (ListMotion.TryMove(action, count, state.ActiveIndex, state.ActiveCount, page, out var target))
        {
            State = state.MoveTo(target);
            redraw();
        }
    }

    private void Run(SettingsCommandKind command)
    {
        if (State is not { } state)
        {
            return;
        }

        var outcome = SettingsReducer.Reduce(state, command, environment);
        State = outcome.State;
        if (outcome.Warning.Length > 0)
        {
            statuses.Warning(outcome.Warning);
        }

        if (outcome.Closes)
        {
            Close();

            return;
        }

        SyncMode();
        if (outcome.OpensEditor)
        {
            OpenEditor();
        }

        if (outcome.Commit is { } commit)
        {
            Write(commit);
        }

        redraw();
    }

    private void Close()
    {
        State = null;
        SyncMode();
        redraw();
    }

    // Insert while a number, folder or filter is typed, Confirm while the removal question is up, else Normal.
    private void SyncMode()
    {
        var wanted = State switch
        {
            null => KeyMode.Normal,
            { Removal: not null } => KeyMode.Confirm,
            { IsTyping: true } => KeyMode.Insert,
            _ => KeyMode.Normal,
        };
        if (wanted != _mode)
        {
            _mode = wanted;
            setMode(wanted);
        }
    }

    private void Open()
    {
        if (_isBusy)
        {
            return;
        }

        ReadThen(Show);
    }

    private void Show(ConfigSnapshot snapshot)
    {
        _snapshot = snapshot;
        var load = SettingsValueReader.Read(snapshot, environment.HomeDirectory, environment.DirectoryExists);
        State = SettingsState.Create(_definitions, load).WithLayout(IsCollapsed());
        ApplyOptions(load);
        SyncMode();
        redraw();
    }

    // Reads the file off the UI thread, then continues on it. Reads and writes never overlap.
    private void ReadThen(Action<ConfigSnapshot> then)
    {
        _isBusy = true;
        _ = Task.Run(() => ReadAsync(then), CancellationToken.None);
    }

    private async Task ReadAsync(Action<ConfigSnapshot> then)
    {
        try
        {
            var snapshot = await writer.ReadAsync(cancellationToken).ConfigureAwait(false);
            postToUi(() => Finished(then, snapshot));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _isBusy = false);
        }
    }

    private void Finished(Action<ConfigSnapshot> then, ConfigSnapshot snapshot)
    {
        _isBusy = false;
        then(snapshot);
    }

    private void OpenEditor()
    {
        var notice = launchEditor(writer.FilePath, ReloadAfterEditor);
        if (notice.Length > 0)
        {
            statuses.Warning(notice);
        }
    }

    private void ReloadAfterEditor()
    {
        if (_isBusy)
        {
            return;
        }

        ReadThen(Reloaded);
    }

    private void Reloaded(ConfigSnapshot snapshot)
    {
        _snapshot = snapshot;
        ApplyLoad(snapshot);
        statuses.Post("Read config.json again; key bindings apply after a restart");
        redraw();
    }

    private void Write(SettingsCommit commit)
    {
        if (_isBusy)
        {
            statuses.Warning("Still saving config.json: repeat the change in a moment");

            return;
        }

        _isBusy = true;
        var json = commit.Value.ToJson(commit.Definition.Control);
        var snapshot = _snapshot;
        _ = Task.Run(() => WriteAsync(commit, json, snapshot), CancellationToken.None);
    }

    private async Task WriteAsync(SettingsCommit commit, string json, ConfigSnapshot snapshot)
    {
        try
        {
            var result = await writer
                .SetAsync(commit.Definition.ConfigPath, json, snapshot, cancellationToken)
                .ConfigureAwait(false);
            postToUi(() => Written(commit, result));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _isBusy = false);
        }
    }

    private void Written(SettingsCommit commit, ConfigWriteResult result)
    {
        _isBusy = false;
        switch (result.Status)
        {
            case ConfigWriteStatus.Written:
                _snapshot = result.Snapshot;
                ApplyLoad(result.Snapshot);
                SelectWritten(commit);
                statuses.Post($"Saved {commit.Definition.Name}");
                if (result.Shadowed)
                {
                    statuses.Warning(result.Message);
                }

                break;
            case ConfigWriteStatus.Stale:
                _snapshot = result.Snapshot;
                ApplyLoad(result.Snapshot);
                statuses.Warning(result.Message);
                break;
            case ConfigWriteStatus.Refused:
                statuses.Warning(result.Message);
                break;
            default:
                statuses.Error(result.Message);
                break;
        }

        SyncMode();
        redraw();
    }

    private void SelectWritten(SettingsCommit commit)
    {
        if (commit.SelectItem >= 0 && State is { IsFiltering: false } state)
        {
            State = state.Reveal(commit.Definition, SettingRowKind.Item, commit.SelectItem);
        }
    }

    private void ApplyLoad(ConfigSnapshot snapshot)
    {
        var load = SettingsValueReader.Read(snapshot, environment.HomeDirectory, environment.DirectoryExists);
        State = State?.WithLoad(load);
        ApplyOptions(load);
    }

    // A file that cannot be edited is not applied: the running app keeps what it started with.
    private void ApplyOptions(SettingsLoad load)
    {
        if (load.Banner.Length == 0)
        {
            applyOptions(load.Options);
        }
    }
}
