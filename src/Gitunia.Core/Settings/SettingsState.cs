using System.Collections.Immutable;
using Gitunia.Core.Settings.Models;

namespace Gitunia.Core.Settings;

/// <summary>
/// The open settings screen: the registry it shows, the values read from the file, the cursor in the two columns, the
/// filter and whatever is being edited. Pure; <see cref="SettingsReducer"/> moves it on and the controller carries out
/// the effects (writing the file).
/// </summary>
/// <param name="Definitions">The settings on offer, in registry order.</param>
/// <param name="Values">The value of each setting by id.</param>
/// <param name="Warnings">Why the value in the file of a setting is not valid, by id.</param>
/// <param name="Banner">Why the file cannot be edited; empty when it can.</param>
/// <param name="Column">The column that has the cursor.</param>
/// <param name="CategoryIndex">The selected category, an index into <see cref="Categories"/>.</param>
/// <param name="Cursor">The selected row of the settings column, an index into <see cref="Rows"/>.</param>
/// <param name="IsCollapsed">The screen is too narrow for two columns: one list with category headings.</param>
/// <param name="Filter">The filter text; empty for none.</param>
/// <param name="IsEditingFilter">The filter box takes typed text.</param>
/// <param name="Choice">The choice whose options are listed, null for none.</param>
/// <param name="Input">The text being typed into a setting, null for none.</param>
/// <param name="Removal">The folder waiting for the answer to the removal question, null for none.</param>
public sealed record SettingsState(
    IReadOnlyList<SettingDefinition> Definitions,
    ImmutableDictionary<string, SettingValue> Values,
    ImmutableDictionary<string, string> Warnings,
    string Banner,
    SettingsColumn Column = SettingsColumn.Categories,
    int CategoryIndex = 0,
    int Cursor = 0,
    bool IsCollapsed = false,
    string Filter = "",
    bool IsEditingFilter = false,
    ChoiceEdit? Choice = null,
    SettingsInput? Input = null,
    PendingRemoval? Removal = null)
{
    /// <summary>The categories that have at least one setting, in display order.</summary>
    public IReadOnlyList<SettingCategory> Categories =>
        [.. Definitions.Select(definition => definition.Category).Distinct().Order()];

    /// <summary>The file cannot be edited (the banner says why).</summary>
    public bool IsReadOnly => Banner.Length > 0;

    /// <summary>A filter is narrowing the list.</summary>
    public bool IsFiltering => Filter.Length > 0;

    /// <summary>The settings column lists the settings of every category (a filter or the collapsed layout).</summary>
    public bool ListsAllCategories => IsCollapsed || IsFiltering;

    /// <summary>Something is being typed (a number, a folder or the filter): the keymap is in Insert mode.</summary>
    public bool IsTyping => Input is not null || IsEditingFilter;

    /// <summary>The rows of the settings column.</summary>
    public IReadOnlyList<SettingRow> Rows
    {
        get
        {
            var rows = new List<SettingRow>();
            foreach (var definition in ScopeDefinitions())
            {
                AddRows(rows, definition);
            }

            return rows;
        }
    }

    /// <summary>The row under the cursor, null when the column is empty.</summary>
    public SettingRow? Selected
    {
        get
        {
            var rows = Rows;
            return rows.Count == 0 ? null : rows[Math.Clamp(Cursor, 0, rows.Count - 1)];
        }
    }

    /// <summary>The selected category, null when nothing is on offer.</summary>
    public SettingCategory? CurrentCategory
    {
        get
        {
            var categories = Categories;
            return categories.Count == 0 ? null : categories[Math.Clamp(CategoryIndex, 0, categories.Count - 1)];
        }
    }

    /// <summary>The index of the cursor in the column that has it.</summary>
    public int ActiveIndex => Column == SettingsColumn.Categories && !ListsAllCategories ? CategoryIndex : Cursor;

    /// <summary>The number of rows in the column that has the cursor.</summary>
    public int ActiveCount => Column == SettingsColumn.Categories && !ListsAllCategories
        ? Categories.Count
        : Rows.Count;

    /// <summary>Builds the state for a freshly read file.</summary>
    /// <param name="definitions">The settings on offer.</param>
    /// <param name="load">The values read from the file.</param>
    public static SettingsState Create(IReadOnlyList<SettingDefinition> definitions, SettingsLoad load) =>
        new(definitions, load.Values, load.Warnings, load.Banner);

    /// <summary>The value of a setting; its default shape when the file read gave none.</summary>
    /// <param name="definition">The setting.</param>
    public SettingValue ValueOf(SettingDefinition definition)
    {
        if (Values.TryGetValue(definition.Id, out var value))
        {
            return value;
        }

        var choice = definition.Choices.Count > 0 ? definition.Choices[0] : string.Empty;

        return new SettingValue(false, choice, definition.Minimum, [], IsDefault: true);
    }

    /// <summary>Moves the cursor of the column that has it to a row (clamped to the column).</summary>
    /// <param name="target">The wanted row.</param>
    public SettingsState MoveTo(int target)
    {
        var count = ActiveCount;
        var clamped = count == 0 ? 0 : Math.Clamp(target, 0, count - 1);
        if (Column == SettingsColumn.Categories && !ListsAllCategories)
        {
            return this with { CategoryIndex = clamped, Cursor = 0 };
        }

        return this with { Cursor = Choice is null ? clamped : WithinOptions(clamped) };
    }

    /// <summary>The state with the cursor back inside the rows after they changed.</summary>
    public SettingsState Clamped()
    {
        var rows = Rows.Count;
        var categories = Categories.Count;
        var cursor = rows == 0 ? 0 : Math.Clamp(Cursor, 0, rows - 1);
        var category = categories == 0 ? 0 : Math.Clamp(CategoryIndex, 0, categories - 1);

        return this with { Cursor = cursor, CategoryIndex = category };
    }

    /// <summary>
    /// Switches between the two-column and the collapsed layout when the screen width changes; the selected setting
    /// stays selected.
    /// </summary>
    /// <param name="collapsed">The screen is too narrow for two columns.</param>
    public SettingsState WithLayout(bool collapsed)
    {
        if (collapsed == IsCollapsed)
        {
            return this;
        }

        var selected = Selected;
        var changed = this with { IsCollapsed = collapsed, Column = SettingsColumn.Settings };

        if (selected is null)
        {
            return changed.Clamped();
        }

        return changed.Reveal(selected.Definition, selected.Kind, selected.Index);
    }

    /// <summary>Puts the cursor on a setting, or one of its folders or options, switching category when needed.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="kind">The kind of row to land on.</param>
    /// <param name="index">The folder or option number for an item or option row.</param>
    public SettingsState Reveal(
        SettingDefinition definition,
        SettingRowKind kind = SettingRowKind.Setting,
        int index = 0)
    {
        var categories = Categories;
        var categoryIndex = Math.Max(0, categories.ToList().IndexOf(definition.Category));
        var moved = this with { CategoryIndex = categoryIndex, Column = SettingsColumn.Settings };
        var rows = moved.Rows.ToList();
        var found = rows.FindIndex(
            row => row.Definition.Id == definition.Id && row.Kind == kind && row.Index == index);
        if (found < 0)
        {
            found = rows.FindIndex(row => row.Definition.Id == definition.Id);
        }

        return moved with { Cursor = Math.Max(0, found) };
    }

    /// <summary>Replaces the values after a write or a reload and drops anything half-edited.</summary>
    /// <param name="load">The values now in the file.</param>
    public SettingsState WithLoad(SettingsLoad load) =>
        (this with
        {
            Values = load.Values,
            Warnings = load.Warnings,
            Banner = load.Banner,
            Choice = null,
            Input = null,
            Removal = null,
        }).Clamped();

    /// <summary>The state with other text in the number or folder box.</summary>
    /// <param name="text">The text now typed.</param>
    public SettingsState WithInputText(string text) =>
        Input is null ? this : this with { Input = Input with { Text = text } };

    /// <summary>The state with other text in the filter box; the list starts again at its top.</summary>
    /// <param name="text">The filter now typed.</param>
    public SettingsState WithFilterText(string text) =>
        this with { Filter = text, Cursor = 0, Column = SettingsColumn.Settings };

    // While a choice is open the cursor stays among its options.
    private int WithinOptions(int target)
    {
        var rows = Rows.ToList();
        var first = rows.FindIndex(row => row.Kind == SettingRowKind.Option);
        var last = rows.FindLastIndex(row => row.Kind == SettingRowKind.Option);

        return first < 0 ? target : Math.Clamp(target, first, last);
    }

    private IEnumerable<SettingDefinition> ScopeDefinitions()
    {
        if (IsFiltering)
        {
            return Definitions.Where(Matches).OrderBy(definition => definition.Category);
        }

        if (IsCollapsed)
        {
            return Definitions.OrderBy(definition => definition.Category);
        }

        var category = CurrentCategory;

        return Definitions.Where(definition => definition.Category == category);
    }

    private bool Matches(SettingDefinition definition) =>
        definition.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
        || definition.Help.Contains(Filter, StringComparison.OrdinalIgnoreCase);

    private void AddRows(List<SettingRow> rows, SettingDefinition definition)
    {
        rows.Add(new SettingRow(definition, SettingRowKind.Setting, 0));
        if (IsFiltering)
        {
            return;
        }

        if (definition.Control == SettingControl.PathList)
        {
            var paths = ValueOf(definition).Paths;
            for (var index = 0; index < paths.Length; index++)
            {
                rows.Add(new SettingRow(definition, SettingRowKind.Item, index));
            }

            if (Input is { Kind: SettingsInputKind.PathAdd } add && add.SettingId == definition.Id)
            {
                rows.Add(new SettingRow(definition, SettingRowKind.NewItem, paths.Length));
            }
        }
        else if (Choice is { } choice && choice.SettingId == definition.Id)
        {
            for (var index = 0; index < definition.Choices.Count; index++)
            {
                rows.Add(new SettingRow(definition, SettingRowKind.Option, index));
            }
        }
    }
}
