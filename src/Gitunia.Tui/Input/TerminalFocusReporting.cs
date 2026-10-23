using Terminal.Gui.Drivers;

namespace Gitunia.Tui.Input;

/// <summary>
/// Asks the terminal to report when it gains or loses focus (DECSET 1004: <c>ESC [ I</c> in, <c>ESC [ O</c> out).
/// Terminal.Gui v2 neither enables the mode nor reports the sequences, so this registers them as persistent
/// expected responses on the driver's ANSI parser, which keeps them away from the key handling.
/// </summary>
public static class TerminalFocusReporting
{
    private const string Enable = "\x1b[?1004h";
    private const string Disable = "\x1b[?1004l";

    /// <summary>
    /// True where the driver's VT input stream carries the sequences (Linux, macOS, and Windows, where the driver turns
    /// on Virtual Terminal Input mode and parses ANSI like the Unix one; an injected focus-in was seen on Windows 11). Every other OS is
    /// unsupported: focus never triggers a refresh there.
    /// </summary>
    public static bool IsSupported =>
        OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    /// <summary>
    /// Starts reporting on <paramref name="driver"/>; <paramref name="gained"/> runs on the input thread when the
    /// terminal gains focus. Returns false when unsupported or the driver has no ANSI parser. Call <see cref="Stop"/>
    /// before the driver is disposed.
    /// </summary>
    public static bool Start(IDriver driver, Action gained)
    {
        if (!IsSupported || driver.GetInputProcessor().GetParser() is not { } parser)
        {
            return false;
        }

        parser.ExpectResponse("I", string.Empty, _ => gained(), static () => { }, true);
        // Focus-out needs no action; registering it only keeps the sequence from turning into key events.
        parser.ExpectResponse("O", string.Empty, static _ => { }, static () => { }, true);
        driver.WriteRaw(Enable);

        return true;
    }

    /// <summary>Puts the terminal back to not reporting focus.</summary>
    public static void Stop(IDriver driver)
    {
        if (IsSupported)
        {
            driver.WriteRaw(Disable);
        }
    }
}
