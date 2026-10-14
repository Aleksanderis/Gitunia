using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Repositories;

/// <summary>Detects a stopped multi-step operation (rebase, merge, ...) of a worktree.</summary>
public static class RepositoryOperationDetector
{
    /// <summary>Detects a stopped multi-step operation from the state files in this worktree's git directory.</summary>
    public static RepositoryOperation Detect(RepositoryLocation location)
    {
        var gitDirectory = location.GitDirectory;
        if (Directory.Exists(Path.Combine(gitDirectory, "rebase-merge")))
        {
            return RepositoryOperation.Rebase;
        }

        if (Directory.Exists(Path.Combine(gitDirectory, "rebase-apply")))
        {
            return File.Exists(Path.Combine(gitDirectory, "rebase-apply", "applying"))
                ? RepositoryOperation.ApplyMailbox
                : RepositoryOperation.Rebase;
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
                var firstCommand = File.ReadLines(sequencerTodo).FirstOrDefault(line => line.Length > 0 && line[0] != '#') ?? string.Empty;
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
