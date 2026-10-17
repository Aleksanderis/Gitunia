using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Operations;

/// <summary>
/// Reads the state files git keeps for a stopped operation (<c>rebase-merge/</c>, <c>MERGE_MSG</c>,
/// <c>sequencer/</c>), so the
/// banner survives a restart of the app and never spawns a process. A file that is missing or vanishes while read
/// gives null fields.
/// </summary>
public sealed class OperationInfoReader : IOperationInfoReader
{
    private const string HeadsPrefix = "refs/heads/";
    private const string DetachedHeadName = "detached HEAD";
    private const string RebaseMerge = "rebase-merge";
    private const string RebaseApply = "rebase-apply";
    private const string Sequencer = "sequencer";
    private const string MergeMessage = "MERGE_MSG";
    private const string TodoFile = "todo";
    private const char Quote = '\'';
    private const char CommentMarker = '#';

    public Task<OperationInfo?> ReadAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken)
    {
        var gitDirectory = location.GitDirectory;
        var info = operation switch
        {
            RepositoryOperation.Merge => ReadMerge(gitDirectory),
            RepositoryOperation.Rebase => ReadRebase(gitDirectory),
            RepositoryOperation.CherryPick => ReadSequenced(gitDirectory, operation, "CHERRY_PICK_HEAD"),
            RepositoryOperation.Revert => ReadSequenced(gitDirectory, operation, "REVERT_HEAD"),
            _ => null,
        };

        return Task.FromResult(info);
    }

    private static OperationInfo ReadMerge(string gitDirectory)
    {
        var message = ReadFirstLine(Path.Combine(gitDirectory, MergeMessage));
        return new OperationInfo(RepositoryOperation.Merge, QuotedName(message), null, null, null, null, null, null);
    }

    private static OperationInfo ReadRebase(string gitDirectory)
    {
        var isMerge = Directory.Exists(Path.Combine(gitDirectory, RebaseMerge));
        var directory = Path.Combine(gitDirectory, isMerge ? RebaseMerge : RebaseApply);
        var headName = ReadFirstLine(Path.Combine(directory, "head-name"));
        var target = headName is null || headName == DetachedHeadName
            ? null
            : headName.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? headName[HeadsPrefix.Length..] : headName;
        var step = ReadNumber(Path.Combine(directory, isMerge ? "msgnum" : "next"));
        var total = ReadNumber(Path.Combine(directory, isMerge ? "end" : "last"));
        var commit = ReadFirstLine(Path.Combine(directory, "stopped-sha"));
        var subject = ReadFirstLine(Path.Combine(directory, "message"));
        var original = ReadFirstLine(Path.Combine(directory, "orig-head"));
        return new OperationInfo(RepositoryOperation.Rebase, target, step, total, null, commit, subject, original);
    }

    private static OperationInfo ReadSequenced(string gitDirectory, RepositoryOperation kind, string headFile)
    {
        var commit = ReadFirstLine(Path.Combine(gitDirectory, headFile));
        var subject = ReadFirstLine(Path.Combine(gitDirectory, MergeMessage));
        var todo = Path.Combine(gitDirectory, Sequencer, TodoFile);
        var remaining = File.Exists(todo) ? CountCommands(todo) : null;
        var original = ReadFirstLine(Path.Combine(gitDirectory, Sequencer, "head"));
        return new OperationInfo(kind, null, null, null, remaining, commit, subject, original);
    }

    private static int? CountCommands(string todoPath)
    {
        try
        {
            return File.ReadLines(todoPath).Count(line => line.Length > 0 && line[0] != CommentMarker);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? QuotedName(string? message)
    {
        if (message is null)
        {
            return null;
        }

        var start = message.IndexOf(Quote, StringComparison.Ordinal);
        var end = start < 0 ? -1 : message.IndexOf(Quote, start + 1);
        return end > start + 1 ? message[(start + 1)..end] : null;
    }

    private static int? ReadNumber(string path) =>
        int.TryParse(ReadFirstLine(path), out var number) ? number : null;

    private static string? ReadFirstLine(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            var line = reader.ReadLine()?.Trim();
            return string.IsNullOrEmpty(line) ? null : line;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
