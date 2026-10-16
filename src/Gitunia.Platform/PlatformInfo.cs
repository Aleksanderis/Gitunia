namespace Gitunia.Platform;

/// <summary>Resolves the <see cref="IPlatform"/> for the running OS.</summary>
public static class PlatformInfo
{
    public const string AppDirectoryName = "gitunia";

    public static IPlatform Current { get; } = new TerminalPlatform();
}
