using Gitunia.Git.Remotes;
using Xunit;

namespace Gitunia.Git.Tests.Remotes;

public class SshCommandPolicyTests
{
    [Fact]
    public void GivenNothingConfigured_WhenAsked_ThenPlainSshRunsInBatchMode()
    {
        Assert.Equal("ssh -o BatchMode=yes", SshCommandPolicy.BatchCommand(null, null, null));
    }

    [Fact]
    public void GivenAConfiguredSshCommand_WhenAsked_ThenItIsKeptAndExtended()
    {
        Assert.Equal(
            "ssh -i /k/id -o BatchMode=yes",
            SshCommandPolicy.BatchCommand(null, null, "ssh -i /k/id"));
    }

    [Fact]
    public void GivenTheVariableAndTheConfig_WhenAsked_ThenTheVariableWins()
    {
        Assert.Equal(
            "/usr/bin/ssh -F x -o BatchMode=yes",
            SshCommandPolicy.BatchCommand("/usr/bin/ssh -F x", null, "ssh -i other"));
    }

    [Fact]
    public void GivenBatchModeAlready_WhenAsked_ThenTheCommandIsNotDoubled()
    {
        Assert.Equal("ssh -o BatchMode=yes", SshCommandPolicy.BatchCommand("ssh -o BatchMode=yes", null, null));
    }

    [Fact]
    public void GivenAProgramAndAConfiguredSsh_WhenAsked_ThenTheConfigWinsAndGetsBatchMode()
    {
        Assert.Equal(
            "ssh -F cfg -o BatchMode=yes",
            SshCommandPolicy.BatchCommand(null, "/opt/wrapper", "ssh -F cfg"));
    }

    [Theory]
    [InlineData("plink -batch", null)]
    [InlineData(null, "/opt/wrapper")]
    public void GivenAnotherClientOrAProgram_WhenAsked_ThenItIsLeftAlone(string? command, string? program)
    {
        Assert.Null(SshCommandPolicy.BatchCommand(command, program, null));
    }
}
