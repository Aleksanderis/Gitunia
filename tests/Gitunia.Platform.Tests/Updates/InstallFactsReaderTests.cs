using Gitunia.Platform.Updates;
using Xunit;

namespace Gitunia.Platform.Tests.Updates;

public sealed class InstallFactsReaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("gitunia-facts-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void GivenAMarkerBesideTheBinary_WhenReading_ThenTheFactsCarryItsText()
    {
        var executable = Path.Combine(_directory, "gitunia");
        File.WriteAllText(Path.Combine(_directory, InstallMarker.FileName), "script\n");

        var facts = new InstallFactsReader(executable).Read();

        Assert.NotNull(facts);
        Assert.Equal(executable, facts.ExecutablePath);
        Assert.Equal("script\n", facts.MarkerText);
        Assert.Equal(InstallRoute.Script, InstallRouteDetector.Detect(facts));
    }

    [Fact]
    public void GivenNoMarker_WhenReading_ThenTheMarkerTextIsNull()
    {
        var facts = new InstallFactsReader(Path.Combine(_directory, "gitunia")).Read();

        Assert.NotNull(facts);
        Assert.Null(facts.MarkerText);
    }

    [Fact]
    public void GivenAnEmptyExecutablePath_WhenReading_ThenThereAreNoFacts()
    {
        Assert.Null(new InstallFactsReader(string.Empty).Read());
    }

    [Fact]
    public void GivenAScriptCopyInTheHomeBin_WhenReadingAnotherBinary_ThenTheFactsCarryItsPath()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no second script copy to name.");
        var home = Path.Combine(_directory, "home");
        var bin = Directory.CreateDirectory(Path.Combine(home, ".local", "bin")).FullName;
        var copy = Path.Combine(bin, "gitunia");
        File.WriteAllText(copy, "x");
        File.WriteAllText(Path.Combine(bin, InstallMarker.FileName), "script\n");

        var facts = new InstallFactsReader(Path.Combine(_directory, "usr-bin", "gitunia"), home).Read();

        Assert.NotNull(facts);
        Assert.Equal(copy, facts.OtherScriptCopyPath);
    }

    [Fact]
    public void GivenTheRunningBinaryIsTheScriptCopy_WhenReading_ThenThereIsNoOtherCopy()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no second script copy to name.");
        var home = Path.Combine(_directory, "home");
        var bin = Directory.CreateDirectory(Path.Combine(home, ".local", "bin")).FullName;
        File.WriteAllText(Path.Combine(bin, "gitunia"), "x");
        File.WriteAllText(Path.Combine(bin, InstallMarker.FileName), "script\n");

        var facts = new InstallFactsReader(Path.Combine(bin, "gitunia"), home).Read();

        Assert.NotNull(facts);
        Assert.Null(facts.OtherScriptCopyPath);
    }

    [Fact]
    public void GivenAHomeBinCopyWithoutTheScriptMarker_WhenReading_ThenThereIsNoOtherCopy()
    {
        var home = Path.Combine(_directory, "home");
        var bin = Directory.CreateDirectory(Path.Combine(home, ".local", "bin")).FullName;
        File.WriteAllText(Path.Combine(bin, "gitunia"), "x");

        var facts = new InstallFactsReader(Path.Combine(_directory, "usr-bin", "gitunia"), home).Read();

        Assert.NotNull(facts);
        Assert.Null(facts.OtherScriptCopyPath);
    }

    [Fact]
    public void GivenWindows_WhenAMarkedCopyExistsInTheHomeBin_ThenThereIsNoOtherCopy()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var home = Path.Combine(_directory, "home");
        var bin = Directory.CreateDirectory(Path.Combine(home, ".local", "bin")).FullName;
        File.WriteAllText(Path.Combine(bin, "gitunia"), "x");
        File.WriteAllText(Path.Combine(bin, InstallMarker.FileName), "script\n");

        var facts = new InstallFactsReader(Path.Combine(_directory, "other", "gitunia.exe"), home).Read();

        Assert.NotNull(facts);
        Assert.Null(facts.OtherScriptCopyPath);
    }
}
