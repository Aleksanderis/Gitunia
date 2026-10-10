using System.Runtime.InteropServices;

namespace Gitunia.Platform;

/// <summary>
/// Raw-mode terminal session (Unix: termios via libc). Windows would need
/// SetConsoleMode(ENABLE_VIRTUAL_TERMINAL_INPUT / ENABLE_VIRTUAL_TERMINAL_PROCESSING) on the stdin/stdout
/// handles plus WINDOW_BUFFER_SIZE_EVENT for resize; not implemented in the spike.
/// </summary>
public sealed unsafe partial class RawTerminal : IDisposable
{
    private readonly byte[] _saved = new byte[256];
    private bool _active;

    public static bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    [LibraryImport("libc", EntryPoint = "tcgetattr")]
    private static partial int TcGetAttr(int fd, byte* termios);

    [LibraryImport("libc", EntryPoint = "tcsetattr")]
    private static partial int TcSetAttr(int fd, int actions, byte* termios);

    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    private static partial void CfMakeRaw(byte* termios);

    /// <summary>
    /// Terminal size via ioctl(TIOCGWINSZ). Deliberately avoids Console.WindowWidth: touching it makes .NET
    /// initialise its own terminal handling, which resets termios (raw mode lost, echo back) and restores it
    /// again whenever a child process (git) exits.
    /// </summary>
    public static (int Width, int Height) GetSize()
    {
        var ws = new ushort[4];
        fixed (ushort* p = ws)
        {
            // TIOCGWINSZ: 0x5413 on Linux, 0x40087468 on macOS
            if (IoCtl(1, OperatingSystem.IsLinux() ? 0x5413UL : 0x40087468UL, p) == 0 && ws[0] > 0 && ws[1] > 0)
            {
                return (ws[1], ws[0]);
            }
        }

        return (80, 24);
    }

    [LibraryImport("libc", EntryPoint = "ioctl")]
    private static partial int IoCtl(int fd, ulong request, ushort* arg);

    public void Enter()
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("Raw terminal mode is not implemented for this OS.");
        }

        var work = new byte[256];
        fixed (byte* s = _saved)
        fixed (byte* w = work)
        {
            if (TcGetAttr(0, s) != 0)
            {
                throw new InvalidOperationException("stdin is not a terminal");
            }

            Buffer.MemoryCopy(s, w, 256, 256);
            CfMakeRaw(w);
            _ = TcSetAttr(0, 0, w);
        }

        _active = true;
        Write("\e[?1049h\e[?25l\e[?1000h\e[?1006h"u8); // alt screen, hide cursor, mouse (SGR)
    }

    public static void Write(ReadOnlySpan<byte> bytes)
    {
        using var o = Console.OpenStandardOutput();
        o.Write(bytes);
        o.Flush();
    }

    public void Dispose()
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        Write("\e[?1006l\e[?1000l\e[?25h\e[?1049l"u8);
        fixed (byte* s = _saved)
        {
            _ = TcSetAttr(0, 0, s);
        }
    }
}
