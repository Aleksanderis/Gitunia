using Gitunia.Platform.Updates;
using Xunit;

namespace Gitunia.Platform.Tests.Updates;

public class InstallRouteDetectorTests
{
    private const string ArchRelease = "NAME=\"Arch Linux\"\nID=arch\n";
    private const string OmarchyRelease = "NAME=Omarchy\nID=omarchy\nID_LIKE=arch\n";
    private const string UbuntuRelease = "NAME=\"Ubuntu\"\nID=ubuntu\nID_LIKE=debian\n";
    private const string FedoraRelease = "NAME=Fedora\nID=fedora\n";
    private const string RockyRelease = "ID=\"rocky\"\nID_LIKE=\"rhel centos fedora\"\n";

    [Theory]
    [InlineData(HostSystem.Linux, "/home/me/.local/bin/gitunia")]
    [InlineData(HostSystem.Linux, "/usr/bin/gitunia")]
    [InlineData(HostSystem.MacOs, "/opt/homebrew/bin/gitunia")]
    [InlineData(HostSystem.Windows, "C:\\Users\\me\\AppData\\Local\\Gitunia\\gitunia.exe")]
    public void GivenAScriptMarker_WhenDetecting_ThenTheRouteIsScriptWhateverThePath(HostSystem system, string path)
    {
        var facts = new InstallFacts(path, "script\n", system, ArchRelease);

        Assert.Equal(InstallRoute.Script, InstallRouteDetector.Detect(facts));
    }

    [Theory]
    [InlineData(ArchRelease, InstallRoute.Pacman)]
    [InlineData(OmarchyRelease, InstallRoute.Pacman)]
    [InlineData(UbuntuRelease, InstallRoute.Apt)]
    [InlineData(FedoraRelease, InstallRoute.Dnf)]
    [InlineData(RockyRelease, InstallRoute.Dnf)]
    [InlineData("ID=nixos\n", InstallRoute.Unknown)]
    [InlineData(null, InstallRoute.Unknown)]
    public void GivenAPackageManagedLinuxPath_WhenDetecting_ThenTheDistributionNamesTheManager(
        string? osRelease,
        InstallRoute expected)
    {
        var facts = new InstallFacts("/usr/bin/gitunia", null, HostSystem.Linux, osRelease);

        Assert.Equal(expected, InstallRouteDetector.Detect(facts));
    }

    [Theory]
    [InlineData(HostSystem.MacOs, "/opt/homebrew/bin/gitunia")]
    [InlineData(HostSystem.MacOs, "/usr/local/Cellar/gitunia/1.0.0/bin/gitunia")]
    [InlineData(HostSystem.Linux, "/home/linuxbrew/.linuxbrew/bin/gitunia")]
    public void GivenAHomebrewPath_WhenDetecting_ThenTheRouteIsHomebrew(HostSystem system, string path)
    {
        var facts = new InstallFacts(path, null, system, UbuntuRelease);

        Assert.Equal(InstallRoute.Homebrew, InstallRouteDetector.Detect(facts));
    }

    [Theory]
    [InlineData("C:\\Users\\me\\AppData\\Local\\Microsoft\\WinGet\\Packages\\Gitunia.Gitunia\\gitunia.exe")]
    [InlineData("C:\\Users\\me\\AppData\\Local\\Microsoft\\WindowsApps\\gitunia.exe")]
    public void GivenAWingetPath_WhenDetecting_ThenTheRouteIsWinget(string path)
    {
        var facts = new InstallFacts(path, null, HostSystem.Windows, null);

        Assert.Equal(InstallRoute.Winget, InstallRouteDetector.Detect(facts));
    }

    [Theory]
    [InlineData(HostSystem.Linux, "/home/me/src/Gitunia/publish/gitunia")]
    [InlineData(HostSystem.Linux, "/usr/local/bin/gitunia")]
    [InlineData(HostSystem.MacOs, "/Users/me/bin/gitunia")]
    [InlineData(HostSystem.Windows, "D:\\tools\\gitunia.exe")]
    public void GivenAnUnrecognisedPathWithoutMarker_WhenDetecting_ThenTheRouteIsUnknown(
        HostSystem system,
        string path)
    {
        var facts = new InstallFacts(path, null, system, ArchRelease);

        Assert.Equal(InstallRoute.Unknown, InstallRouteDetector.Detect(facts));
    }

    [Fact]
    public void GivenAnotherMarkerText_WhenDetecting_ThenThePathDecides()
    {
        var facts = new InstallFacts("/usr/bin/gitunia", "something else", HostSystem.Linux, ArchRelease);

        Assert.Equal(InstallRoute.Pacman, InstallRouteDetector.Detect(facts));
    }
}
