using Gitunia.Core.Config.Models;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Search;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Status;

namespace Gitunia.Core.Settings;

/// <summary>
/// The settings screen as pure transitions <c>(session, command) -> (session, effects)</c>. The edits of the open
/// screen are <see cref="SettingsReducer"/>; this reducer adds what surrounds them: reading and writing the file as
/// effects (never two at once), the key mode, and the theme on screen (the one under the cursor while picking, else the
/// one just kept, else the one of the file, so Esc or closing restores the previous theme).
/// </summary>
public static class SettingsScreenReducer
{
    /// <summary>The warning while a read or write of the file is still running.</summary>
    public const string StillSavingNotice = "Still saving config.json: repeat the change in a moment";

    /// <summary>The notice after the editor closed and the file was read again.</summary>
    public const string ReloadedNotice = "Read config.json again; key bindings apply after a restart";

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

    /// <summary>True when the action is the settings' to handle: any action while the screen is open, else the
    /// three that open it or toggle the command level.</summary>
    /// <param name="action">The bound action.</param>
    /// <param name="isOpen">The screen is open.</param>
    public static bool Consumes(ActionId action, bool isOpen) =>
        isOpen
        || action == ActionIds.SettingsOpen
        || action == ActionIds.SettingsOpenTheme
        || action == ActionIds.ViewCycleCommandLevel;

    /// <summary>Applies a command.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="command">What happened.</param>
    public static SettingsUpdate Reduce(SettingsSession session, SettingsCommand command)
    {
        var effects = new List<AppEffect>();
        var next = Apply(session, command, effects);
        next = SyncTheme(next, effects);

        return new SettingsUpdate(next, [.. effects]);
    }

    private static SettingsSession Apply(SettingsSession session, SettingsCommand command, List<AppEffect> effects) =>
        command switch
        {
            SettingsActionPressed pressed => Press(session, pressed, effects),
            SettingsCategoryClicked clicked => SelectCategory(session, clicked.Index),
            SettingsColumnFocused focused => FocusColumn(session, focused.Column),
            SettingsEditorClosed => Reload(session, effects),
            SettingsFileRead read => Finished(session, read, effects),
            SettingsKeyTyped typed => TypeKey(session, typed.Key),
            SettingsOperationCancelled => session with { IsBusy = false },
            SettingsOperationFailed failed => Failed(session, failed.Text, effects),
            SettingsRowClicked clicked => SelectRow(session, clicked.Index),
            SettingsViewportChanged changed => Resize(session, changed.Viewport),
            SettingsWritten written => Written(session, written, effects),
            _ => throw new UnhandledCommandException(command),
        };

    private static SettingsSession Press(
        SettingsSession session,
        SettingsActionPressed pressed,
        List<AppEffect> effects)
    {
        var action = pressed.Action;
        if (session.Screen is null)
        {
            return PressClosed(session, action, effects);
        }

        if (Commands.TryGetValue(action, out var kind))
        {
            return Run(session, kind, effects);
        }

        return Move(session, pressed);
    }

    private static SettingsSession PressClosed(SettingsSession session, ActionId action, List<AppEffect> effects)
    {
        if (action == ActionIds.ViewCycleCommandLevel)
        {
            if (session.IsBusy)
            {
                effects.Add(new PostStatusEffect(StillSavingNotice, StatusSeverity.Warning));

                return session;
            }

            effects.Add(new ReadSettingsEffect(SettingsReadIntent.CycleCommandLevel));

            return session with { IsBusy = true };
        }

        if (action != ActionIds.SettingsOpen && action != ActionIds.SettingsOpenTheme)
        {
            return session;
        }

        if (session.IsBusy)
        {
            return session;
        }

        var atTheme = action == ActionIds.SettingsOpenTheme;
        effects.Add(new ReadSettingsEffect(atTheme ? SettingsReadIntent.OpenAtTheme : SettingsReadIntent.Open));

        return session with { IsBusy = true };
    }

    private static SettingsSession Move(SettingsSession session, SettingsActionPressed pressed)
    {
        if (session.Screen is not { IsTyping: false, Removal: null } screen)
        {
            return session;
        }

        var page = Math.Max(1, session.Viewport.PageRows);
        var moved = ListMotion.TryMove(
            pressed.Action,
            pressed.Count,
            screen.ActiveIndex,
            screen.ActiveCount,
            page,
            out var target);
        if (moved)
        {
            return session with { Screen = screen.MoveTo(target) };
        }

        return session;
    }

    private static SettingsSession Run(SettingsSession session, SettingsCommandKind kind, List<AppEffect> effects)
    {
        if (session.Screen is not { } screen)
        {
            return session;
        }

        var outcome = SettingsReducer.Reduce(screen, kind, session.Environment);
        var next = session with { Screen = outcome.State };
        if (outcome.Warning.Length > 0)
        {
            effects.Add(new PostStatusEffect(outcome.Warning, StatusSeverity.Warning));
        }

        if (outcome.Notice.Length > 0)
        {
            effects.Add(new PostStatusEffect(outcome.Notice, StatusSeverity.Info));
        }

        if (outcome.Closes)
        {
            return SyncMode(next with { Screen = null }, effects);
        }

        next = SyncMode(next, effects);
        if (outcome.OpensEditor)
        {
            effects.Add(new OpenConfigEditorEffect());
        }

        return outcome.Commit is { } commit ? Write(next, commit, effects) : next;
    }

    private static SettingsSession TypeKey(SettingsSession session, KeyEvent key)
    {
        if (session.Screen is not { IsTyping: true } screen)
        {
            return session;
        }

        if (screen.Input is { } input)
        {
            if (PromptTextEditor.Apply(input.Text, key) is { } text && text != input.Text)
            {
                return session with { Screen = screen.WithInputText(text) };
            }

            return session;
        }

        if (PromptTextEditor.Apply(screen.Filter, key) is { } filter && filter != screen.Filter)
        {
            return session with { Screen = screen.WithFilterText(filter) };
        }

        return session;
    }

    private static SettingsSession SelectCategory(SettingsSession session, int index)
    {
        if (session.Screen is { IsFiltering: false } screen)
        {
            var selected = screen with { Column = SettingsColumn.Categories, CategoryIndex = index, Cursor = 0 };

            return session with { Screen = selected };
        }

        return session;
    }

    private static SettingsSession SelectRow(SettingsSession session, int index)
    {
        if (session.Screen is not { } screen || index >= screen.Rows.Count)
        {
            return session;
        }

        var row = screen.Rows[index];
        var leavesChoice = screen.Choice is not null && row.Kind != SettingRowKind.Option;
        var selected = (leavesChoice ? screen with { Choice = null } : screen) with
        {
            Column = SettingsColumn.Settings,
            Cursor = index,
        };

        return session with { Screen = selected.Clamped() };
    }

    private static SettingsSession FocusColumn(SettingsSession session, SettingsColumn column)
    {
        if (session.Screen is not { IsCollapsed: false, IsTyping: false, Removal: null, Choice: null } screen
            || screen.IsFiltering
            || column == screen.Column)
        {
            return session;
        }

        return session with { Screen = screen with { Column = column } };
    }

    private static SettingsSession Resize(SettingsSession session, SettingsViewport viewport)
    {
        var resized = session with { Viewport = viewport };

        if (session.Screen is not { } screen)
        {
            return resized;
        }

        return resized with { Screen = screen.WithLayout(viewport.IsCollapsed) };
    }

    private static SettingsSession Reload(SettingsSession session, List<AppEffect> effects)
    {
        if (session.IsBusy)
        {
            return session;
        }

        effects.Add(new ReadSettingsEffect(SettingsReadIntent.Reload));

        return session with { IsBusy = true };
    }

    private static SettingsSession Finished(SettingsSession session, SettingsFileRead file, List<AppEffect> effects)
    {
        var read = file.Read;
        var idle = session with { IsBusy = false, Snapshot = read.Snapshot };

        return file.Intent switch
        {
            SettingsReadIntent.CycleCommandLevel => WriteNextCommandLevel(idle, file, effects),
            SettingsReadIntent.Reload => Reloaded(idle, read, effects),
            _ => Show(idle, file, effects),
        };
    }

    private static SettingsSession Show(SettingsSession session, SettingsFileRead file, List<AppEffect> effects)
    {
        var screen = SettingsState.Create(file.Definitions, file.Read.Load, file.KeyReference)
            .WithLayout(session.Viewport.IsCollapsed);
        var theme = file.Definitions.FirstOrDefault(definition => definition.Id == SettingsRegistry.ThemeId);
        if (file.Intent == SettingsReadIntent.OpenAtTheme && theme is not null)
        {
            screen = screen.Reveal(theme);
        }

        var shown = ApplyOptions(session with { Screen = screen }, file.Read.Load, effects);

        return SyncMode(shown, effects);
    }

    private static SettingsSession Reloaded(SettingsSession session, SettingsRead read, List<AppEffect> effects)
    {
        var loaded = ApplyLoad(session, read.Load, effects);
        effects.Add(new PostStatusEffect(ReloadedNotice, StatusSeverity.Info));

        return loaded;
    }

    // The fast toggle (#839): writes the next level and applies it like the settings row does.
    private static SettingsSession WriteNextCommandLevel(
        SettingsSession session,
        SettingsFileRead file,
        List<AppEffect> effects)
    {
        var next = CommandLevels.Next(file.Read.Load.Options.CommandLevel);
        var definition = file.Definitions.FirstOrDefault(candidate => candidate.Id == SettingsRegistry.CommandLevelId)
            ?? SettingsRegistry.All.First(candidate => candidate.Id == SettingsRegistry.CommandLevelId);
        var commit = new SettingsCommit(definition, SettingValue.OfChoice(CommandLevels.NameOf(next)));

        return Write(session, commit, effects);
    }

    private static SettingsSession Write(SettingsSession session, SettingsCommit commit, List<AppEffect> effects)
    {
        if (session.IsBusy)
        {
            effects.Add(new PostStatusEffect(StillSavingNotice, StatusSeverity.Warning));

            return session;
        }

        var pending = session.Pending;
        if (commit.Definition.Id == SettingsRegistry.ThemeId)
        {
            pending = SettingsValueReader.ThemeOf(commit.Value.Choice);
        }

        var json = commit.Value.ToJson(commit.Definition.Control);
        effects.Add(new WriteSettingEffect(commit, json, session.Snapshot));

        return session with { IsBusy = true, Pending = pending };
    }

    private static SettingsSession Written(SettingsSession session, SettingsWritten written, List<AppEffect> effects)
    {
        var next = session with { IsBusy = false, Pending = null };
        if (written.Read is { } read)
        {
            next = ApplyLoad(next with { Snapshot = read.Snapshot }, read.Load, effects);
        }

        var result = written.Result;
        switch (result.Status)
        {
            case ConfigWriteStatus.Written:
                next = SelectWritten(next, written.Commit);
                effects.Add(new PostStatusEffect(SavedMessage(written.Commit), StatusSeverity.Info));
                if (result.Shadowed)
                {
                    effects.Add(new PostStatusEffect(result.Message, StatusSeverity.Warning));
                }

                break;
            case ConfigWriteStatus.Stale:
            case ConfigWriteStatus.Refused:
                effects.Add(new PostStatusEffect(result.Message, StatusSeverity.Warning));
                break;
            default:
                effects.Add(new PostStatusEffect(result.Message, StatusSeverity.Error));
                break;
        }

        return SyncMode(next, effects);
    }

    private static SettingsSession Failed(SettingsSession session, string text, List<AppEffect> effects)
    {
        effects.Add(new PostStatusEffect(text, StatusSeverity.Error));

        return session with { IsBusy = false, Pending = null };
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

    private static SettingsSession SelectWritten(SettingsSession session, SettingsCommit commit)
    {
        if (commit.SelectItem >= 0 && session.Screen is { IsFiltering: false } screen)
        {
            return session with { Screen = screen.Reveal(commit.Definition, SettingRowKind.Item, commit.SelectItem) };
        }

        return session;
    }

    private static SettingsSession ApplyLoad(SettingsSession session, SettingsLoad load, List<AppEffect> effects)
    {
        var loaded = session with { Screen = session.Screen?.WithLoad(load) };

        return ApplyOptions(loaded, load, effects);
    }

    // A file that cannot be edited is not applied: the running app keeps what it started with.
    private static SettingsSession ApplyOptions(SettingsSession session, SettingsLoad load, List<AppEffect> effects)
    {
        if (load.Banner.Length > 0)
        {
            return session;
        }

        effects.Add(new ApplySettingsOptionsEffect(load.Options));

        return session with { Committed = load.Theme };
    }

    // Insert while a number, folder or filter is typed, Confirm while the removal question is up, else Normal.
    private static SettingsSession SyncMode(SettingsSession session, List<AppEffect> effects)
    {
        var wanted = session.Screen switch
        {
            null => KeyMode.Normal,
            { Removal: not null } => KeyMode.Confirm,
            { IsTyping: true } => KeyMode.Insert,
            _ => KeyMode.Normal,
        };
        if (wanted == session.Mode)
        {
            return session;
        }

        effects.Add(new SetKeyModeEffect(wanted));

        return session with { Mode = wanted };
    }

    private static SettingsSession SyncTheme(SettingsSession session, List<AppEffect> effects)
    {
        var wanted = session.Screen?.PreviewTheme ?? session.Pending ?? session.Committed;
        if (wanted is not { } theme || session.Shown == theme)
        {
            return session;
        }

        effects.Add(new ApplySettingsThemeEffect(theme));

        return session with { Shown = theme };
    }
}
