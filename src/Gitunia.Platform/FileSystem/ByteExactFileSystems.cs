namespace Gitunia.Platform.FileSystem;

/// <summary>Picks the implementation for the running OS.</summary>
public static class ByteExactFileSystems
{
    /// <summary>True where a name that is not valid UTF-8 can be handled by its exact bytes (Linux); the same answer as
    /// <see cref="IByteExactFileSystem.IsSupported"/> of <see cref="ForCurrentOs"/>.</summary>
    public static bool IsSupportedHere => OperatingSystem.IsLinux();

    public static IByteExactFileSystem ForCurrentOs() =>
        IsSupportedHere ? new LinuxByteExactFileSystem() : new UnsupportedByteExactFileSystem();
}
