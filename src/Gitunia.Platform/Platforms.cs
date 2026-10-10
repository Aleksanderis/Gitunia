namespace Gitunia.Platform;

public sealed class LinuxPlatform(Func<string, string?>? getEnv = null) : PlatformBase(getEnv)
{
    public override string ConfigDir => Path.Combine(Env("XDG_CONFIG_HOME") ?? Path.Combine(Home, ".config"), PlatformInfo.AppDirName);

    public override string StateDir => Path.Combine(Env("XDG_STATE_HOME") ?? Path.Combine(Home, ".local", "state"), PlatformInfo.AppDirName);
}

public sealed class MacPlatform : PlatformBase
{
    public override string ConfigDir => Path.Combine(Home, "Library", "Application Support", PlatformInfo.AppDirName);

    public override string StateDir => Path.Combine(Home, "Library", "Application Support", PlatformInfo.AppDirName, "state");
}

public sealed class WindowsPlatform : PlatformBase
{
    private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public override string ConfigDir => Path.Combine(AppData, PlatformInfo.AppDirName);

    public override string StateDir => Path.Combine(LocalAppData, PlatformInfo.AppDirName);
}

/// <summary>Explicit "unsupported" path for any other OS: keeps everything under a dot-directory under the home dir.</summary>
public sealed class UnsupportedPlatform : PlatformBase
{
    public override string ConfigDir => Path.Combine(Home, "." + PlatformInfo.AppDirName);

    public override string StateDir => Path.Combine(Home, "." + PlatformInfo.AppDirName, "state");
}
