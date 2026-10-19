namespace Gitunia.Core.Settings;

/// <summary>What the reducer needs from the machine, injected so it stays pure.</summary>
/// <param name="HomeDirectory">What a leading <c>~</c> in a folder stands for.</param>
/// <param name="DirectoryExists">Whether a folder exists.</param>
public sealed record SettingsEnvironment(string HomeDirectory, Func<string, bool> DirectoryExists);
