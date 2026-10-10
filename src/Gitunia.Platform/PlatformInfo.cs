namespace Gitunia.Platform;

/// <summary>Resolves the <see cref="IPlatform"/> for the running OS.</summary>
public static class PlatformInfo
{
    public const string AppDirName = "gitunia";

    public static IPlatform Current { get; } = Create();

    private static IPlatform Create()
    {
        if (OperatingSystem.IsLinux())
        {
            return new LinuxPlatform();
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatform();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacPlatform();
        }

        return new UnsupportedPlatform();
    }
}
