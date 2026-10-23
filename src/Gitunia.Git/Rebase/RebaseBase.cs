namespace Gitunia.Git.Rebase;

/// <summary>
/// Where an interactive rebase starts: the commits after a base commit, or the whole history (<c>--root</c>).
/// </summary>
/// <param name="Onto">A commit hash or ref name that does not start with <c>-</c>; null for the root.</param>
/// <param name="KeepMerges">
/// Keep the merge commits of the range (<c>--rebase-merges</c>). A range without merge commits is read and rebased as
/// usual; without this a range with merge commits is refused.
/// </param>
public sealed record RebaseBase(string? Onto, bool KeepMerges = false)
{
    /// <summary>Rebase everything, the root commit included (<c>git rebase -i --root</c>).</summary>
    public static RebaseBase Root { get; } = new(Onto: null);

    public bool IsRoot => Onto is null;

    /// <summary>Rebase the commits of HEAD that <paramref name="onto"/> does not have.</summary>
    public static RebaseBase OntoCommit(string onto) => new(onto);

    /// <summary>The same start, keeping the merge commits of the range.</summary>
    public RebaseBase WithMerges() => this with { KeepMerges = true };
}
