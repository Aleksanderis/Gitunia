namespace Gitunia.Core.Settings.Models;

/// <summary>What a row of the settings column is.</summary>
public enum SettingRowKind
{
    /// <summary>The row of a setting.</summary>
    Setting,

    /// <summary>A folder of a path list, listed under its setting.</summary>
    Item,

    /// <summary>An option of the choice being edited, listed under its setting.</summary>
    Option,

    /// <summary>The line where a new folder is being typed.</summary>
    NewItem,

    /// <summary>A line of the key reference, listed under its setting; the index is into the reference.</summary>
    Binding,
}
