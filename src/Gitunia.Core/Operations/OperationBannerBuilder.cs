using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Text;
using System.Collections.Immutable;
using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Operations;

/// <summary>Builds the banner text of an in-progress operation (ux-workflows.md, workflow 4). Pure: same input, same
/// banner.</summary>
public static class OperationBannerBuilder
{
    private const string Gap = "  ";

    // The keys the banner names when no keymap is given (tests, tools); the running app passes its table (#1198).
    private const string EditorLabel = "open conflict editor";
    private const string ShortEditorLabel = "conflict editor";

    // No conflicted file is one the editor takes (delete/modify and the like): the mergetool is the way (#742).
    private const string MergetoolLabel = "open mergetool";
    private const string ShortMergetoolLabel = "mergetool";

    /// <summary>The banner for <paramref name="info"/>, null when no operation the banner handles is in
    /// progress.</summary>
    /// <param name="info">The operation, null when none.</param>
    /// <param name="conflictCount">Conflicted files in the working tree.</param>
    /// <param name="editorOpens">Whether the conflict editor takes any of the conflicted files; when not, the hint
    /// names the mergetool instead.</param>
    /// <param name="keymap">The running keymap the keys are read from; null names the default keys. An unbound action
    /// has no key text and its hint is left out.</param>
    public static OperationBanner? Build(
        OperationInfo? info,
        int conflictCount,
        bool editorOpens = true,
        KeyBindingTable? keymap = null)
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
        var continueChip = Chip(keymap, ActionIds.OperationContinue, "<space>gc", "continue");
        var abortChip = Chip(keymap, ActionIds.OperationAbort, "<space>ga", "abort");
        ImmutableArray<BannerChip> chips = info.Kind == RepositoryOperation.Merge
            ? [continueChip, abortChip]
            : [continueChip, abortChip, Chip(keymap, ActionIds.OperationSkip, "<space>gs", "skip")];
        var hasConflicts = conflictCount > 0;
        var hint = hasConflicts ? HintOf(editorOpens, isShort: false, keymap) : string.Empty;
        var shortHint = hasConflicts ? HintOf(editorOpens, isShort: true, keymap) : string.Empty;

        return new OperationBanner(title, string.Join(Gap, parts), chips, hint, shortHint);
    }

    /// <summary>Conflicted files in a tab's working tree, 0 while it is not loaded.</summary>
    public static int ConflictCount(WorkingTreeState? tree) =>
        tree?.Snapshot.Entries.Count(entry => entry.Kind == StatusEntryKind.Unmerged) ?? 0;

    /// <summary>Whether the working tree has any conflicted file.</summary>
    public static bool HasConflicts(WorkingTreeState? tree) =>
        tree?.Snapshot.Entries.Any(entry => entry.Kind == StatusEntryKind.Unmerged) ?? false;

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

    private static BannerChip Chip(KeyBindingTable? keymap, ActionId action, string defaultKeys, string label) =>
        new(KeyOrDefault(keymap, action, defaultKeys), label, action);

    private static string KeyOrDefault(KeyBindingTable? keymap, ActionId action, string defaultKeys) =>
        keymap is null ? defaultKeys : KeyTokens.KeyOf(keymap, action) ?? string.Empty;

    private static string HintOf(bool editorOpens, bool isShort, KeyBindingTable? keymap)
    {
        var (action, defaultKeys, label) = editorOpens
            ? (ActionIds.ConflictOpen, "<space>gr", isShort ? ShortEditorLabel : EditorLabel)
            : (ActionIds.ToolMergetool, "<space>em", isShort ? ShortMergetoolLabel : MergetoolLabel);
        var keys = KeyOrDefault(keymap, action, defaultKeys);

        return keys.Length == 0 ? string.Empty : $"{keys} {label}";
    }

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

        return info.Commit is { } commit ? ShortHash.Of(commit) : string.Empty;
    }

    private static string ConflictText(int count) => count switch
    {
        0 => "no conflicts left",
        1 => "1 conflict",
        _ => $"{count.ToString(CultureInfo.InvariantCulture)} conflicts",
    };
}
