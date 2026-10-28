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
            var before = Read(slave);

            MakeRaw(slave);
            Assert.NotEqual(before, Read(slave));

            modes.Restore();

            Assert.Equal(before, Read(slave));
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
