namespace Gitunia.Git;

/// <summary>A parsed <c>git status</c>: the branch header, every entry in git's order, and warnings for input that was skipped.</summary>
public sealed record StatusSnapshot(StatusBranch Branch, IReadOnlyList<StatusEntry> Entries, IReadOnlyList<string> Warnings);
