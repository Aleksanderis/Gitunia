using Xunit;

namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// Mutates process-wide environment variables (GIT_CONFIG_GLOBAL, GIT_COMMITTER_NAME) that every git child process
/// inherits, so it must not overlap with any other test: a concurrent git run reads the temporary config while it is
/// deleted and fails with "unknown error occurred while reading the configuration files" (#299).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AmbientEnvironmentGroup
{
    public const string Name = "Ambient git environment";
}

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
