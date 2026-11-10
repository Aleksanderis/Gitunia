using Gitunia.Core.Updates;
using Xunit;

namespace Gitunia.Core.Tests.Updates;

[Trait("Os", "Independent")]
public class ReleaseVersionTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData(" v10.0.4 ", "10.0.4")]
    [InlineData("1.2.3-rc.1", "1.2.3-rc.1")]
    [InlineData("1.2.3+build.5", "1.2.3")]
    public void GivenAReleaseTag_WhenParsed_ThenTheVersionIsNormalised(string text, string expected)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));
        Assert.Equal(expected, version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("a.b.c")]
    [InlineData("1.2.3-")]
    [InlineData("1.-2.3")]
    [InlineData("latest")]
    public void GivenTextThatIsNotAVersion_WhenParsed_ThenItIsRejected(string text)
    {
        Assert.False(ReleaseVersion.TryParse(text, out _));
    }

    [Theory]
    [InlineData("1.2.4", "1.2.3", true)]
    [InlineData("1.10.0", "1.9.9", true)]
    [InlineData("2.0.0", "1.99.99", true)]
    [InlineData("1.2.3", "1.2.3", false)]
    [InlineData("1.2.2", "1.2.3", false)]
    [InlineData("1.2.3", "1.2.3-rc.1", true)]
    [InlineData("1.2.3-rc.1", "1.2.3", false)]
    [InlineData("1.2.3-rc.2", "1.2.3-rc.1", true)]
    [InlineData("1.2.3-rc.10", "1.2.3-rc.9", true)]
    [InlineData("1.2.3-rc", "1.2.3-beta", true)]
    [InlineData("1.2.3-rc.1", "1.2.3-rc", true)]
    [InlineData("1.2.3+b2", "1.2.3+b1", false)]
    public void GivenTwoVersions_WhenComparedForNewer_ThenSemanticVersioningOrderApplies(
        string candidate,
        string installed,
        bool expected)
    {
        Assert.True(ReleaseVersion.TryParse(candidate, out var left));
        Assert.True(ReleaseVersion.TryParse(installed, out var right));

        Assert.Equal(expected, left.IsNewerThan(right));
    }

    [Theory]
    [InlineData("0.0.0-dev", true)]
    [InlineData("0.0.0", false)]
    [InlineData("0.1.0-dev", false)]
    [InlineData("1.0.0", false)]
    public void GivenAVersion_WhenAskedIfItIsADevelopmentBuild_ThenOnlyTheDefaultBuildVersionIs(
        string text,
        bool expected)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));

        Assert.Equal(expected, version.IsDevelopmentBuild);
    }
}
