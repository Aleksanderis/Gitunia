using System.Runtime.InteropServices;

namespace Gitunia.Platform.Terminal;

/// <summary>
/// Windows: <see cref="Wake"/> writes one key press into the console input buffer through a handle of its own, which
/// satisfies a read that waits for input and is still there for a read that has not started yet; <see cref="Settle"/>
/// empties that buffer. Linux and macOS: nothing, Terminal.Gui there polls without blocking and sleeps with a
/// cancellable delay. Any other OS: nothing.
/// </summary>
public sealed class TerminalInputWaker : ITerminalInputWaker
{
    private const string InputDevice = "CONIN$";
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareReadWrite = 3;
    private const uint OpenExisting = 3;
    private const ushort KeyEvent = 1;
    private const ushort Space = ' ';
    private static readonly nint InvalidHandle = new(-1);

    public void Wake()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = OpenInput();
        if (handle == InvalidHandle)
        {
            return;
        }

        try
        {
            var record = new InputRecord { EventType = KeyEvent, KeyDown = 1, RepeatCount = 1, UnicodeChar = Space };
            _ = WriteConsoleInput(handle, [record], 1, out _);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    public void Settle()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = OpenInput();
        if (handle == InvalidHandle)
        {
            return;
        }

        try
        {
            _ = FlushConsoleInputBuffer(handle);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    private static nint OpenInput() => CreateFile(
        InputDevice,
        GenericRead | GenericWrite,
        ShareReadWrite,
        nint.Zero,
        OpenExisting,
        0,
        nint.Zero);

    [DllImport("kernel32", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode)]
    private static extern nint CreateFile(
        string name,
        uint access,
        uint share,
        nint security,
        uint disposition,
        uint attributes,
        nint template);

    [DllImport("kernel32", EntryPoint = "WriteConsoleInputW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteConsoleInput(nint handle, InputRecord[] records, uint length, out uint written);

    [DllImport("kernel32", EntryPoint = "FlushConsoleInputBuffer")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushConsoleInputBuffer(nint handle);

    [DllImport("kernel32", EntryPoint = "CloseHandle")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    // INPUT_RECORD with its KEY_EVENT_RECORD member: 20 bytes, the union starts at offset 4.
    [StructLayout(LayoutKind.Sequential)]
    private struct InputRecord
    {
        public ushort EventType;
        public ushort Padding;
        public int KeyDown;
        public ushort RepeatCount;
        public ushort VirtualKeyCode;
        public ushort VirtualScanCode;
        public ushort UnicodeChar;
        public uint ControlKeyState;
    }
}
