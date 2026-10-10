using System.Runtime.InteropServices;
using System.Text;

namespace Gitunia.CustomRenderTui;

/// <summary>
/// Raw-mode terminal session with VT input/output.
/// Unix: termios via libc, size via ioctl, input from fd 0, resize via SIGWINCH.
/// Windows: SetConsoleMode with ENABLE_VIRTUAL_TERMINAL_INPUT / ENABLE_VIRTUAL_TERMINAL_PROCESSING, input via
/// ReadConsoleInputW (VT sequences arrive as key events' UnicodeChar; WINDOW_BUFFER_SIZE_EVENT raises <see cref="Resized"/>),
/// size via GetConsoleScreenBufferInfo, UTF-8 code pages. No .NET Console input/size APIs are touched on either OS.
/// </summary>
public sealed unsafe partial class RawTerminal : IDisposable
{
    private const int StdInputHandle = -10;
    private const int StdOutputHandle = -11;
    private const uint EnableProcessedInput = 0x0001;
    private const uint EnableLineInput = 0x0002;
    private const uint EnableEchoInput = 0x0004;
    private const uint EnableWindowInput = 0x0008;
    private const uint EnableQuickEdit = 0x0040;
    private const uint EnableExtendedFlags = 0x0080;
    private const uint EnableVirtualTerminalInput = 0x0200;
    private const uint EnableProcessedOutput = 0x0001;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const uint DisableNewlineAutoReturn = 0x0008;
    private const ushort KeyEvent = 0x0001;
    private const ushort WindowBufferSizeEvent = 0x0004;
    private const int InputRecordSize = 20;
    private const uint Utf8CodePage = 65001;

    private readonly byte[] _saved = new byte[256];
    private readonly byte[] _records = new byte[InputRecordSize * 32];
    private bool _active;
    private FileStream? _unixStdin;
    private uint _savedInMode;
    private uint _savedOutMode;
    private uint _savedInCp;
    private uint _savedOutCp;
    private char _pendingHigh;
    private PosixSignalRegistration? _winch;

    /// <summary>Raised when the terminal was resized: from the reader thread on Windows, from a signal callback on Unix.</summary>
    public event Action? Resized;

    public static bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    /// <summary>
    /// Terminal size. Deliberately avoids Console.WindowWidth on Unix: touching it makes .NET initialise its own
    /// terminal handling, which resets termios (raw mode lost, echo back) and restores it whenever a child process exits.
    /// </summary>
    public static (int Width, int Height) GetSize()
    {
        if (OperatingSystem.IsWindows())
        {
            if (GetConsoleScreenBufferInfo(GetStdHandle(StdOutputHandle), out var info))
            {
                var w = info.Right - info.Left + 1;
                var h = info.Bottom - info.Top + 1;
                if (w > 0 && h > 0)
                {
                    return (w, h);
                }
            }

            return (80, 24);
        }

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

    public static void Write(ReadOnlySpan<byte> bytes)
    {
        if (OperatingSystem.IsWindows())
        {
            WriteWindows(bytes);
            return;
        }

        using var o = Console.OpenStandardOutput();
        o.Write(bytes);
        o.Flush();
    }

    public void Enter()
    {
        if (OperatingSystem.IsWindows())
        {
            EnterWindows();
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            EnterUnix();
            _winch = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, _ => Resized?.Invoke());
        }
        else
        {
            throw new PlatformNotSupportedException("Raw terminal mode is not implemented for this OS.");
        }

        _active = true;
        Write("\e[?1049h\e[?25l\e[?1000h\e[?1006h"u8); // alt screen, hide cursor, mouse (SGR)
    }

    /// <summary>Blocks until input bytes (UTF-8, VT sequences) are available. Returns 0 at end of input.</summary>
    public int Read(byte[] buffer) => OperatingSystem.IsWindows() ? ReadWindows(buffer) : ReadUnix(buffer);

    public void Dispose()
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        _winch?.Dispose();
        Write("\e[?1006l\e[?1000l\e[?25h\e[?1049l"u8);
        if (OperatingSystem.IsWindows())
        {
            _ = SetConsoleMode(GetStdHandle(StdInputHandle), _savedInMode);
            _ = SetConsoleMode(GetStdHandle(StdOutputHandle), _savedOutMode);
            _ = SetConsoleCP(_savedInCp);
            _ = SetConsoleOutputCP(_savedOutCp);
        }
        else
        {
            fixed (byte* s = _saved)
            {
                _ = TcSetAttr(0, 0, s);
            }
        }
    }

    [LibraryImport("libc", EntryPoint = "tcgetattr")]
    private static partial int TcGetAttr(int fd, byte* termios);

    [LibraryImport("libc", EntryPoint = "tcsetattr")]
    private static partial int TcSetAttr(int fd, int actions, byte* termios);

    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    private static partial void CfMakeRaw(byte* termios);

    [LibraryImport("libc", EntryPoint = "ioctl")]
    private static partial int IoCtl(int fd, ulong request, ushort* arg);

    [LibraryImport("kernel32", EntryPoint = "GetStdHandle")]
    private static partial nint GetStdHandle(int which);

    [LibraryImport("kernel32", EntryPoint = "GetConsoleMode")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint handle, out uint mode);

    [LibraryImport("kernel32", EntryPoint = "SetConsoleMode")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint handle, uint mode);

    [LibraryImport("kernel32", EntryPoint = "GetConsoleCP")]
    private static partial uint GetConsoleCP();

    [LibraryImport("kernel32", EntryPoint = "GetConsoleOutputCP")]
    private static partial uint GetConsoleOutputCP();

    [LibraryImport("kernel32", EntryPoint = "SetConsoleCP")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleCP(uint cp);

    [LibraryImport("kernel32", EntryPoint = "SetConsoleOutputCP")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleOutputCP(uint cp);

    [LibraryImport("kernel32", EntryPoint = "GetConsoleScreenBufferInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleScreenBufferInfo(nint handle, out ConsoleScreenBufferInfo info);

    [LibraryImport("kernel32", EntryPoint = "ReadConsoleInputW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadConsoleInput(nint handle, byte* records, uint length, out uint read);

    [LibraryImport("kernel32", EntryPoint = "WriteFile")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WriteFile(nint handle, byte* buffer, uint count, out uint written, nint overlapped);

    private static void WriteWindows(ReadOnlySpan<byte> bytes)
    {
        var hOut = GetStdHandle(StdOutputHandle);
        fixed (byte* p = bytes)
        {
            uint offset = 0;
            while (offset < bytes.Length)
            {
                if (!WriteFile(hOut, p + offset, (uint)bytes.Length - offset, out var written, 0) || written == 0)
                {
                    return;
                }

                offset += written;
            }
        }
    }

    private void EnterUnix()
    {
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
    }

    private int ReadUnix(byte[] buffer)
    {
        _unixStdin ??= new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(0, false), FileAccess.Read, 1, false);
        return _unixStdin.Read(buffer, 0, buffer.Length);
    }

    private void EnterWindows()
    {
        var hIn = GetStdHandle(StdInputHandle);
        var hOut = GetStdHandle(StdOutputHandle);
        if (!GetConsoleMode(hIn, out _savedInMode) || !GetConsoleMode(hOut, out _savedOutMode))
        {
            throw new InvalidOperationException("stdin/stdout is not a console");
        }

        _savedInCp = GetConsoleCP();
        _savedOutCp = GetConsoleOutputCP();

        // Input: no line buffering, echo, Ctrl-C processing or quick-edit selection; VT sequences + resize events on.
        var inMode = (_savedInMode & ~(EnableLineInput | EnableEchoInput | EnableProcessedInput | EnableQuickEdit))
            | EnableExtendedFlags | EnableWindowInput | EnableVirtualTerminalInput;
        if (!SetConsoleMode(hIn, inMode))
        {
            throw new InvalidOperationException("Could not enable virtual terminal input (console too old?)");
        }

        var outMode = _savedOutMode | EnableProcessedOutput | EnableVirtualTerminalProcessing | DisableNewlineAutoReturn;
        if (!SetConsoleMode(hOut, outMode))
        {
            _ = SetConsoleMode(hIn, _savedInMode);
            throw new InvalidOperationException("Could not enable virtual terminal processing (console too old?)");
        }

        _ = SetConsoleCP(Utf8CodePage);
        _ = SetConsoleOutputCP(Utf8CodePage);
    }

    private int ReadWindows(byte[] buffer)
    {
        var hIn = GetStdHandle(StdInputHandle);
        while (true)
        {
            uint read;
            bool ok;
            fixed (byte* rec = _records)
            {
                ok = ReadConsoleInput(hIn, rec, 32, out read);
            }

            if (!ok)
            {
                return 0;
            }

            var n = 0;
            for (var i = 0; i < read; i++)
            {
                var type = BitConverter.ToUInt16(_records, i * InputRecordSize);
                var data = (i * InputRecordSize) + 4;
                if (type == WindowBufferSizeEvent)
                {
                    Resized?.Invoke();
                }
                else if (type == KeyEvent && BitConverter.ToInt32(_records, data) != 0)
                {
                    var ch = (char)BitConverter.ToUInt16(_records, data + 10);
                    n += EncodeChar(ch, buffer.AsSpan(n));
                }
            }

            if (n > 0)
            {
                return n;
            }
        }
    }

    private int EncodeChar(char ch, Span<byte> dest)
    {
        if (ch == 0 || dest.Length < 4)
        {
            return 0;
        }

        if (char.IsHighSurrogate(ch))
        {
            _pendingHigh = ch;
            return 0;
        }

        Span<char> chars = stackalloc char[2];
        var count = 0;
        if (char.IsLowSurrogate(ch) && _pendingHigh != 0)
        {
            chars[count++] = _pendingHigh;
        }

        _pendingHigh = '\0';
        chars[count++] = ch;
        return Encoding.UTF8.GetBytes(chars[..count], dest);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ConsoleScreenBufferInfo
    {
        public short SizeX;
        public short SizeY;
        public short CursorX;
        public short CursorY;
        public ushort Attributes;
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
        public short MaxX;
        public short MaxY;
    }
}
