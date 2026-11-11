using System.Text;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>
/// Commits with the git CLI. The message goes to git on stdin (<c>-F -</c>), so it needs no temporary file and no quoting, and it is
/// kept as typed (comment lines included, unlike a commit through the editor). Hooks, signing and the author come from the
/// user's configuration; nothing is set in the environment.
/// </summary>
public sealed class CommitWriter(GitRunner runner) : ICommitWriter
{
    private const string Commit = "commit";
    private const string MessageFromFile = "-F";
    private const string StandardInputName = "-";
    private const string SignOffOption = "-s";
    private const string NoVerifyOption = "--no-verify";
    private const string AmendOption = "--amend";
    private const string RevParse = "rev-parse";
    private const string VerifyOption = "--verify";
    private const string QuietOption = "--quiet";
    private const string HeadMessageFormat = "--format=%B";
    private const string SingleCommit = "-1";
    private const string Log = "log";
    private const string MergeMessageFile = "MERGE_MSG";
    private const char CommentMark = '#';

    // Hooks (tests, linters, signing) can take minutes.
    private static readonly TimeSpan CommitTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);
    private static readonly string[] ReadHeadArguments = [RevParse, VerifyOption, QuietOption, GitOptions.HeadRevision];
    private static readonly string[] ReadHeadMessageArguments =
        [Log, SingleCommit, HeadMessageFormat, GitOptions.HeadRevision];

    public async Task<CommitResult> CommitAsync(
        RepositoryLocation location,
        CommitRequest request,
        CancellationToken cancellationToken)
    {
        var previous = request.Amend ? await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false) : null;
        var options = new GitRunOptions(Timeout: CommitTimeout, StandardInput: Encoding.UTF8.GetBytes(request.Message));
        await runner
            .RunAsync(location.CommandPath, ArgumentsOf(request), options, cancellationToken)
            .ConfigureAwait(false);

        // The commit exists now: reading its hash is not cancellable, so a late cancel never reports a commit in the history as cancelled.
        var head = await ReadHeadAsync(location, CancellationToken.None).ConfigureAwait(false);
        return new CommitResult(head ?? string.Empty, previous);
    }

    public async Task<string?> ReadMergeMessageAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(location.GitDirectory, MergeMessageFile);
            // Shares read, write and delete so the read never blocks git renaming over MERGE_MSG (Windows).
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true);
            await using (stream.ConfigureAwait(false))
            {
                using var reader = new StreamReader(stream);
                var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var lines = text.Split('\n').Select(line => line.TrimEnd('\r'));

                return string.Join('\n', lines.Where(line => !line.StartsWith(CommentMark))).TrimEnd();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The merge finished while we looked, or the file is not readable: there is no prepared message.
            return null;
        }
    }

    public async Task<string?> ReadHeadMessageAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        if (await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        var options = new GitRunOptions(Timeout: ReadTimeout, ReadOnly: true);
        var result = await runner
            .RunAsync(location.CommandPath, ReadHeadMessageArguments, options, cancellationToken)
            .ConfigureAwait(false);
        return result.StandardOutputText.TrimEnd();
    }

    private static string[] ArgumentsOf(CommitRequest request)
    {
        var arguments = new List<string> { Commit, MessageFromFile, StandardInputName };
        if (request.SignOff)
        {
            arguments.Add(SignOffOption);
        }

        if (request.NoVerify)
        {
            arguments.Add(NoVerifyOption);
        }

        if (request.Amend)
        {
            arguments.Add(AmendOption);
        }

        return [.. arguments];
    }

    // The hash of HEAD, null on an unborn branch (rev-parse --verify --quiet exits 1 without output).
    private async Task<string?> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var options = new GitRunOptions(
            Timeout: ReadTimeout,
            ReadOnly: true,
            AcceptedExitCodes: [GitRunOptions.NoMatchExitCode]);

        try
        {
            var result = await runner
                .RunAsync(location.CommandPath, ReadHeadArguments, options, cancellationToken)
                .ConfigureAwait(false);
            var text = result.StandardOutputText.Trim();
            return text.Length == 0 ? null : text;
        }
        catch (GitCommandFailedException)
        {
            return null;
        }
    }
}
