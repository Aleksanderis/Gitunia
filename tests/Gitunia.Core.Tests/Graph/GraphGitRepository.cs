using System.Globalization;
using Gitunia.Git;

namespace Gitunia.Core.Tests.Graph;

/// <summary>A throwaway, isolated git repository of empty commits used to compare the layout with real git output.</summary>
public sealed class GraphGitRepository : IDisposable
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string rootDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gitunia-graph-" + Guid.NewGuid().ToString("N"));
    private readonly string emptyConfigPath;
    private int clock;

    public GraphGitRepository()
    {
        Path = System.IO.Path.Combine(rootDirectory, "repo");
        Directory.CreateDirectory(Path);
        emptyConfigPath = System.IO.Path.Combine(rootDirectory, "empty.gitconfig");
        File.WriteAllText(emptyConfigPath, string.Empty);
        Git("init", "-q", "-b", "main");
    }

    public string Path { get; }

    /// <summary>Branchy history: a feature branch merged twice, an octopus merge of three topics, and a second root.</summary>
    public static GraphGitRepository CreateBranchy()
    {
        var repository = new GraphGitRepository();
        repository.Commit("base");
        repository.Git("checkout", "-q", "-b", "feature");
        repository.Commit("feature 1");
        repository.Commit("feature 2");
        repository.Git("checkout", "-q", "main");
        repository.Commit("main 1");
        repository.Git("merge", "-q", "--no-ff", "-m", "merge feature", "feature");
        repository.Git("checkout", "-q", "-b", "feature-again", "feature");
        repository.Commit("feature 3");
        repository.Git("checkout", "-q", "main");
        repository.Commit("main 2");
        repository.Git("merge", "-q", "--no-ff", "-m", "merge feature again", "feature-again");
        foreach (var topic in new[] { "topic-a", "topic-b", "topic-c" })
        {
            repository.Git("checkout", "-q", "-b", topic, "main~1");
            repository.Commit(topic);
        }

        repository.Git("checkout", "-q", "main");
        repository.Git("merge", "-q", "--no-ff", "-m", "octopus", "topic-a", "topic-b", "topic-c");
        repository.Git("checkout", "-q", "--orphan", "unrelated");
        repository.Commit("second root");
        repository.Git("checkout", "-q", "main");
        return repository;
    }

    public void Commit(string message) => Git("commit", "-q", "--allow-empty", "-m", message);

    public string Git(params string[] arguments) =>
        CreateRunner().RunAsync(Path, arguments, GitRunOptions.Default).GetAwaiter().GetResult().StandardOutputText;

    /// <summary>A runner isolated from the user's git config, with a fixed identity and the next fake clock tick.</summary>
    public GitRunner CreateRunner()
    {
        var stamp = Epoch.AddMinutes(clock++).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return new GitRunner(new GitRunnerOptions(
            EnvironmentOverrides: new Dictionary<string, string?>
            {
                ["GIT_CONFIG_GLOBAL"] = emptyConfigPath,
                ["GIT_CONFIG_SYSTEM"] = emptyConfigPath,
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_AUTHOR_NAME"] = "Test",
                ["GIT_AUTHOR_EMAIL"] = "test@example.com",
                ["GIT_COMMITTER_NAME"] = "Test",
                ["GIT_COMMITTER_EMAIL"] = "test@example.com",
                ["GIT_AUTHOR_DATE"] = stamp,
                ["GIT_COMMITTER_DATE"] = stamp,
            }));
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        try
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup of a temp directory.
        }
    }
}
