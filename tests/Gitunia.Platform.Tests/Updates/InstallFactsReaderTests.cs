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
}
