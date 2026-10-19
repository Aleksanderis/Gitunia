namespace Gitunia.Core.Settings.Models;

/// <summary>What a command did: the new state and, at most, one thing for the caller to carry out.</summary>
/// <param name="State">The state after the command.</param>
/// <param name="Commit">A change to write to the file.</param>
/// <param name="OpensEditor">The config file should be opened in the user's editor.</param>
/// <param name="Closes">The screen should close.</param>
/// <param name="Warning">Something to tell the user, empty for nothing.</param>
public sealed record SettingsOutcome(
    SettingsState State,
    SettingsCommit? Commit = null,
    bool OpensEditor = false,
    bool Closes = false,
    string Warning = "");
