using System.Globalization;
using Gitunia.Git.Execution;
using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.History;

/// <summary>Reads file histories with <c>git log --follow</c>.</summary>
public sealed class FileHistoryReader(GitRunner runner) : IFileHistoryReader
{
    private const string Log = "log";
    private const string Follow = "--follow";
    private const string NameStatus = "--name-status";
    private const string FindRenames = "--find-renames";
    private const string LimitOption = "-n";

    public async Task<ParseResult<FileHistoryEntry>> ReadAsync(
        RepositoryLocation location,
        string revision,
        string path,
        int limit,
        CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            Log,
            Follow,
            FindRenames,
            NameStatus,
            GitOptions.NulTerminated,
            FileHistoryParser.Format,
            FileHistoryParser.DateOption,
            GitOptions.NoColor,
            LimitOption,
            limit.ToString(CultureInfo.InvariantCulture),
            revision,
            GitOptions.EndOfOptions,
            $"{GitOptions.LiteralPathspecPrefix}{path}",
        ];
        var result = await runner
            .RunAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);

        return FileHistoryParser.Parse(result.StandardOutputText, path);
    }
}
