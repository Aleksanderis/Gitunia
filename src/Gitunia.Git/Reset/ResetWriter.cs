using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Reset;

/// <summary>Resets with the git CLI; hooks and config are the user's.</summary>
public sealed class ResetWriter(GitRunner runner) : IResetWriter
{
    private const string Reset = "reset";
    private const string RevParse = "rev-parse";
    private const string Verify = "--verify";
    private const string Quiet = "--quiet";
    private const string SoftOption = "--soft";
    private const string MixedOption = "--mixed";
    private const string HardOption = "--hard";
    private const string CommitSuffix = "^{commit}";

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private static readonly GitRunOptions ReadOptions = new(Timeout: ReadTimeout, ReadOnly: true);
    private static readonly GitRunOptions WriteOptions = new(Timeout: WriteTimeout);

    public async Task<string> ResetAsync(
        RepositoryLocation location,
        ResetMode mode,
        string commit,
        CancellationToken cancellationToken)
    {
        // A cancel before the start must not run a write that a short-lived process would finish before it is noticed.
        cancellationToken.ThrowIfCancellationRequested();
        var before = await runner
            .RunAsync(
                location.CommandPath,
                [RevParse, Verify, Quiet, GitOptions.HeadRevision],
                ReadOptions,
                cancellationToken)
            .ConfigureAwait(false);
        var oldHead = before.StandardOutputText.Trim();

        // Only a commit is a valid target: a tag object or a tree would reset to something else than the user chose.
        await runner
            .RunAsync(
                location.CommandPath,
                [Reset, ModeOption(mode), Quiet, $"{commit}{CommitSuffix}", GitOptions.EndOfOptions],
                WriteOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return oldHead;
    }

    private static string ModeOption(ResetMode mode) => mode switch
    {
        ResetMode.Soft => SoftOption,
        ResetMode.Mixed => MixedOption,
        _ => HardOption,
    };
}
