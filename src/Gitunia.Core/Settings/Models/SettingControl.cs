namespace Gitunia.Core.Settings.Models;

/// <summary>How a setting is shown and edited.</summary>
public enum SettingControl
{
    /// <summary>On or off; <c>Enter</c> or <c>x</c> flips it.</summary>
    Toggle,

    /// <summary>One of a few named options.</summary>
    Choice,

    /// <summary>A whole number inside a range.</summary>
    Number,

    /// <summary>A list of folders.</summary>
    PathList,

    /// <summary>A row that does something instead of holding a value (open the config file in the editor).</summary>
    Action,

    /// <summary>
    /// The read-only key reference: its rows are the bindings of the keymap, <c>Enter</c> shows one's config entry.
    /// </summary>
    KeyReference,
}
