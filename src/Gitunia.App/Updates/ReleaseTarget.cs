using System.Runtime.InteropServices;
using Gitunia.Platform.Updates;

namespace Gitunia.App.Updates;

/// <summary>Which release archive and which binary name fit a machine. Pure.</summary>
public static class ReleaseTarget
{
    private const string WindowsBinary = "gitunia.exe";
    private const string UnixBinary = "gitunia";

    /// <summary>The ending of the archive's file name for this machine (<c>-linux-x64.tar.gz</c>), null when no release fits.</summary>
    public static string? ArchiveSuffix(HostSystem system, Architecture architecture)
    {
        var processor = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };
        if (processor is null)
        {
            return null;
        }

        return system switch
        {
            HostSystem.Windows => $"-win-{processor}.zip",
            HostSystem.MacOs => processor == "arm64" ? "-osx-arm64.tar.gz" : null,
            _ => $"-linux-{processor}.tar.gz",
        };
    }

    /// <summary>The file name of the binary inside the archive.</summary>
    public static string BinaryName(HostSystem system) => system == HostSystem.Windows ? WindowsBinary : UnixBinary;
}
