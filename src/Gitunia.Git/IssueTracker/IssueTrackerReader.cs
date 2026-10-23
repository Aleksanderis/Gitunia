using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.IssueTracker.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.IssueTracker;

/// <summary>Reads <c>.issuetracker</c> with <c>git config --file</c>, so there is no parser of our own.</summary>
public sealed class IssueTrackerReader : IIssueTrackerReader
{
    /// <summary>The file name, in the working tree root.</summary>
    public const string FileName = ".issuetracker";

    private const string Config = "config";
    private const string FileOption = "--file";
    private const string ListOption = "--list";
    private const string NullSeparatedOption = "-z";

    private readonly GitRunner _runner;

    public IssueTrackerReader(GitRunner runner)
    {
        _runner = runner;
    }

    public async Task<IReadOnlyList<IssueTrackerRule>> ReadAsync(
        RepositoryLocation location,
        CancellationToken cancellationToken)
    {
        if (location.WorkingTreePath is not { } root)
        {
            return [];
        }

        var path = Path.Combine(root, FileName);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var result = await _runner
                .RunAsync(
                    root,
                    [Config, FileOption, path, ListOption, NullSeparatedOption],
                    GitRunOptions.ReadOnlyRun,
                    cancellationToken)
                .ConfigureAwait(false);

            return IssueTrackerParser.Parse(result.StandardOutputText);
        }
        catch (Exception exception) when (exception is GitCommandFailedException or GitTimeoutException)
        {
            return [];
        }
    }
}
