using System.Collections.Immutable;
using System.Globalization;
using Gitunia.Core.Config;
using Gitunia.Core.Settings.Models;

namespace Gitunia.Core.Settings;

/// <summary>
/// The transitions of the settings screen: <c>(state, command) -> state</c> plus at most one effect for the caller
/// (a change to write, the editor to open, the screen to close). Pure: a folder is only checked for shape and
/// duplicates here; that it exists is an effect.
/// </summary>
public static class SettingsReducer
{
    private const string NoFolder = "Type a folder path first (Esc cancels)";

    /// <summary>Applies one command.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="command">What was asked.</param>
    /// <param name="homeDirectory">What a leading <c>~</c> stands for.</param>
    public static SettingsOutcome Reduce(
        SettingsState state,
        SettingsCommandKind command,
        string homeDirectory)
    {
        return command switch
        {
            SettingsCommandKind.Back => Back(state),
            SettingsCommandKind.Close => new SettingsOutcome(state, Closes: true),
            SettingsCommandKind.Activate => Activate(state),
            SettingsCommandKind.Toggle => Toggle(state),
            SettingsCommandKind.Add => Add(state),
            SettingsCommandKind.Remove => Remove(state),
            SettingsCommandKind.ItemDown => MoveItem(state, 1),
            SettingsCommandKind.ItemUp => MoveItem(state, -1),
            SettingsCommandKind.Increase => Step(state, 1),
            SettingsCommandKind.Decrease => Step(state, -1),
            SettingsCommandKind.FilterOpen => FilterOpen(state),
            SettingsCommandKind.InputAccept => InputAccept(state, homeDirectory),
            SettingsCommandKind.ConfirmYes => ConfirmYes(state),
            SettingsCommandKind.ConfirmNo => ConfirmNo(state),
            _ => new SettingsOutcome(state),
        };
    }

    private static SettingsOutcome Back(SettingsState state)
    {
        if (state.Removal is not null)
        {
            return new SettingsOutcome(state with { Removal = null });
        }

        if (state.Input is not null)
        {
            return new SettingsOutcome((state with { Input = null }).Clamped());
        }

        if (state.IsEditingFilter)
        {
            var cleared = state with
            {
                Filter = string.Empty,
                IsEditingFilter = false,
                Column = SettingsColumn.Categories,
                Cursor = 0,
            };

            return new SettingsOutcome(cleared);
        }

        if (state is { Choice: { } choice } && FindDefinition(state, choice.SettingId) is { } edited)
        {
            return new SettingsOutcome((state with { Choice = null }).Reveal(edited));
        }

        if (state.IsFiltering)
        {
            return new SettingsOutcome(LeaveFilter(state));
        }

        if (state.Column == SettingsColumn.Settings && !state.IsCollapsed)
        {
            return new SettingsOutcome(state with { Column = SettingsColumn.Categories });
        }

        return new SettingsOutcome(state, Closes: true);
    }

    private static SettingsOutcome Activate(SettingsState state)
    {
        if (state.Removal is not null || state.Input is not null || state.IsEditingFilter)
        {
            return new SettingsOutcome(state);
        }

        if (state.IsFiltering)
        {
            return new SettingsOutcome(LeaveFilter(state));
        }

        if (state.Column == SettingsColumn.Categories && !state.ListsAllCategories)
        {
            var entered = state with { Column = SettingsColumn.Settings, Cursor = 0 };

            return new SettingsOutcome(state.Rows.Count == 0 ? state : entered);
        }

        return state.Selected is { } row ? ActivateRow(state, row) : new SettingsOutcome(state);
    }

    private static SettingsOutcome ActivateRow(SettingsState state, SettingRow row)
    {
        var definition = row.Definition;
        if (definition.Control == SettingControl.Action)
        {
            return new SettingsOutcome(state, OpensEditor: true);
        }

        if (row.Kind == SettingRowKind.Binding)
        {
            return new SettingsOutcome(state, Notice: ShowEntry(state.Reference[row.Index]));
        }

        if (definition.Control == SettingControl.KeyReference)
        {
            return new SettingsOutcome(state);
        }

        if (state.IsReadOnly)
        {
            return Refuse(state, state.Banner);
        }

        return row.Kind switch
        {
            SettingRowKind.Option => KeepOption(state, row),
            SettingRowKind.Item => StartPathEdit(state, row),
            SettingRowKind.NewItem => new SettingsOutcome(state),
            _ => ActivateSetting(state, definition),
        };
    }

    private static SettingsOutcome ActivateSetting(SettingsState state, SettingDefinition definition)
    {
        var value = state.ValueOf(definition);
        switch (definition.Control)
        {
            case SettingControl.Toggle:
                return Commit(state, definition, SettingValue.OfFlag(!value.Flag));
            case SettingControl.Choice:
                var original = Math.Max(0, IndexOfChoice(definition, value.Choice));
                var editing = state with { Choice = new ChoiceEdit(definition.Id) };

                return new SettingsOutcome(editing.Reveal(definition, SettingRowKind.Option, original));
            case SettingControl.Number:
                var number = new SettingsInput(
                    SettingsInputKind.Number,
                    definition.Id,
                    -1,
                    value.Number.ToString(CultureInfo.InvariantCulture));

                return new SettingsOutcome(state with { Input = number });
            case SettingControl.PathList:
                return StartPathAdd(state, definition);
            default:
                return new SettingsOutcome(state);
        }
    }

    private static string ShowEntry(KeyReferenceEntry entry) =>
        $"{entry.Title}, config.json keys entry: {entry.Snippet}";

    private static SettingsOutcome KeepOption(SettingsState state, SettingRow row)
    {
        var definition = row.Definition;
        var option = definition.Choices[row.Index];
        var kept = (state with { Choice = null }).Reveal(definition);
        if (string.Equals(state.ValueOf(definition).Choice, option, StringComparison.Ordinal))
        {
            return new SettingsOutcome(kept);
        }

        return new SettingsOutcome(kept, new SettingsCommit(definition, SettingValue.OfChoice(option)));
    }

    private static SettingsOutcome StartPathAdd(SettingsState state, SettingDefinition definition)
    {
        var count = state.ValueOf(definition).Paths.Length;
        var input = new SettingsInput(SettingsInputKind.PathAdd, definition.Id, -1, string.Empty);
        var typing = state with { Input = input };

        return new SettingsOutcome(typing.Reveal(definition, SettingRowKind.NewItem, count));
    }

    private static SettingsOutcome StartPathEdit(SettingsState state, SettingRow row)
    {
        var path = state.ValueOf(row.Definition).Paths[row.Index];
        var input = new SettingsInput(SettingsInputKind.PathEdit, row.Definition.Id, row.Index, path);

        return new SettingsOutcome(state with { Input = input });
    }

    private static SettingsOutcome Toggle(SettingsState state)
    {
        if (state.Selected is not { Kind: SettingRowKind.Setting, Definition.Control: SettingControl.Toggle } row)
        {
            return new SettingsOutcome(state);
        }

        if (state.IsReadOnly)
        {
            return Refuse(state, state.Banner);
        }

        return Commit(state, row.Definition, SettingValue.OfFlag(!state.ValueOf(row.Definition).Flag));
    }

    private static SettingsOutcome Add(SettingsState state)
    {
        if (state.Selected is not { Definition.Control: SettingControl.PathList } row
            || row.Kind is SettingRowKind.Option or SettingRowKind.NewItem
            || state.Removal is not null
            || state.IsTyping)
        {
            return new SettingsOutcome(state);
        }

        return state.IsReadOnly ? Refuse(state, state.Banner) : StartPathAdd(state, row.Definition);
    }

    private static SettingsOutcome Remove(SettingsState state)
    {
        if (state.Selected is not { Kind: SettingRowKind.Item } row || state.Removal is not null || state.IsTyping)
        {
            return new SettingsOutcome(state);
        }

        if (state.IsReadOnly)
        {
            return Refuse(state, state.Banner);
        }

        var path = state.ValueOf(row.Definition).Paths[row.Index];

        return new SettingsOutcome(state with { Removal = new PendingRemoval(row.Definition.Id, row.Index, path) });
    }

    private static SettingsOutcome ConfirmYes(SettingsState state)
    {
        if (state.Removal is not { } removal || FindDefinition(state, removal.SettingId) is not { } definition)
        {
            return new SettingsOutcome(state with { Removal = null });
        }

        var paths = state.ValueOf(definition).Paths;
        if (removal.ItemIndex >= paths.Length)
        {
            return new SettingsOutcome(state with { Removal = null });
        }

        var remaining = paths.RemoveAt(removal.ItemIndex);
        var select = remaining.Length == 0 ? -1 : Math.Min(removal.ItemIndex, remaining.Length - 1);
        var commit = new SettingsCommit(definition, SettingValue.OfPaths(remaining), select);

        return new SettingsOutcome(state with { Removal = null }, commit);
    }

    private static SettingsOutcome ConfirmNo(SettingsState state) => new(state with { Removal = null });

    private static SettingsOutcome MoveItem(SettingsState state, int direction)
    {
        if (state.Selected is not { Kind: SettingRowKind.Item } row || state.IsTyping || state.Removal is not null)
        {
            return new SettingsOutcome(state);
        }

        if (state.IsReadOnly)
        {
            return Refuse(state, state.Banner);
        }

        var paths = state.ValueOf(row.Definition).Paths;
        var target = row.Index + direction;
        if (target < 0 || target >= paths.Length)
        {
            return new SettingsOutcome(state);
        }

        var moved = paths.SetItem(row.Index, paths[target]).SetItem(target, paths[row.Index]);

        return new SettingsOutcome(state, new SettingsCommit(row.Definition, SettingValue.OfPaths(moved), target));
    }

    private static SettingsOutcome Step(SettingsState state, int direction)
    {
        if (state.Selected is not { Kind: SettingRowKind.Setting, Definition.Control: SettingControl.Number } row
            || state.IsTyping)
        {
            return new SettingsOutcome(state);
        }

        if (state.IsReadOnly)
        {
            return Refuse(state, state.Banner);
        }

        var definition = row.Definition;
        var next = state.ValueOf(definition).Number + (direction * definition.Step);

        if (!IsInRange(definition, next))
        {
            return Refuse(state, RangeMessage(definition));
        }

        return Commit(state, definition, SettingValue.OfNumber(next));
    }

    private static SettingsOutcome FilterOpen(SettingsState state)
    {
        if (state.IsTyping || state.Removal is not null || state.Choice is not null)
        {
            return new SettingsOutcome(state);
        }

        var opened = state with
        {
            Filter = string.Empty,
            IsEditingFilter = true,
            Column = SettingsColumn.Settings,
            Cursor = 0,
        };

        return new SettingsOutcome(opened);
    }

    private static SettingsOutcome InputAccept(SettingsState state, string homeDirectory)
    {
        if (state.IsEditingFilter)
        {
            return new SettingsOutcome((state with { IsEditingFilter = false }).Clamped());
        }

        if (state.Input is not { } input || FindDefinition(state, input.SettingId) is not { } definition)
        {
            return new SettingsOutcome(state);
        }

        if (input.Kind == SettingsInputKind.Number)
        {
            return AcceptNumber(state, definition, input);
        }

        return AcceptPath(state, definition, input, homeDirectory);
    }

    private static SettingsOutcome AcceptNumber(SettingsState state, SettingDefinition definition, SettingsInput input)
    {
        var text = input.Text.Trim();
        var parsed = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
        if (!parsed || !IsInRange(definition, number))
        {
            return Refuse(state, RangeMessage(definition));
        }

        return Commit(state, definition, SettingValue.OfNumber(number));
    }

    private static SettingsOutcome AcceptPath(
        SettingsState state,
        SettingDefinition definition,
        SettingsInput input,
        string homeDirectory)
    {
        var paths = state.ValueOf(definition).Paths;
        var ignored = input.Kind == SettingsInputKind.PathEdit ? input.ItemIndex : -1;
        var typed = input.Text.Trim();
        var error = CheckPath(typed, paths, ignored, homeDirectory, out var expanded);
        if (error.Length > 0)
        {
            return Refuse(state, error);
        }

        var isEdit = input.Kind == SettingsInputKind.PathEdit;
        var changed = isEdit ? paths.SetItem(input.ItemIndex, typed) : paths.Add(typed);
        var select = isEdit ? input.ItemIndex : changed.Length - 1;
        var commit = new SettingsCommit(definition, SettingValue.OfPaths(changed), select);

        return new SettingsOutcome(state, CheckFolder: new SettingsFolderCheck(expanded, typed, commit));
    }

    // Empty string when the folder passes the checks that need no disk; else why not.
    private static string CheckPath(
        string typed,
        ImmutableArray<string> existing,
        int ignoredIndex,
        string homeDirectory,
        out string expanded)
    {
        expanded = string.Empty;
        if (typed.Length == 0)
        {
            return NoFolder;
        }

        expanded = Normalize(typed, homeDirectory);
        if (!Path.IsPathFullyQualified(expanded))
        {
            var starts = OperatingSystem.IsWindows() ? @"~ or C:\" : "~ or /";
            return $"\"{typed}\" is not an absolute path (start it with {starts})";
        }

        for (var index = 0; index < existing.Length; index++)
        {
            if (index != ignoredIndex && Normalize(existing[index], homeDirectory) == expanded)
            {
                return $"\"{typed}\" is already in the list";
            }
        }

        return string.Empty;
    }

    private static string Normalize(string text, string homeDirectory)
    {
        if (text.Length == 0)
        {
            return text;
        }

        return Path.TrimEndingDirectorySeparator(OptionsConfigurator.ExpandHome(text, homeDirectory));
    }

    private static SettingsOutcome Commit(SettingsState state, SettingDefinition definition, SettingValue value) =>
        new(state, new SettingsCommit(definition, value));

    private static SettingsOutcome Refuse(SettingsState state, string message) => new(state, Warning: message);

    private static SettingsState LeaveFilter(SettingsState state)
    {
        var selected = state.Selected;
        var cleared = state with { Filter = string.Empty, IsEditingFilter = false };

        if (selected is null)
        {
            return cleared.Clamped();
        }

        return cleared.Reveal(selected.Definition, selected.Kind, selected.Index);
    }

    private static SettingDefinition? FindDefinition(SettingsState state, string id) =>
        state.Definitions.FirstOrDefault(definition => definition.Id == id);

    private static int IndexOfChoice(SettingDefinition definition, string choice)
    {
        for (var index = 0; index < definition.Choices.Count; index++)
        {
            if (string.Equals(definition.Choices[index], choice, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsInRange(SettingDefinition definition, int number) =>
        number >= definition.Minimum && number <= definition.Maximum;

    private static string RangeMessage(SettingDefinition definition) =>
        $"{definition.Name} must be a whole number from {definition.Minimum} to {definition.Maximum}";
}
