using Gitunia.Core.Config.Models;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Status;

namespace Gitunia.Core.Settings;

public static partial class SettingsScreenReducer
{
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
