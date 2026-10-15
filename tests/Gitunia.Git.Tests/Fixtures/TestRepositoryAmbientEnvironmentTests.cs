using Xunit;

namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// Mutates GIT_CONFIG_GLOBAL and GIT_COMMITTER_NAME, which every git child process inherits; a concurrent git run would
/// read the temporary config while it is deleted and fail with "unknown error occurred while reading the configuration
/// files" (#299).
/// </summary>
[Collection(AmbientEnvironmentGroup.Name)]
public class TestRepositoryAmbientEnvironmentTests
{
    [Fact]
    public void GivenAmbientGitIdentityAndConfig_WhenCommitting_ThenFixtureIdentityIsUsed()
    {
        var home = Path.Combine(Path.GetTempPath(), $"gitunia-home-{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(home);
        var config = Path.Combine(home, "config");
        File.WriteAllText(config, "[user]\n\tname = Ambient\n");
        var previousName = Environment.GetEnvironmentVariable("GIT_COMMITTER_NAME");
        var previousGlobal = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        try
        {
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", "Ambient");
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", config);
            using var repository = TestRepositories.Linear(1);

            Assert.Equal("Test", repository.Git("log", "-1", "--format=%an|%cn").Trim().Split('|')[0]);
            Assert.Equal("Test", repository.Git("log", "-1", "--format=%an|%cn").Trim().Split('|')[1]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", previousName);
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", previousGlobal);
            Directory.Delete(home, recursive: true);
        }
    }
}
