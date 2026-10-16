using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Gitunia.Platform.Files;

/// <summary>
/// Reads a file whose name is given as raw bytes. On Linux and macOS a file name is any byte string, and one that is not
/// valid UTF-8 cannot be written as a .NET string path, so the file is opened through <c>open(2)</c> with the bytes as they
/// are. Windows names are UTF-16 and cannot hold such bytes, so nothing is read there.
/// </summary>
public static partial class RawPathFileReader
{
    // Open flags: read-only, and no blocking on a FIFO (a regular file is unaffected). The value differs per OS.
    private const int ReadOnlyLinux = 0x800;
    private const int ReadOnlyMac = 0x4;
    private const int MaximumBytes = 4 * 1024 * 1024;
    private const int InvalidDescriptor = -1;

    /// <summary>The whole file, or null when it cannot be read (missing, a directory, no access, or an OS without such names).</summary>
    /// <param name="path">The absolute path as bytes, without a terminator.</param>
    public static byte[]? TryReadAll(ReadOnlySpan<byte> path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return null;
        }

        var terminated = new byte[path.Length + 1];
        path.CopyTo(terminated);
        var descriptor = Open(terminated, OperatingSystem.IsLinux() ? ReadOnlyLinux : ReadOnlyMac);
        if (descriptor == InvalidDescriptor)
        {
            return null;
        }

        try
        {
            using var handle = new SafeFileHandle(descriptor, ownsHandle: true);
            using var stream = new FileStream(handle, FileAccess.Read, 1, false);
            var buffer = new byte[MaximumBytes];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = stream.Read(buffer, length, buffer.Length - length);
                if (read == 0)
                {
                    break;
                }

                length += read;
            }

            return buffer[..length];
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    [LibraryImport("libc", EntryPoint = "open")]
    private static partial int Open(byte[] path, int flags);
}
