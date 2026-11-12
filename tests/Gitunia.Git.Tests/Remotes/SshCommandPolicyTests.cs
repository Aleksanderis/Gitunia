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
    [InlineData("\"C:\\Program Files\\Git\\usr\\bin\\ssh.exe\" -i key")]
    [InlineData("'C:\\Program Files\\Git\\usr\\bin\\ssh.exe' -i key")]
    [InlineData("\"/opt/my tools/ssh\" -i key")]
    [InlineData("C:/Git/usr/bin/SSH.EXE -i key")]
    [InlineData("\"C:\\Program Files\\Git\\usr\\bin\\ssh.exe\"")]
    [InlineData("ssh.exe\t-i key")]
    public void GivenAQuotedOrWindowsSshPath_WhenAsked_ThenItGetsBatchMode(string command)
    {
        Assert.Equal($"{command} -o BatchMode=yes", SshCommandPolicy.BatchCommand(null, null, command));
    }

    [Fact]
    public void GivenBlanksAroundAQuotedPath_WhenAsked_ThenTheTrimmedCommandGetsBatchMode()
    {
        Assert.Equal(
            "\"/opt/my tools/ssh\"\t-i key -o BatchMode=yes",
            SshCommandPolicy.BatchCommand(null, null, "  \"/opt/my tools/ssh\"\t-i key"));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\PuTTY\\plink.exe\" -batch")]
    [InlineData("\"C:\\Program Files\\Git\\usr\\bin\\ssh.exe -i key")]
    [InlineData("C:\\Program Files\\Git\\usr\\bin\\ssh.exe -i key")]
    [InlineData("\"ssh\"-i key")]
    [InlineData("\"\" -i key")]
    public void GivenAQuotedOtherClientAnUnclosedQuoteOrAnUnquotedSpacedPath_WhenAsked_ThenItIsLeftAlone(
        string command)
    {
        Assert.Null(SshCommandPolicy.BatchCommand(null, null, command));
    }

    [Theory]
    [InlineData("plink -batch", null)]
    [InlineData(null, "/opt/wrapper")]
    public void GivenAnotherClientOrAProgram_WhenAsked_ThenItIsLeftAlone(string? command, string? program)
    {
        Assert.Null(SshCommandPolicy.BatchCommand(command, program, null));
    }
}
