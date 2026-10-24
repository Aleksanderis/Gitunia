using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Conflicts;

/// <summary>Re-creates a conflict with <c>checkout --merge -- &lt;file&gt;</c>; the path is taken literally.</summary>
public sealed class ConflictRecreator(GitRunner runner) : IConflictRecreator
{
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(1);

    public async Task RecreateAsync(
        RepositoryLocation location,
        string path,
        CancellationToken cancellationToken = default)
    {
        var literal = $"{GitOptions.LiteralPathspecPrefix}{path}";
        string[] arguments = ["checkout", "--merge", GitOptions.EndOfOptions, literal];
        var options = new GitRunOptions(Timeout: WriteTimeout);
        await runner.RunAsync(location.CommandPath, arguments, options, cancellationToken).ConfigureAwait(false);
    }
}
