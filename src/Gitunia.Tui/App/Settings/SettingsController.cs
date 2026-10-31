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
/// The settings screen (ux-settings.md, "Settings screen"): a modal overlay over every tab that edits <c>config.json</c> through
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
/// <param name="applyTheme">Shows a theme in the running app: the one under the cursor, else the kept one.</param>
/// <param name="launchEditor">Opens a file in the editor and calls back when it closes; returns why it cannot.</param>
/// <param name="keyReference">Builds the key reference from the running keymap when the screen opens.</param>
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
    Action<ThemeChoice> applyTheme,
    Func<string, Action, string> launchEditor,
    Func<IReadOnlyList<KeyReferenceEntry>> keyReference,
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
    private ThemeChoice? _committedTheme;
    private ThemeChoice? _pendingTheme;
    private ThemeChoice? _shownTheme;

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
            if (action == ActionIds.ViewCycleCommandLevel)
            {
                CycleCommandLevel();

                return true;
            }

            if (action != ActionIds.SettingsOpen && action != ActionIds.SettingsOpenTheme)
            {
                return false;
            }

            Open(action == ActionIds.SettingsOpenTheme);

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
            if (hit.Kind == SettingsHitKind.ConfirmYes)
            {
                return DefaultMouseMap.ConfirmYesButton;
            }

            return DefaultMouseMap.ConfirmNoButton;
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
        SyncTheme();
        redraw();
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
            SyncTheme();
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

        if (outcome.Notice.Length > 0)
        {
            statuses.Post(outcome.Notice);
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

        SyncTheme();
        redraw();
    }

    // The fast toggle (#839): reads the file, writes the next level and applies it like the settings row does.
    private void CycleCommandLevel()
    {
        if (_isBusy)
        {
            statuses.Warning("Still saving config.json: repeat the change in a moment");

            return;
        }

        ReadThen(WriteNextCommandLevel);
    }

    private void WriteNextCommandLevel(SettingsRead read)
    {
        _snapshot = read.Snapshot;
        var next = CommandLevels.Next(read.Load.Options.CommandLevel);
        var definition = _definitions.FirstOrDefault(candidate => candidate.Id == SettingsRegistry.CommandLevelId)
            ?? SettingsRegistry.All.First(candidate => candidate.Id == SettingsRegistry.CommandLevelId);
        Write(new SettingsCommit(definition, SettingValue.OfChoice(CommandLevels.NameOf(next))));
    }

    private void Close()
    {
        State = null;
        SyncMode();
        SyncTheme();
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

    private void Open(bool atTheme)
    {
        if (_isBusy)
        {
            return;
        }

        ReadThen(read => Show(read, atTheme));
    }

    private void Show(SettingsRead read, bool atTheme)
    {
        _snapshot = read.Snapshot;
        State = SettingsState.Create(_definitions, read.Load, keyReference()).WithLayout(IsCollapsed());
        var theme = _definitions.FirstOrDefault(definition => definition.Id == SettingsRegistry.ThemeId);
        if (atTheme && theme is not null)
        {
            State = State.Reveal(theme);
        }

        ApplyOptions(read.Load);
        SyncMode();
        redraw();
    }

    // Reads and parses the file off the UI thread (a scan root on a slow mount must not freeze the UI), then
    // continues on it. Reads and writes never overlap.
    private void ReadThen(Action<SettingsRead> then)
    {
        _isBusy = true;
        _ = Task.Run(() => ReadAsync(then), CancellationToken.None);
    }

    private async Task ReadAsync(Action<SettingsRead> then)
    {
        try
        {
            var snapshot = await writer.ReadAsync(cancellationToken).ConfigureAwait(false);
            var read = new SettingsRead(snapshot, Parse(snapshot));
            postToUi(() => Finished(then, read));
        }
        catch (OperationCanceledException)
        {
            postToUi(Idle);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            postToUi(() => Failed($"Could not read config.json: {exception.Message}"));
        }
    }

    private SettingsLoad Parse(ConfigSnapshot snapshot) =>
        SettingsValueReader.Read(
            snapshot,
            environment.HomeDirectory,
            environment.DirectoryExists,
            environment.ThemeNames,
            environment.DefaultLabelCaps);

    private void Finished(Action<SettingsRead> then, SettingsRead read)
    {
        _isBusy = false;
        then(read);
    }

    private void Idle() => _isBusy = false;

    private void Failed(string message)
    {
        _isBusy = false;
        _pendingTheme = null;
        statuses.Error(message);
        SyncTheme();
        redraw();
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

    private void Reloaded(SettingsRead read)
    {
        _snapshot = read.Snapshot;
        ApplyLoad(read.Load);
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
        if (commit.Definition.Id == SettingsRegistry.ThemeId)
        {
            _pendingTheme = SettingsValueReader.ThemeOf(commit.Value.Choice);
        }

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
            var changed = result.Status is ConfigWriteStatus.Written or ConfigWriteStatus.Stale;
            var read = changed ? new SettingsRead(result.Snapshot, Parse(result.Snapshot)) : null;
            postToUi(() => Written(commit, result, read));
        }
        catch (OperationCanceledException)
        {
            postToUi(Idle);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            postToUi(() => Failed($"Could not save the setting: {exception.Message}"));
        }
    }

    private void Written(SettingsCommit commit, ConfigWriteResult result, SettingsRead? read)
    {
        _isBusy = false;
        _pendingTheme = null;
        if (read is not null)
        {
            _snapshot = read.Snapshot;
            ApplyLoad(read.Load);
        }

        switch (result.Status)
        {
            case ConfigWriteStatus.Written:
                SelectWritten(commit);
                statuses.Post(SavedMessage(commit));
                if (result.Shadowed)
                {
                    statuses.Warning(result.Message);
                }

                break;
            case ConfigWriteStatus.Stale:
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
        SyncTheme();
        redraw();
    }

    private static string SavedMessage(SettingsCommit commit)
    {
        if (commit.Definition.Id == SettingsRegistry.CommandLevelId
            && CommandLevels.TryParse(commit.Value.Choice, out var level))
        {
            return $"{commit.Definition.Name}: {CommandLevels.LabelOf(level)}";
        }

        return $"Saved {commit.Definition.Name}";
    }

    private void SelectWritten(SettingsCommit commit)
    {
        if (commit.SelectItem >= 0 && State is { IsFiltering: false } state)
        {
            State = state.Reveal(commit.Definition, SettingRowKind.Item, commit.SelectItem);
        }
    }

    private void ApplyLoad(SettingsLoad load)
    {
        State = State?.WithLoad(load);
        ApplyOptions(load);
    }

    // A file that cannot be edited is not applied: the running app keeps what it started with.
    private void ApplyOptions(SettingsLoad load)
    {
        if (load.Banner.Length == 0)
        {
            applyOptions(load.Options);
            _committedTheme = load.Theme;
        }

        SyncTheme();
    }

    // The theme on screen: the one under the cursor while picking, else the one just kept (until its write is
    // answered), else the one of the file. Closing or Esc therefore restores the previous theme.
    private void SyncTheme()
    {
        var wanted = State?.PreviewTheme ?? _pendingTheme ?? _committedTheme;
        if (wanted is { } theme && _shownTheme != theme)
        {
            _shownTheme = theme;
            applyTheme(theme);
        }
    }
}
