using System.Runtime.InteropServices;

namespace Gitunia.Platform.FileSystem;

/// <summary>Picks the implementation for the running OS.</summary>
public static class ByteExactFileSystems
{
    public static IByteExactFileSystem ForCurrentOs() =>
        OperatingSystem.IsLinux() ? new LinuxByteExactFileSystem() : new UnsupportedByteExactFileSystem();
}

/// <summary>Windows, macOS and any other OS: explicitly unsupported (names there are always valid text).</summary>
public sealed class UnsupportedByteExactFileSystem : IByteExactFileSystem
{
    public bool IsSupported => false;

    public void DeleteFile(byte[] path) => throw new NotSupportedException(Message);

    public bool TryRemoveEmptyDirectory(byte[] path) => throw new NotSupportedException(Message);

    private const string Message = "Deleting by raw path bytes is only supported on Linux.";
}

/// <summary>Linux: <c>unlink</c> and <c>rmdir</c> from libc, which take the bytes as they are.</summary>
public sealed class LinuxByteExactFileSystem : IByteExactFileSystem
{
    private const int NoSuchEntry = 2;
    private const int NotEmpty = 39;
    private const int Exists = 17;

    public bool IsSupported => true;

    public void DeleteFile(byte[] path)
    {
        if (Unlink(Terminate(path)) != 0)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error != NoSuchEntry)
            {
                throw new IOException($"Could not delete a file (errno {error}).");
            }
        }
    }

    public bool TryRemoveEmptyDirectory(byte[] path)
    {
        if (Rmdir(Terminate(path)) == 0)
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        return error is NoSuchEntry or NotEmpty or Exists
            ? false
            : throw new IOException($"Could not remove a directory (errno {error}).");
    }

    private static byte[] Terminate(byte[] path)
    {
        var terminated = new byte[path.Length + 1];
        path.CopyTo(terminated, 0);
        return terminated;
    }

    [DllImport("libc", EntryPoint = "unlink", SetLastError = true)]
    private static extern int Unlink(byte[] path);

    [DllImport("libc", EntryPoint = "rmdir", SetLastError = true)]
    private static extern int Rmdir(byte[] path);
}
