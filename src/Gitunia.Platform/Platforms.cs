namespace Gitunia.Platform;

public sealed class LinuxPlatform(Func<string, string?>? getEnvironmentVariable = null) : PlatformBase(getEnvironmentVariable)
{
    public override string ConfigDirectory => Path.Combine(ReadEnvironment("XDG_CONFIG_HOME") ?? Path.Combine(Home, ".config"), PlatformInfo.AppDirectoryName);

    public override string StateDirectory => Path.Combine(ReadEnvironment("XDG_STATE_HOME") ?? Path.Combine(Home, ".local", "state"), PlatformInfo.AppDirectoryName);
}

public sealed class MacPlatform : PlatformBase
{
    public override string ConfigDirectory => Path.Combine(Home, "Library", "Application Support", PlatformInfo.AppDirectoryName);

    public override string StateDirectory => Path.Combine(Home, "Library", "Application Support", PlatformInfo.AppDirectoryName, "state");
}

public sealed class WindowsPlatform : PlatformBase
{
    private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public override string ConfigDirectory => Path.Combine(AppData, PlatformInfo.AppDirectoryName);

    public override string StateDirectory => Path.Combine(LocalAppData, PlatformInfo.AppDirectoryName);
}

/// <summary>Explicit "unsupported" path for any other OS: keeps everything under a dot-directory under the home dir.</summary>
public sealed class UnsupportedPlatform : PlatformBase
{
    public override string ConfigDirectory => Path.Combine(Home, "." + PlatformInfo.AppDirectoryName);

    public override string StateDirectory => Path.Combine(Home, "." + PlatformInfo.AppDirectoryName, "state");
}
