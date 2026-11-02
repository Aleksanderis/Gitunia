using System.Globalization;
using Gitunia.Git.Execution;
using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.History;

/// <summary>Reads file histories with <c>git log --follow</c>.</summary>
public sealed class FileHistoryReader(GitRunner runner) : IFileHistoryReader
{
    private const string LimitOption = "-n";
    private const string RevisionSuffix = "^{commit}";

    private static readonly string[] FixedArguments =
    [
        "log",
        "--follow",
        "--find-renames",
        "--name-status",
        GitOptions.NulTerminated,
        FileHistoryParser.Format,
        FileHistoryParser.DateOption,
        GitOptions.NoColor,
        "--no-show-signature",
        "--encoding=UTF-8",
    ];

    private static readonly string[] ResolveArguments = ["rev-parse", "--verify", "--quiet"];

    public async Task<ParseResult<FileHistoryEntry>> ReadAsync(
        RepositoryLocation location,
        string revision,
        string path,
        int limit,
        CancellationToken cancellationToken)
    {
        // An unborn HEAD (a fresh repository with staged files) has no history; log would fail on it.
        if (!await ExistsAsync(location, revision, cancellationToken).ConfigureAwait(false))
        {
            return new ParseResult<FileHistoryEntry>([], []);
        }

        string[] arguments =
        [
            .. FixedArguments,
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

    private async Task<bool> ExistsAsync(RepositoryLocation location, string revision, CancellationToken token)
    {
        string[] arguments = [.. ResolveArguments, $"{revision}{RevisionSuffix}"];
        var result = await runner
            .RunAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRunAllowingNoMatch, token)
            .ConfigureAwait(false);

        return result.ExitCode == 0;
    }
}
