namespace Gitunia.Core.Settings.Models;

/// <summary>What the reducer needs from the machine, injected so it stays pure.</summary>
/// <param name="HomeDirectory">What a leading <c>~</c> in a folder stands for.</param>
/// <param name="DirectoryExists">Whether a folder exists.</param>
/// <param name="ThemeNames">The names of the theme files on offer; null for the built-in ones only.</param>
public sealed record SettingsEnvironment(
    string HomeDirectory,
    Func<string, bool> DirectoryExists,
    IReadOnlyCollection<string>? ThemeNames = null);
