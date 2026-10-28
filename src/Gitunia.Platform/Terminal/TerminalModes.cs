using System.Runtime.InteropServices;

namespace Gitunia.Platform.Terminal;

/// <summary>
/// Linux and macOS: <c>tcgetattr</c> and <c>tcsetattr</c> on the input descriptor, with the structure kept as an opaque
/// buffer (its layout differs between the two systems, only its size bound matters). Windows: the console modes of the
/// input and output handles. Any other OS: explicitly unsupported, nothing is captured or restored.
/// </summary>
public sealed class TerminalModes(int inputDescriptor = TerminalModes.StandardInputDescriptor) : ITerminalModes
{
    /// <summary>The descriptor of the standard input.</summary>
    public const int StandardInputDescriptor = 0;

    private const int TermiosBufferSize = 256;
    private const int ApplyNow = 0;
    private const int StandardInputHandle = -10;
    private const int StandardOutputHandle = -11;

    private byte[]? _termios;
    private uint? _inputMode;
    private uint? _outputMode;

    public bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    public bool Capture()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var buffer = new byte[TermiosBufferSize];
            _termios = GetAttributes(inputDescriptor, buffer) == 0 ? buffer : null;

            return _termios is not null;
        }

        if (OperatingSystem.IsWindows())
        {
            _inputMode = ReadConsoleMode(StandardInputHandle);
            _outputMode = ReadConsoleMode(StandardOutputHandle);

            return _inputMode is not null || _outputMode is not null;
        }

        return false;
    }

    public void Restore()
    {
        if (_termios is { } termios)
        {
            _ = SetAttributes(inputDescriptor, ApplyNow, termios);
        }

        if (_inputMode is { } inputMode)
        {
            WriteConsoleMode(StandardInputHandle, inputMode);
        }

        if (_outputMode is { } outputMode)
        {
            WriteConsoleMode(StandardOutputHandle, outputMode);
        }
    }

    private static uint? ReadConsoleMode(int standardHandle) =>
        GetConsoleMode(GetStdHandle(standardHandle), out var mode) ? mode : null;

    private static void WriteConsoleMode(int standardHandle, uint mode) =>
        _ = SetConsoleMode(GetStdHandle(standardHandle), mode);

    [DllImport("libc", EntryPoint = "tcgetattr")]
    private static extern int GetAttributes(int descriptor, byte[] termios);

    [DllImport("libc", EntryPoint = "tcsetattr")]
    private static extern int SetAttributes(int descriptor, int optionalActions, byte[] termios);

    [DllImport("kernel32", EntryPoint = "GetStdHandle")]
    private static extern nint GetStdHandle(int standardHandle);

    [DllImport("kernel32", EntryPoint = "GetConsoleMode")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(nint handle, out uint mode);

    [DllImport("kernel32", EntryPoint = "SetConsoleMode")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(nint handle, uint mode);
}
