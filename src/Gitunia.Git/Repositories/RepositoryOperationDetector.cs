using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Repositories;

/// <summary>Detects a stopped multi-step operation (rebase, merge, ...) of a worktree from the state files in its git directory.</summary>
public sealed class RepositoryOperationDetector : IRepositoryOperationDetector
{
    public RepositoryOperation Detect(RepositoryLocation location)
    {
        var gitDirectory = location.GitDirectory;
        if (Directory.Exists(Path.Combine(gitDirectory, "rebase-merge")))
        {
            return RepositoryOperation.Rebase;
        }

        if (Directory.Exists(Path.Combine(gitDirectory, "rebase-apply")))
        {
            if (File.Exists(Path.Combine(gitDirectory, "rebase-apply", "applying")))
            {
                return RepositoryOperation.ApplyMailbox;
            }

            return RepositoryOperation.Rebase;
        }

        if (File.Exists(Path.Combine(gitDirectory, "MERGE_HEAD")))
        {
            return RepositoryOperation.Merge;
        }

        if (File.Exists(Path.Combine(gitDirectory, "CHERRY_PICK_HEAD")))
        {
            return RepositoryOperation.CherryPick;
        }

        if (File.Exists(Path.Combine(gitDirectory, "REVERT_HEAD")))
        {
            return RepositoryOperation.Revert;
        }

        var sequencerTodo = Path.Combine(gitDirectory, "sequencer", "todo");
        if (File.Exists(sequencerTodo))
        {
            try
            {
                // Shares read, write and delete so the read never blocks git renaming over the file (Windows).
                using var reader = new StreamReader(
                    new FileStream(sequencerTodo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
                var firstCommand = string.Empty;
                while (reader.ReadLine() is { } line)
                {
                    if (line.Length > 0 && line[0] != '#')
                    {
                        firstCommand = line;
                        break;
                    }
                }

                return firstCommand.StartsWith("revert", StringComparison.Ordinal) ? RepositoryOperation.Revert : RepositoryOperation.CherryPick;
            }
            catch (IOException)
            {
                // The operation finished while we looked.
                return RepositoryOperation.None;
            }
        }

        return File.Exists(Path.Combine(gitDirectory, "BISECT_LOG")) ? RepositoryOperation.Bisect : RepositoryOperation.None;
    }
}
