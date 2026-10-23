using System.Globalization;
using System.Text;
using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Rebase;

/// <summary>
/// Runs <c>git rebase -i</c> with a todo Gitunia prepared. Git is given <c>-c sequence.editor=&lt;command&gt;</c> (per
/// call, never written to the config) where the command is the <see cref="SequenceEditorCommand"/> followed by the
/// prepared todo, so git's own todo is replaced by ours; <c>-c core.editor=true</c> keeps squash from opening an editor
/// (the messages are combined as git does). A reword is a pick plus an <c>exec git commit --amend -F &lt;file&gt;</c>;
/// the message files live in <c>&lt;git dir&gt;/gitunia-rebase</c> until the rebase ends, because a conflict stop
/// hands over to continue/skip/abort and the exec lines run later. A range with merge commits is refused unless
/// <see cref="RebaseBase.KeepMerges"/> is set: then git prepares the <c>--rebase-merges</c> todo, which is read
/// (<c>label</c>, <c>reset</c> and <c>merge</c> lines included) and written back as edited. The user's
/// <c>GIT_SEQUENCE_EDITOR</c> would win over the config, so its presence is refused instead of silently ignoring the
/// todo.
/// </summary>
public sealed class InteractiveRebaseWriter(
    OperationRunner operations,
    GitRunner git,
    IRepositoryOperationDetector detector,
    SequenceEditorCommand editor,
    Func<string, string?> environment) : IInteractiveRebaseWriter
{
    private const string WorkDirectoryName = "gitunia-rebase";

    private const string TodoFileName = "todo";

    private const string SequenceEditorVariable = "GIT_SEQUENCE_EDITOR";

    // Prints a marker and git's todo, then fails: git removes its state and restores an autostash.
    private const string DumpMarker = "---gitunia-todo---\n";

    private const string DumpEditor = "echo ---gitunia-todo---; cat \"$1\"; false";

    private const int DumpExitCode = 1;

    private const string CommitSuffix = "^{commit}";

    private const string RangeSeparator = "..";

    private const int ParentsField = 1;

    private const int SubjectField = 2;

    private const int FieldCount = 3;

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private static readonly GitRunOptions ReadOptions = new(Timeout: ReadTimeout, ReadOnly: true);

    private static readonly string LogFormat =
        $"--format=%H{GitSeparators.UnitSeparatorPlaceholder}%P{GitSeparators.UnitSeparatorPlaceholder}%s";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<IReadOnlyList<RebaseTodoCommit>> ReadRangeAsync(
        RepositoryLocation location,
        RebaseBase rebaseBase,
        CancellationToken cancellationToken)
    {
        RejectOption(rebaseBase);
        var arguments = new List<string>
        {
            "log",
            GitOptions.NoColor,
            "--topo-order",
            "--reverse",
            GitOptions.NulTerminated,
            LogFormat,
            rebaseBase.IsRoot
                ? GitOptions.HeadRevision
                : $"{rebaseBase.Onto}{CommitSuffix}{RangeSeparator}{GitOptions.HeadRevision}",
            GitOptions.EndOfOptions,
        };
        var result = await git.RunAsync(location.CommandPath, arguments, ReadOptions, cancellationToken)
            .ConfigureAwait(false);

        var commits = new List<RebaseTodoCommit>();
        var merges = 0;
        foreach (var record in result.StandardOutputText.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = record.Split((char)GitSeparators.UnitSeparator);
            if (fields.Length != FieldCount)
            {
                continue;
            }

            if (fields[ParentsField].Contains(' ', StringComparison.Ordinal))
            {
                merges++;
            }

            commits.Add(new RebaseTodoCommit(fields[0], fields[SubjectField]));
        }

        if (commits.Count == 0)
        {
            throw new RebaseRangeException("There are no commits to rebase");
        }

        if (merges > 0 && rebaseBase.KeepMerges)
        {
            return await ReadTodoAsync(location, rebaseBase, cancellationToken).ConfigureAwait(false);
        }

        if (merges > 0)
        {
            throw new RebaseRangeException(string.Create(
                CultureInfo.InvariantCulture,
                $"The range contains {merges} merge commit(s); they cannot be rebased without keeping them"));
        }

        return commits;
    }

    public async Task<InteractiveRebaseResult> RebaseAsync(
        RepositoryLocation location,
        RebaseBase rebaseBase,
        IReadOnlyList<RebaseTodoEntry> entries,
        CancellationToken cancellationToken)
    {
        RejectSequenceEditor();
        var range = await ReadRangeAsync(location, rebaseBase, cancellationToken).ConfigureAwait(false);
        RebaseTodoValidator.Validate(range, entries);
        cancellationToken.ThrowIfCancellationRequested();

        var originalHead = await ReadHeadAsync(location, cancellationToken).ConfigureAwait(false);
        var directory = Path.Combine(location.GitDirectory, WorkDirectoryName);
        if (detector.Detect(location) == RepositoryOperation.None && Directory.Exists(directory))
        {
            // Left over from a rebase that was aborted or finished elsewhere; nothing refers to it any more.
            Directory.Delete(directory, recursive: true);
        }

        var keepsMerges = range.Any(commit => commit.Action != RebaseTodoAction.Pick);
        var todoPath = await WriteFilesAsync(directory, entries, cancellationToken).ConfigureAwait(false);
        try
        {
            var arguments = BuildArguments(rebaseBase, todoPath, keepsMerges);
            var result = await operations.RunAsync(location, arguments, cancellationToken).ConfigureAwait(false);
            return new InteractiveRebaseResult(result.Outcome, result.Head, originalHead);
        }
        finally
        {
            // A refused start (dirty tree) and a finished rebase leave no operation and need no files. Any rebase
            // still in progress (a conflict stop, a hook that rejected an exec line, a cancel) keeps them, because
            // the exec lines of the rest of the todo run on continue.
            if (detector.Detect(location) == RepositoryOperation.None)
            {
                DeleteQuietly(directory);
            }
        }
    }

    private void RejectSequenceEditor()
    {
        if (!string.IsNullOrEmpty(environment(SequenceEditorVariable)))
        {
            throw new RebaseEnvironmentException(
                $"{SequenceEditorVariable} is set and would replace Gitunia's todo; unset it to rebase interactively");
        }
    }

    // Lets git prepare the todo of --rebase-merges (the label, reset and merge lines are its to get right), prints it
    // through the sequence editor and fails the editor so that git stops before anything is changed.
    private async Task<IReadOnlyList<RebaseTodoCommit>> ReadTodoAsync(
        RepositoryLocation location,
        RebaseBase rebaseBase,
        CancellationToken cancellationToken)
    {
        RejectSequenceEditor();
        var arguments = new List<string>
        {
            "-c",
            "core.editor=true",
            "-c",
            "core.abbrev=no",
            "-c",
            "rebase.abbreviateCommands=false",
            "-c",
            "rebase.instructionFormat=%s",
            "-c",
            $"sequence.editor={DumpEditor}",
            "rebase",
            "--interactive",
            "--rebase-merges",
            "--no-update-refs",
            "--no-verify",
            rebaseBase.IsRoot ? "--root" : $"{rebaseBase.Onto}{CommitSuffix}",
        };
        var options = new GitRunOptions(Timeout: ReadTimeout, AcceptedExitCodes: [DumpExitCode]);
        var result = await git.RunAsync(location.CommandPath, arguments, options, cancellationToken)
            .ConfigureAwait(false);

        var output = result.StandardOutputText;
        var marker = output.IndexOf(DumpMarker, StringComparison.Ordinal);
        if (marker < 0)
        {
            var reason = result.StandardError.Trim();
            throw new RebaseRangeException(
                reason.Length == 0
                    ? "Git did not prepare a todo for the range"
                    : $"Git did not prepare a todo: {reason}");
        }

        var todo = RebaseTodoParser.Parse(output[(marker + DumpMarker.Length)..]);
        if (todo.Count == 0 || todo.All(line => line.Action.IsStructure()))
        {
            throw new RebaseRangeException("There are no commits to rebase");
        }

        return todo;
    }

    private static void RejectOption(RebaseBase rebaseBase)
    {
        if (rebaseBase.Onto is { } onto && onto.StartsWith('-'))
        {
            throw new ArgumentException("A rebase target cannot start with '-'", nameof(rebaseBase));
        }
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover directory is removed by the next interactive rebase.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: it is only a few small files.
        }
    }

    private static async Task<string> WriteFilesAsync(
        string directory,
        IReadOnlyList<RebaseTodoEntry> entries,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var messageFiles = new string?[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Action != RebaseTodoAction.Reword)
            {
                continue;
            }

            var path = Path.Combine(directory, $"reword-{index.ToString(CultureInfo.InvariantCulture)}.txt");
            await File.WriteAllTextAsync(path, entries[index].Message, Utf8, cancellationToken).ConfigureAwait(false);
            messageFiles[index] = SequenceEditorCommand.ToGitPath(path);
        }

        var todoPath = Path.Combine(directory, TodoFileName);
        var todo = RebaseTodoFormatter.Format(entries, messageFiles);
        await File.WriteAllTextAsync(todoPath, todo, Utf8, cancellationToken).ConfigureAwait(false);
        return todoPath;
    }

    private List<string> BuildArguments(RebaseBase rebaseBase, string todoPath, bool keepsMerges)
    {
        var arguments = new List<string>
        {
            "-c",
            "core.editor=true",
            "-c",
            $"sequence.editor={editor.ToConfigValue(todoPath)}",
            "rebase",
            "--interactive",
            "--no-update-refs",
            "--empty=drop",
        };
        if (keepsMerges)
        {
            arguments.Add("--rebase-merges");
        }

        if (rebaseBase.IsRoot)
        {
            arguments.Add("--root");
        }
        else
        {
            arguments.Add($"{rebaseBase.Onto}{CommitSuffix}");
        }

        return arguments;
    }

    private async Task<string> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await git
            .RunAsync(
                location.CommandPath,
                ["rev-parse", "--verify", GitOptions.HeadRevision],
                ReadOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return result.StandardOutputText.Trim();
    }
}
