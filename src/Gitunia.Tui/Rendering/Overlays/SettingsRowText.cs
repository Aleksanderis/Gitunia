using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>The words of one row of the settings column: the label on the left and the control on the right.</summary>
/// <param name="Marker">A one-character mark before the label, <c>!</c> for an invalid value in the file.</param>
/// <param name="Left">The label.</param>
/// <param name="Right">The control, empty for none.</param>
public sealed record SettingsRowText(string Marker, string Left, string Right)
{
    /// <summary>The mark before the label of a setting whose value in the file is invalid.</summary>
    public const string Warning = "!";
    private const string NoMarker = " ";
    private const string ChoiceArrow = "▾";
    private const string AsciiChoiceArrow = "v";
    private const string Cursor = "_";
    private const string ItemIndent = "  ";
    private const string RemoveMarker = "[x]";
    private const string CheckedBox = "[x]";
    private const string EmptyBox = "[ ]";

    /// <summary>The <c>[+]</c> marker at the end of a path list's row.</summary>
    public const string AddMarker = "[+]";

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
            SettingRowKind.Option => new SettingsRowText(NoMarker, OptionText(row, value, glyphs), string.Empty),
            _ => SettingText(state, definition, value, glyphs),
        };
    }

    /// <summary>The control of a row cut to fit the settings column (at most half of it).</summary>
    /// <param name="right">The control text.</param>
    /// <param name="settingsWidth">Inner width of the settings column.</param>
    /// <param name="ellipsis">The cut mark.</param>
    public static string FitRight(string right, int settingsWidth, string ellipsis) =>
        TextWidth.Fit(right, Math.Max(0, settingsWidth / 2), ellipsis);

    private static SettingsRowText NewItemText(SettingsState state) =>
        new(NoMarker, $"{ItemIndent}> {TypedText(state)}{Cursor}", string.Empty);

    private static SettingsRowText SettingText(
        SettingsState state,
        SettingDefinition definition,
        SettingValue value,
        GraphGlyphSet glyphs)
    {
        var marker = state.Warnings.ContainsKey(definition.Id) ? Warning : NoMarker;
        var name = state.IsFiltering
            ? $"{SettingsRegistry.TitleOf(definition.Category)}: {definition.Name}"
            : definition.Name;

        return new SettingsRowText(marker, name, ControlText(state, definition, value, glyphs));
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
                return state.Input is { Kind: SettingsInputKind.Number } input && input.SettingId == definition.Id
                    ? $"[{input.Text}{Cursor}]"
                    : $"[{value.Number}]";
            case SettingControl.PathList:
                return $"{PathSummary(value)} {AddMarker}";
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

        return value.Paths.Length == 1 ? "1 folder" : $"{value.Paths.Length} folders";
    }

    private static SettingsRowText ItemText(SettingsState state, SettingRow row, SettingValue value)
    {
        var editing = state.Input is { Kind: SettingsInputKind.PathEdit } input
            && input.SettingId == row.Definition.Id
            && input.ItemIndex == row.Index;
        var left = editing ? $"{ItemIndent}> {TypedText(state)}{Cursor}" : $"{ItemIndent}{value.Paths[row.Index]}";

        return new SettingsRowText(NoMarker, left, RemoveMarker);
    }

    private static string OptionText(SettingRow row, SettingValue value, GraphGlyphSet glyphs)
    {
        var option = row.Definition.Choices[row.Index];
        var isCurrent = string.Equals(option, value.Choice, StringComparison.OrdinalIgnoreCase);
        var radio = glyphs.IsAscii ? (isCurrent ? "(*)" : "( )") : (isCurrent ? "●" : "○");

        return $"{ItemIndent}{radio} {option}";
    }

    private static string TypedText(SettingsState state) => state.Input?.Text ?? string.Empty;
}
