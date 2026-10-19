using System.Collections.Immutable;
using System.Globalization;
using Gitunia.Core.Config;

namespace Gitunia.Core.Settings.Models;

/// <summary>
/// The current value of a setting. Which member counts depends on the control of the setting: the flag of a toggle,
/// the choice text, the number or the list of folders.
/// </summary>
/// <param name="Flag">The value of a toggle.</param>
/// <param name="Choice">The selected option of a choice.</param>
/// <param name="Number">The value of a number.</param>
/// <param name="Paths">The folders of a path list, as written in the file.</param>
/// <param name="IsDefault">The file does not set the key, so the default applies.</param>
public sealed record SettingValue(
    bool Flag,
    string Choice,
    int Number,
    ImmutableArray<string> Paths,
    bool IsDefault = false)
{
    /// <summary>A toggle value.</summary>
    public static SettingValue OfFlag(bool flag) => new(flag, string.Empty, 0, []);

    /// <summary>A choice value.</summary>
    public static SettingValue OfChoice(string choice) => new(false, choice, 0, []);

    /// <summary>A number value.</summary>
    public static SettingValue OfNumber(int number) => new(false, string.Empty, number, []);

    /// <summary>A path list value.</summary>
    public static SettingValue OfPaths(IEnumerable<string> paths, bool isDefault = false) =>
        new(false, string.Empty, 0, [.. paths], isDefault);

    /// <summary>The JSON text written to the file for a setting with this control.</summary>
    /// <param name="control">The control of the setting the value belongs to.</param>
    public string ToJson(SettingControl control) => control switch
    {
        SettingControl.Toggle => Flag ? "true" : "false",
        SettingControl.Choice => ConfigTextEditor.StringValue(Choice),
        SettingControl.Number => Number.ToString(CultureInfo.InvariantCulture),
        SettingControl.PathList => ConfigTextEditor.StringListValue(Paths),
        _ => "null",
    };
}
