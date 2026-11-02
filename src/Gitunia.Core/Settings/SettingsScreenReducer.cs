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
public static partial class SettingsScreenReducer
{
    /// <summary>The warning while a read or write of the file is still running.</summary>
    public const string StillSavingNotice = "Still saving config.json: repeat the change in a moment";

    /// <summary>The notice after the editor closed and the file was read again.</summary>
    public const string ReloadedNotice = "Read config.json again; key bindings apply after a restart";

    private const string MissingFolderReason = "is not an existing folder";

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
            SettingsFolderChecked checkedFolder => FolderChecked(session, checkedFolder, effects),
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

        var outcome = SettingsReducer.Reduce(screen, kind, session.HomeDirectory);
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

        if (outcome.CheckFolder is { } check)
        {
            return CheckFolder(next, check, effects);
        }

        return outcome.Commit is { } commit ? Write(next, commit, effects) : next;
    }

    // The folder check holds the file busy so no read or write starts in between; the result writes or refuses.
    private static SettingsSession CheckFolder(
        SettingsSession session,
        SettingsFolderCheck check,
        List<AppEffect> effects)
    {
        if (session.IsBusy)
        {
            effects.Add(new PostStatusEffect(StillSavingNotice, StatusSeverity.Warning));

            return session;
        }

        effects.Add(new CheckSettingsFolderEffect(check));

        return session with { IsBusy = true };
    }

    // Esc while the check ran closes the box, and a new box may hold other text by now: the answer is dropped then.
    private static SettingsSession FolderChecked(
        SettingsSession session,
        SettingsFolderChecked answer,
        List<AppEffect> effects)
    {
        var idle = session with { IsBusy = false };
        if (idle.Screen is not { Input: { } input } || input.Text.Trim() != answer.Check.Typed)
        {
            return idle;
        }

        if (!answer.Exists)
        {
            var message = $"\"{answer.Check.Typed}\" {MissingFolderReason}";
            effects.Add(new PostStatusEffect(message, StatusSeverity.Warning));

            return idle;
        }

        return Write(idle, answer.Check.Commit, effects);
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
}
