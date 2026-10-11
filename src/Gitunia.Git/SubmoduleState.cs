namespace Gitunia.Git;

/// <summary>What is dirty in a submodule entry (the <c>S&lt;c&gt;&lt;m&gt;&lt;u&gt;</c> field).</summary>
/// <param name="CommitChanged">The submodule checks out a different commit than the superproject records.</param>
/// <param name="HasTrackedChanges">The submodule has modified tracked content.</param>
/// <param name="HasUntrackedChanges">The submodule has untracked files.</param>
public sealed record SubmoduleState(bool CommitChanged, bool HasTrackedChanges, bool HasUntrackedChanges);
