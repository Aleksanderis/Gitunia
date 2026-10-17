using System.Globalization;
using Gitunia.Git.Execution;
using Gitunia.Git.Reflog.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Reflog;

/// <summary>Reads reflogs with <c>git reflog show</c>.</summary>
public sealed class ReflogReader(GitRunner runner) : IReflogReader
{
    private const string Reflog = "reflog";
    private const string Show = "show";
    private const string LimitOption = "-n";

    public async Task<ParseResult<ReflogEntry>> ReadAsync(
        RepositoryLocation location,
        string reference,
        int limit,
        CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            Reflog,
            Show,
            ReflogParser.Format,
            ReflogParser.DateOption,
            GitOptions.NoColor,
            LimitOption,
            limit.ToString(CultureInfo.InvariantCulture),
            reference,
            GitOptions.EndOfOptions,
        ];
        var result = await runner
            .RunAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false);
        return ReflogParser.Parse(result.StandardOutputText);
    }
}
