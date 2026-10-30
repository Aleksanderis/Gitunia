namespace Gitunia.Core.Keymap.Models;

/// <summary>
/// How much of the command set the lists that teach keys show (#839). Each level includes the ones below it; an
/// action above the chosen level is hidden from help, palette, which-key, actions menu and status hints, never
/// disabled.
/// </summary>
public enum CommandLevel
{
    /// <summary>The daily commands only.</summary>
    Everyday,

    /// <summary>Everyday plus the less frequent commands (reflog, reset, rebase, cherry-pick, remote management).</summary>
    Advanced,

    /// <summary>Everything, including rare or risky commands.</summary>
    AdvancedPlus,
}
