using Gitunia.Platform.ExternalTools;
using Xunit;

namespace Gitunia.Platform.Tests.ExternalTools;

public class ExternalToolLauncherTests
{
    private readonly ExternalToolLauncher _launcher = new();

    [Fact]
    public void GivenAProgramThatSucceeds_WhenRunning_ThenExitCodeIsZero()
    {
        var result = _launcher.Run(new ExternalToolRequest("git", ["--version"], Path.GetTempPath()));

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void GivenAProgramThatFails_WhenRunning_ThenItsExitCodeIsReturned()
    {
        var result = _launcher.Run(new ExternalToolRequest("git", ["no-such-subcommand"], Path.GetTempPath()));

        Assert.False(result.Succeeded);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Null(result.Error);
        Assert.Equal($"tool exited with code {result.ExitCode}", result.Describe("tool"));
    }

    [Fact]
    public void GivenAMissingProgram_WhenRunning_ThenAnErrorIsReturnedInsteadOfThrowing()
    {
        var result = _launcher.Run(new ExternalToolRequest("gitunia-no-such-program", [], Path.GetTempPath()));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.StartsWith("tool: could not start", result.Describe("tool"));
    }
}
