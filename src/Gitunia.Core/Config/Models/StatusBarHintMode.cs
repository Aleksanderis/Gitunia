namespace Gitunia.Core.Config.Models;

/// <summary>How many key hints the status bar shows (the <c>hints.statusBar</c> option, #837).</summary>
public enum StatusBarHintMode
{
    /// <summary>The pane's own hints after help, commands and quit.</summary>
    Full,

    /// <summary>Only <c>? help  : commands  q quit</c>.</summary>
    Minimal,

    /// <summary>No key hints.</summary>
    Off,
}
