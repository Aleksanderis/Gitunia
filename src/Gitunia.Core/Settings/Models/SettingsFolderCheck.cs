namespace Gitunia.Core.Settings.Models;

/// <summary>A typed folder that passed the checks that need no disk and waits for the existence check.</summary>
/// <param name="Path">The folder with <c>~</c> expanded and no trailing separator.</param>
/// <param name="Typed">The folder as typed, for the message.</param>
/// <param name="Commit">The change to write when the folder exists.</param>
public sealed record SettingsFolderCheck(string Path, string Typed, SettingsCommit Commit);
