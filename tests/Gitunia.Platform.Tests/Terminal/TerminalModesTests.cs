using System.Runtime.InteropServices;
using Gitunia.Platform.Terminal;
using Xunit;

namespace Gitunia.Platform.Tests.Terminal;

public class TerminalModesTests
{
    private const int BufferSize = 256;

    [Fact]
    public void GivenAPseudoTerminalPutIntoRawMode_WhenRestoring_ThenTheCapturedModesAreBack()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Needs a POSIX pseudo-terminal.");
        Assert.Equal(0, OpenPty(out var master, out var slave, nint.Zero, nint.Zero, nint.Zero));
        try
        {
            var modes = new TerminalModes(slave);
            Assert.True(modes.Capture());
            var before = Normalize(Read(slave));

            MakeRaw(slave);
            Assert.NotEqual(before, Normalize(Read(slave)));

            modes.Restore();

            Assert.Equal(before, Normalize(Read(slave)));
        }
        finally
        {
            _ = Close(slave);
            _ = Close(master);
        }
    }

    [Fact]
    public void GivenNoTerminal_WhenCapturing_ThenNothingIsCapturedAndRestoreDoesNothing()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Needs a POSIX descriptor.");
        const int invalidDescriptor = -1;
        var modes = new TerminalModes(invalidDescriptor);

        Assert.False(modes.Capture());
        modes.Restore();
    }

    private static byte[] Read(int descriptor)
    {
        var buffer = new byte[BufferSize];
        Assert.Equal(0, GetAttributes(descriptor, buffer));

        return buffer;
    }

    // macOS: struct termios has four 8-byte flag words, then c_cc. Byte 3 of c_lflag (offset 27) holds PENDIN
    // (0x20000000), a status flag the kernel sets itself after tcsetattr when input is pending; it is not a mode the
    // restore code controls. Linux has no such bit, so nothing is masked there and the comparison stays exact.
    private const int MacOsLocalFlagsHighByte = 27;
    private const byte MacOsPendingInputBit = 0x20;

    private static byte[] Normalize(byte[] termios)
    {
        var copy = (byte[])termios.Clone();
        if (OperatingSystem.IsMacOS())
        {
            copy[MacOsLocalFlagsHighByte] &= unchecked((byte)~MacOsPendingInputBit);
        }

        return copy;
    }

    private static void MakeRaw(int descriptor)
    {
        var buffer = Read(descriptor);
        MakeRawBuffer(buffer);
        Assert.Equal(0, SetAttributes(descriptor, 0, buffer));
    }

    [DllImport("libc", EntryPoint = "openpty")]
    private static extern int OpenPty(out int master, out int slave, nint name, nint termios, nint size);

    [DllImport("libc", EntryPoint = "close")]
    private static extern int Close(int descriptor);

    [DllImport("libc", EntryPoint = "tcgetattr")]
    private static extern int GetAttributes(int descriptor, byte[] termios);

    [DllImport("libc", EntryPoint = "tcsetattr")]
    private static extern int SetAttributes(int descriptor, int optionalActions, byte[] termios);

    [DllImport("libc", EntryPoint = "cfmakeraw")]
    private static extern void MakeRawBuffer(byte[] termios);
}
