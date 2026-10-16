namespace Gitunia.Platform.FileSystem;

/// <summary>Picks the implementation for the running OS.</summary>
public static class ByteExactFileSystems
{
    public static IByteExactFileSystem ForCurrentOs() =>
        OperatingSystem.IsLinux() ? new LinuxByteExactFileSystem() : new UnsupportedByteExactFileSystem();
}
