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
    private uint _savedInputMode;
    private uint _savedOutputMode;
    private uint _savedInputCodePage;
    private uint _savedOutputCodePage;
    private char _pendingHigh;
    private PosixSignalRegistration? _resizeRegistration;

    /// <summary>Raised when the terminal was resized: from the reader thread on Windows, from a signal callback on Unix.</summary>
    public event Action? Resized;

    public static bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    /// <summary>
    /// Terminal size. Deliberately avoids Console.WindowWidth on Unix: touching it makes .NET initialise its own
    /// terminal handling, which resets termios (raw mode lost, echo back) and restores it whenever a child process exits.
    /// </summary>
    public static TerminalSize GetSize()
    {
        if (OperatingSystem.IsWindows())
        {
            if (GetConsoleScreenBufferInfo(GetStdHandle(StdOutputHandle), out var info))
            {
                var width = info.Right - info.Left + 1;
                var height = info.Bottom - info.Top + 1;
                if (width > 0 && height > 0)
                {
                    return new TerminalSize(width, height);
                }
            }

            return new TerminalSize(80, 24);
        }

        var windowSize = new ushort[4];
        fixed (ushort* sizePointer = windowSize)
        {
            // TIOCGWINSZ: 0x5413 on Linux, 0x40087468 on macOS
            if (IoCtl(1, OperatingSystem.IsLinux() ? 0x5413UL : 0x40087468UL, sizePointer) == 0 && windowSize[0] > 0 && windowSize[1] > 0)
            {
                return new TerminalSize(windowSize[1], windowSize[0]);
            }
        }

        return new TerminalSize(80, 24);
    }

    public static void Write(ReadOnlySpan<byte> bytes)
    {
        if (OperatingSystem.IsWindows())
        {
            WriteWindows(bytes);
            return;
        }

        using var output = Console.OpenStandardOutput();
        output.Write(bytes);
        output.Flush();
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
            _resizeRegistration = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, _ => Resized?.Invoke());
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
        _resizeRegistration?.Dispose();
        Write("\e[?1006l\e[?1000l\e[?25h\e[?1049l"u8);
        if (OperatingSystem.IsWindows())
        {
            _ = SetConsoleMode(GetStdHandle(StdInputHandle), _savedInputMode);
            _ = SetConsoleMode(GetStdHandle(StdOutputHandle), _savedOutputMode);
            _ = SetConsoleCP(_savedInputCodePage);
            _ = SetConsoleOutputCP(_savedOutputCodePage);
        }
        else
        {
            fixed (byte* savedPointer = _saved)
            {
                _ = TcSetAttr(0, 0, savedPointer);
            }
        }
    }

    [LibraryImport("libc", EntryPoint = "tcgetattr")]
    private static partial int TcGetAttr(int fileDescriptor, byte* termios);

    [LibraryImport("libc", EntryPoint = "tcsetattr")]
    private static partial int TcSetAttr(int fileDescriptor, int actions, byte* termios);

    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    private static partial void CfMakeRaw(byte* termios);

    [LibraryImport("libc", EntryPoint = "ioctl")]
    private static partial int IoCtl(int fileDescriptor, ulong request, ushort* arg);

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
    private static partial bool SetConsoleCP(uint codePage);

    [LibraryImport("kernel32", EntryPoint = "SetConsoleOutputCP")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleOutputCP(uint codePage);

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
        var outputHandle = GetStdHandle(StdOutputHandle);
        fixed (byte* pointer = bytes)
        {
            uint offset = 0;
            while (offset < bytes.Length)
            {
                if (!WriteFile(outputHandle, pointer + offset, (uint)bytes.Length - offset, out var written, 0) || written == 0)
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
        fixed (byte* savedPointer = _saved)
        fixed (byte* workPointer = work)
        {
            if (TcGetAttr(0, savedPointer) != 0)
            {
                throw new InvalidOperationException("stdin is not a terminal");
            }

            Buffer.MemoryCopy(savedPointer, workPointer, 256, 256);
            CfMakeRaw(workPointer);
            _ = TcSetAttr(0, 0, workPointer);
        }
    }

    private int ReadUnix(byte[] buffer)
    {
        _unixStdin ??= new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(0, false), FileAccess.Read, 1, false);
        return _unixStdin.Read(buffer, 0, buffer.Length);
    }

    private void EnterWindows()
    {
        var inputHandle = GetStdHandle(StdInputHandle);
        var outputHandle = GetStdHandle(StdOutputHandle);
        if (!GetConsoleMode(inputHandle, out _savedInputMode) || !GetConsoleMode(outputHandle, out _savedOutputMode))
        {
            throw new InvalidOperationException("stdin/stdout is not a console");
        }

        _savedInputCodePage = GetConsoleCP();
        _savedOutputCodePage = GetConsoleOutputCP();

        // Input: no line buffering, echo, Ctrl-C processing or quick-edit selection; VT sequences + resize events on.
        var inMode = (_savedInputMode & ~(EnableLineInput | EnableEchoInput | EnableProcessedInput | EnableQuickEdit))
            | EnableExtendedFlags | EnableWindowInput | EnableVirtualTerminalInput;
        if (!SetConsoleMode(inputHandle, inMode))
        {
            throw new InvalidOperationException("Could not enable virtual terminal input (console too old?)");
        }

        var outMode = _savedOutputMode | EnableProcessedOutput | EnableVirtualTerminalProcessing | DisableNewlineAutoReturn;
        if (!SetConsoleMode(outputHandle, outMode))
        {
            _ = SetConsoleMode(inputHandle, _savedInputMode);
            throw new InvalidOperationException("Could not enable virtual terminal processing (console too old?)");
        }

        _ = SetConsoleCP(Utf8CodePage);
        _ = SetConsoleOutputCP(Utf8CodePage);
    }

    private int ReadWindows(byte[] buffer)
    {
        var inputHandle = GetStdHandle(StdInputHandle);
        while (true)
        {
            uint read;
            bool ok;
            fixed (byte* recordsPointer = _records)
            {
                ok = ReadConsoleInput(inputHandle, recordsPointer, 32, out read);
            }

            if (!ok)
            {
                return 0;
            }

            var byteCount = 0;
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
                    var character = (char)BitConverter.ToUInt16(_records, data + 10);
                    byteCount += EncodeCharacter(character, buffer.AsSpan(byteCount));
                }
            }

            if (byteCount > 0)
            {
                return byteCount;
            }
        }
    }

    private int EncodeCharacter(char character, Span<byte> destination)
    {
        if (character == 0 || destination.Length < 4)
        {
            return 0;
        }

        if (char.IsHighSurrogate(character))
        {
            _pendingHigh = character;
            return 0;
        }

        Span<char> chars = stackalloc char[2];
        var count = 0;
        if (char.IsLowSurrogate(character) && _pendingHigh != 0)
        {
            chars[count++] = _pendingHigh;
        }

        _pendingHigh = '\0';
        chars[count++] = character;
        return Encoding.UTF8.GetBytes(chars[..count], destination);
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
