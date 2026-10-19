namespace Gitunia.Core.Settings.Models;

/// <summary>What the text being typed in the settings screen is for.</summary>
public enum SettingsInputKind
{
    /// <summary>A number setting.</summary>
    Number,

    /// <summary>A new folder of a path list.</summary>
    PathAdd,

    /// <summary>A change of a folder of a path list.</summary>
    PathEdit,
}
