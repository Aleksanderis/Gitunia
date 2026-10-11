namespace Gitunia.Git;

/// <summary>The parts of a <see cref="RepositorySnapshot"/>. A refresh result names the sections whose data actually differs, so the UI redraws only those and keeps scroll and selection elsewhere.</summary>
[Flags]
public enum RepositorySections
{
    None = 0,
    Head = 1,
    Refs = 2,
    Worktrees = 4,
    Status = 8,
    Operation = 16,
    All = Head | Refs | Worktrees | Status | Operation,
}
