using Gitunia.Core.Editing;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>The words of one row of the settings column: the label on the left and the control on the right.</summary>
/// <param name="Marker">A one-character mark before the label, <c>!</c> for an invalid value in the file.</param>
/// <param name="Left">The label.</param>
/// <param name="Right">The control, empty for none.</param>
/// <param name="LeftField">The label as an editable field (a folder being typed), drawn with a cursor cell; null for plain text. <paramref name="Left"/> is then its text with the cursor cell as a blank at the end.</param>
/// <param name="RightField">The control as an editable field (a number being typed), drawn with a cursor cell; null for plain text. <paramref name="Right"/> is then its text with the cursor cell as a blank at the end.</param>
public sealed record SettingsRowText(
    string Marker,
    string Left,
    string Right,
    CaretField? LeftField = null,
    CaretField? RightField = null)
{
    /// <summary>The mark before the label of a setting whose value in the file is invalid.</summary>
    public const string Warning = "!";
    private const string NoMarker = " ";
    private const string ChoiceArrow = "▾";
    private const string AsciiChoiceArrow = "v";
    private const string ItemIndent = "  ";
    private static readonly string RemoveMarker = ButtonLabel.Bracketed("x");
    private static readonly string CheckedBox = ButtonLabel.Bracketed("x");
    private static readonly string EmptyBox = ButtonLabel.Bracketed(" ");

    /// <summary>The <c>[+]</c> marker at the end of a path list's row.</summary>
    public static readonly string AddMarker = ButtonLabel.Bracketed("+");

    /// <summary>The words of a row.</summary>
    /// <param name="state">The settings state (values, what is being typed).</param>
    /// <param name="row">The row.</param>
    /// <param name="glyphs">The character set (ASCII or box drawing).</param>
    public static SettingsRowText Of(SettingsState state, SettingRow row, GraphGlyphSet glyphs)
    {
        var definition = row.Definition;
        var value = state.ValueOf(definition);

        return row.Kind switch
        {
            SettingRowKind.Item => ItemText(state, row, value),
            SettingRowKind.NewItem => NewItemText(state),
            SettingRowKind.Binding => BindingText(state.Reference[row.Index]),
            SettingRowKind.Option => new SettingsRowText(NoMarker, OptionText(row, value, glyphs), string.Empty),
            _ => SettingText(state, definition, value, glyphs),
        };
    }

    private static SettingsRowText BindingText(KeyReferenceEntry entry) =>
        new(NoMarker, $"{ItemIndent}{entry.Title}", entry.Keys);

    private static SettingsRowText NewItemText(SettingsState state) => TypedItem(state);

    private static SettingsRowText SettingText(
        SettingsState state,
        SettingDefinition definition,
        SettingValue value,
        GraphGlyphSet glyphs)
    {
        var marker = state.Warnings.ContainsKey(definition.Id) ? Warning : NoMarker;
        var name = definition.Name;
        if (state.IsFiltering)
        {
            name = $"{SettingsRegistry.TitleOf(definition.Category)}: {definition.Name}";
        }

        var control = ControlText(state, definition, value, glyphs);
        if (definition.Control == SettingControl.Number
            && state.Input is { Kind: SettingsInputKind.Number } input
            && input.SettingId == definition.Id)
        {
            return new SettingsRowText(marker, name, control, RightField: CaretField.Of("[", input.Field, "]"));
        }

        return new SettingsRowText(marker, name, control);
    }

    private static string ControlText(
        SettingsState state,
        SettingDefinition definition,
        SettingValue value,
        GraphGlyphSet glyphs)
    {
        switch (definition.Control)
        {
            case SettingControl.Toggle:
                return value.Flag ? CheckedBox : EmptyBox;
            case SettingControl.Choice:
                return $"[{value.Choice} {(glyphs.IsAscii ? AsciiChoiceArrow : ChoiceArrow)}]";
            case SettingControl.Number:
                if (state.Input is { Kind: SettingsInputKind.Number } input && input.SettingId == definition.Id)
                {
                    return $"[{Shown(input.Text, input.Field.Caret)}]";
                }

                return $"[{value.Number}]";
            case SettingControl.PathList:
                return $"{PathSummary(value)} {AddMarker}";
            case SettingControl.KeyReference:
                return CountText.Plural(state.Reference.Count, "key");
            default:
                return string.Empty;
        }
    }

    private static string PathSummary(SettingValue value)
    {
        if (value.Paths.Length == 0)
        {
            return value.IsDefault ? "default folders" : "none";
        }

        return CountText.Plural(value.Paths.Length, "folder");
    }

    private static SettingsRowText ItemText(SettingsState state, SettingRow row, SettingValue value)
    {
        var editing = state.Input is { Kind: SettingsInputKind.PathEdit } input
            && input.SettingId == row.Definition.Id
            && input.ItemIndex == row.Index;
        if (editing)
        {
            return TypedItem(state, RemoveMarker);
        }

        return new SettingsRowText(NoMarker, $"{ItemIndent}{value.Paths[row.Index]}", RemoveMarker);
    }

    private static string OptionText(SettingRow row, SettingValue value, GraphGlyphSet glyphs)
    {
        var option = row.Definition.Choices[row.Index];
        var isCurrent = string.Equals(option, value.Choice, StringComparison.OrdinalIgnoreCase);
        var radio = isCurrent ? "●" : "○";
        if (glyphs.IsAscii)
        {
            radio = isCurrent ? "(*)" : "( )";
        }

        return $"{ItemIndent}{radio} {option}";
    }

    // The folder box: the typed text as a field, the caret drawn as a cursor cell.
    private static SettingsRowText TypedItem(SettingsState state, string right = "")
    {
        var typed = state.Input?.Field ?? new TextField();
        var field = CaretField.Of($"{ItemIndent}> ", typed);

        return new SettingsRowText(NoMarker, $"{ItemIndent}> {Shown(typed.Text, typed.Caret)}", right, field);
    }

    // The text as the field takes room for it: the cursor cell at the end of the text is a blank.
    private static string Shown(string text, int caret) =>
        PromptCaret.Clamp(text, caret) == text.Length ? $"{text}{PromptCaret.EndCell}" : text;
}
