using System.Runtime.InteropServices;
using Gitunia.App.Updates;
using Gitunia.Platform.Updates;
using Xunit;

namespace Gitunia.App.Tests.Updates;

public class ReleaseTargetTests
{
    [Theory]
    [InlineData(HostSystem.Linux, Architecture.X64, "-linux-x64.tar.gz")]
    [InlineData(HostSystem.Linux, Architecture.Arm64, "-linux-arm64.tar.gz")]
    [InlineData(HostSystem.Windows, Architecture.X64, "-win-x64.zip")]
    [InlineData(HostSystem.Windows, Architecture.Arm64, "-win-arm64.zip")]
    [InlineData(HostSystem.MacOs, Architecture.Arm64, "-osx-arm64.tar.gz")]
    [InlineData(HostSystem.MacOs, Architecture.X64, null)]
    [InlineData(HostSystem.Linux, Architecture.X86, null)]
    public void GivenASystemAndArchitecture_WhenAskedForTheArchive_ThenTheReleaseNamingIsUsed(
        HostSystem system,
        Architecture architecture,
        string? expected)
    {
        Assert.Equal(expected, ReleaseTarget.ArchiveSuffix(system, architecture));
    }

    [Theory]
    [InlineData(HostSystem.Windows, "gitunia.exe")]
    [InlineData(HostSystem.Linux, "gitunia")]
    [InlineData(HostSystem.MacOs, "gitunia")]
    public void GivenASystem_WhenAskedForTheBinaryName_ThenWindowsGetsAnExe(HostSystem system, string expected)
    {
        Assert.Equal(expected, ReleaseTarget.BinaryName(system));
    }
}
