using Gitunia.Git.Execution;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>Searches commit messages with <c>git log --grep</c>, so bodies never have to be kept in memory.</summary>
public sealed class CommitMessageSearcher : ICommitMessageSearcher
{
    private static readonly GitRunOptions Options = new(Timeout: TimeSpan.FromSeconds(60), ReadOnly: true);

    private readonly GitRunner _runner;

    public CommitMessageSearcher(GitRunner runner)
    {
        _runner = runner;
    }

    public async Task<IReadOnlySet<ObjectId>> SearchAsync(
        RepositoryLocation location,
        string text,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "log",
            GitOptions.NulTerminated,
            "--format=%H",
            "--regexp-ignore-case",
            "--fixed-strings",
            $"--grep={text}",
            $"--exclude={GitRefPrefixes.Stash}",
            "--all",
            GitOptions.EndOfOptions,
        ];
        var result = await _runner
            .RunAsync(location.CommandPath, arguments, Options, cancellationToken)
            .ConfigureAwait(false);
        var hits = new HashSet<ObjectId>();
        foreach (var field in result.StandardOutputText.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ObjectId.TryParse(field.Trim(), out var id))
            {
                hits.Add(id);
            }
        }

        return hits;
    }
}
