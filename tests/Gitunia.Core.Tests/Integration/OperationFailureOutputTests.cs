using Gitunia.Core.Integration;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Integration.IntegrationFixtures;

namespace Gitunia.Core.Tests.Integration;

public class OperationFailureOutputTests
{
    [Fact]
    public void GivenBothStreams_WhenBuilding_ThenStandardErrorComesFirstAndCrLfIsDropped()
    {
        var output = OperationFailureOutput.Of(
            "continue",
            Info(RepositoryOperation.CherryPick),
            "error: one\r\nhint: two\r\n",
            "out");

        Assert.Equal(["error: one", "hint: two", "out"], output.Lines);
    }

    [Fact]
    public void GivenBlankLinesAtTheEdges_WhenBuilding_ThenTheEdgesAreTrimmedAndTheMiddleKept()
    {
        var output = OperationFailureOutput.Of("skip", Info(RepositoryOperation.Rebase), "\n\nerror\n\nhint\n\n", "");

        Assert.Equal(["error", "", "hint"], output.Lines);
    }

    [Fact]
    public void GivenNoOutput_WhenBuilding_ThenTheNoOutputLineIsShown()
    {
        var output = OperationFailureOutput.Of("abort", Info(RepositoryOperation.Merge), "", "  \n");

        Assert.Equal([OperationFailureOutput.NoOutput], output.Lines);
    }

    [Fact]
    public void GivenACherryPick_WhenBuilding_ThenTitleAndHeadingNameTheVerbAndTheOperation()
    {
        var output = OperationFailureOutput.Of("continue", Info(RepositoryOperation.CherryPick), "x", "");

        Assert.Equal("Could not continue the cherry-pick", output.Title);
        Assert.Equal("Output of git continue", output.Heading);
    }
}
