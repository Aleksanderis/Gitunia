using Gitunia.Git.Execution;

namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// A <see cref="GitRunner"/> for tests that run production git code on a <see cref="TestRepository"/>: the user's and
/// runner's global and system git config is replaced by one with neutral defaults, so settings like
/// <c>core.autocrlf=true</c> cannot change what is written.
/// A test can still override a default in the repository config.
/// </summary>
public static class IsolatedGit
{
    private const string NeutralConfig = "[core]\n\tautocrlf = false\n[commit]\n\tgpgsign = false\n";

    public static GitRunner CreateRunner()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"gitunia-isolated-{Guid.NewGuid().ToString("N")}.gitconfig");
        File.WriteAllText(configPath, NeutralConfig);
        var environment = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = configPath,
            ["GIT_CONFIG_SYSTEM"] = configPath,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_AUTHOR_NAME"] = "Test",
            ["GIT_AUTHOR_EMAIL"] = "test@example.com",
            ["GIT_COMMITTER_NAME"] = "Test",
            ["GIT_COMMITTER_EMAIL"] = "test@example.com",
        };

        return new GitRunner(new GitRunnerOptions(EnvironmentOverrides: environment));
    }
}
