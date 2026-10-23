using Gitunia.Git.Execution;

namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// A <see cref="GitRunner"/> for tests that run production git code on a <see cref="TestRepository"/>: the machine's
/// global and system git config is replaced by one with neutral defaults, so settings like
/// <c>core.autocrlf=true</c> cannot change what is written.
/// A test can still override a default in the repository config.
/// </summary>
public static class IsolatedGit
{
    private const string NeutralConfig =
        "[core]\n\tautocrlf = false\n\tfsync = none\n[commit]\n\tgpgsign = false\n[gc]\n\tauto = 0\n[maintenance]\n\tauto = false\n";

    // One file per test process, written once.
    private static readonly Lazy<string> ConfigPath = new(WriteConfig);

    public static GitRunner CreateRunner()
    {
        var configPath = ConfigPath.Value;
        var environment = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = configPath,
            ["GIT_CONFIG_SYSTEM"] = configPath,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_AUTHOR_NAME"] = "Test",
            ["GIT_AUTHOR_EMAIL"] = "test@example.com",
            ["GIT_COMMITTER_NAME"] = "Test",
            ["GIT_COMMITTER_EMAIL"] = "test@example.com",
            ["GIT_DIR"] = null,
            ["GIT_WORK_TREE"] = null,
            ["GIT_INDEX_FILE"] = null,
            ["GIT_OBJECT_DIRECTORY"] = null,
            ["GIT_COMMON_DIR"] = null,
        };

        return new GitRunner(new GitRunnerOptions(TestGitSpeed.Executable, EnvironmentOverrides: environment));
    }

    private static string WriteConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gitunia-isolated-{Environment.ProcessId}.gitconfig");
        File.WriteAllText(path, NeutralConfig);

        return path;
    }
}
