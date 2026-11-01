namespace Gitunia.Core.Running;

/// <summary>
/// The one table of which running operations of a tab keep another kind from starting. A feature reducer asks
/// <see cref="Excludes(RunningOperations, RunningOperationKind)"/> for its own kind; no feature lists the other kinds.
/// </summary>
public static class OperationExclusion
{
    // The operations that write the index or the working tree of the tab (the editors included): any two exclude each
    // other, because they race on index.lock and on files.
    private static readonly RunningOperationKind[] _treeWriters =
    [
        RunningOperationKind.Branches,
        RunningOperationKind.Commit,
        RunningOperationKind.Conflicts,
        RunningOperationKind.Discard,
        RunningOperationKind.Integration,
        RunningOperationKind.Pick,
        RunningOperationKind.Rebase,
        RunningOperationKind.Staging,
        RunningOperationKind.Stash,
    ];

    /// <summary>The kind writes the index or the working tree (any sync is counted by the sync rule: the table cannot tell a pull from a fetch).</summary>
    public static bool WritesWorkingTree(RunningOperationKind kind) => _treeWriters.Contains(kind);

    /// <summary>
    /// Another operation of the tab keeps an operation of <paramref name="kind"/> from starting. Rules: tree writers
    /// exclude each other and a running sync (a pull writes the tree); remote management and tags wait for a sync; a
    /// sync waits for every other operation. The kind's own running mark is not asked here.
    /// </summary>
    public static bool Excludes(this RunningOperations running, RunningOperationKind kind)
    {
        foreach (var item in running.Items)
        {
            if (item.Kind != kind && IsExcludedBy(kind, item.Kind))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A running operation of kind <paramref name="running"/> keeps <paramref name="kind"/> from starting.</summary>
    public static bool IsExcludedBy(RunningOperationKind kind, RunningOperationKind running)
    {
        if (kind == RunningOperationKind.Sync)
        {
            return running != RunningOperationKind.Sync;
        }

        if (running == RunningOperationKind.Sync)
        {
            return WritesWorkingTree(kind)
                || kind == RunningOperationKind.Remotes
                || kind == RunningOperationKind.Tags;
        }

        return WritesWorkingTree(kind) && WritesWorkingTree(running);
    }
}
