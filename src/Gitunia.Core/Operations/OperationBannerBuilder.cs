using System.Collections.Immutable;
using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Operations;

/// <summary>Builds the banner text of an in-progress operation (ux.md, workflow 4). Pure: same input, same
/// banner.</summary>
public static class OperationBannerBuilder
{
    private const int ShortHashLength = 7;
    private const string Gap = "  ";

    // The editor keys are the ConflictOpen bindings of the Unstaged view; the text is not rebuilt from the keymap
    // because the banner is plain data.
    private const string EditorHint = "Unstaged: Enter, <space>to open conflict editor";
    private const string ShortEditorHint = "Unstaged: Enter conflict editor";

    private static readonly BannerChip Continue = new("<space>gc", "continue", ActionIds.OperationContinue);
    private static readonly BannerChip Abort = new("<space>ga", "abort", ActionIds.OperationAbort);
    private static readonly BannerChip Skip = new("<space>gk", "skip", ActionIds.OperationSkip);
    private static readonly ImmutableArray<BannerChip> MergeChips = [Continue, Abort];
    private static readonly ImmutableArray<BannerChip> SkippableChips = [Continue, Abort, Skip];

    /// <summary>The banner for <paramref name="info"/>, null when no operation the banner handles is in
    /// progress.</summary>
    /// <param name="info">The operation, null when none.</param>
    /// <param name="conflictCount">Conflicted files in the working tree.</param>
    public static OperationBanner? Build(OperationInfo? info, int conflictCount)
    {
        if (info is null || Verb(info.Kind) is not { } verb)
        {
            return null;
        }

        var subject = SubjectOf(info);
        var title = subject.Length > 0 ? $"{verb} {subject}" : verb;
        var parts = new List<string>();
        if (info.Step is { } step && info.Total is { } total)
        {
            var stepText = step.ToString(CultureInfo.InvariantCulture);
            parts.Add($"step {stepText}/{total.ToString(CultureInfo.InvariantCulture)}");
        }
        else if (info.Remaining is > 1 and var remaining)
        {
            parts.Add($"{remaining.ToString(CultureInfo.InvariantCulture)} commits left");
        }

        parts.Add(ConflictText(conflictCount));
        var chips = info.Kind == RepositoryOperation.Merge ? MergeChips : SkippableChips;
        var hasConflicts = conflictCount > 0;
        var hint = hasConflicts ? EditorHint : string.Empty;
        var shortHint = hasConflicts ? ShortEditorHint : string.Empty;

        return new OperationBanner(title, string.Join(Gap, parts), chips, hint, shortHint);
    }

    /// <summary>Conflicted files in a tab's working tree, 0 while it is not loaded.</summary>
    public static int ConflictCount(WorkingTreeState? tree) =>
        tree?.Snapshot.Entries.Count(entry => entry.Kind == StatusEntryKind.Unmerged) ?? 0;

    /// <summary>The name of the operation in the title and in messages, such as <c>rebase</c> or
    /// <c>cherry-pick</c>.</summary>
    public static string NameOf(RepositoryOperation operation) => operation switch
    {
        RepositoryOperation.Merge => "merge",
        RepositoryOperation.Rebase => "rebase",
        RepositoryOperation.CherryPick => "cherry-pick",
        RepositoryOperation.Revert => "revert",
        _ => operation.ToString().ToLowerInvariant(),
    };

    private static string? Verb(RepositoryOperation operation) => operation switch
    {
        RepositoryOperation.Merge => "MERGING",
        RepositoryOperation.Rebase => "REBASING",
        RepositoryOperation.CherryPick => "CHERRY-PICKING",
        RepositoryOperation.Revert => "REVERTING",
        _ => null,
    };

    private static string SubjectOf(OperationInfo info)
    {
        if (info.Kind is RepositoryOperation.Merge or RepositoryOperation.Rebase)
        {
            return info.Target ?? string.Empty;
        }

        return info.Commit is { } commit ? commit[..Math.Min(ShortHashLength, commit.Length)] : string.Empty;
    }

    private static string ConflictText(int count) => count switch
    {
        0 => "no conflicts left",
        1 => "1 conflict",
        _ => $"{count.ToString(CultureInfo.InvariantCulture)} conflicts",
    };
}
