using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.IssueTracker.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.IssueTracker;

/// <summary>
/// Reads the <c>issuetracker</c> sections of <c>.issuetracker</c> and of the effective git config (repository, global
/// and system) with <c>git config</c>, so there is no parser of our own.
/// </summary>
public sealed class IssueTrackerReader : IIssueTrackerReader
{
    /// <summary>The file name, in the working tree root.</summary>
    public const string FileName = ".issuetracker";

    private const string Config = "config";
    private const string FileOption = "--file";
    private const string ListOption = "--list";
    private const string NullSeparatedOption = "-z";

    private static readonly string[] ConfigArguments = [Config, ListOption, NullSeparatedOption];

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
        IReadOnlyList<IssueTrackerRule> fileRules = [];
        if (File.Exists(path))
        {
            string[] fileArguments = [Config, FileOption, path, ListOption, NullSeparatedOption];
            fileRules = await ReadConfigAsync(root, fileArguments, cancellationToken).ConfigureAwait(false);
        }

        var configRules = await ReadConfigAsync(root, ConfigArguments, cancellationToken).ConfigureAwait(false);

        // The file wins over the git config when both define a section of the same name.
        return
        [
            .. fileRules,
            .. configRules.Where(rule => fileRules.All(
                fileRule => !string.Equals(fileRule.Name, rule.Name, StringComparison.Ordinal))),
        ];
    }

    private async Task<IReadOnlyList<IssueTrackerRule>> ReadConfigAsync(
        string root,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner
                .RunAsync(root, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
                .ConfigureAwait(false);

            return IssueTrackerParser.Parse(result.StandardOutputText);
        }
        catch (Exception exception) when (exception is GitCommandFailedException or GitTimeoutException)
        {
            return [];
        }
    }
}
