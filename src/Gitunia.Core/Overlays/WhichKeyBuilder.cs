using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Turns the engine's continuations of a pending chord into popup rows.</summary>
public static class WhichKeyBuilder
{
    /// <summary>Group names by formatted prefix, taken from the action groups (#483): one vocabulary.</summary>
    private static readonly Dictionary<string, string> GroupNames = new(StringComparer.Ordinal)
    {
        ["<space>b"] = ActionGroups.Branch.WhichKeyName!,
        ["<space>g"] = ActionGroups.Merge.WhichKeyName!,
        ["<space>T"] = ActionGroups.Tag.WhichKeyName!,
        ["<space>R"] = ActionGroups.Remote.WhichKeyName!,
        ["<space>w"] = ActionGroups.Worktree.WhichKeyName!,
        ["<space>S"] = ActionGroups.Stash.WhichKeyName!,
        ["<space>t"] = ActionGroups.Tools.WhichKeyName!,
        ["<space>v"] = ActionGroups.Diff.WhichKeyName!,
    };

    /// <summary>Bound, but left out of the popup to keep it short: variants and shortcuts of listed entries.</summary>
    private static readonly HashSet<ActionId> Unlisted =
    [
        ActionIds.MergeBranchSquash,
        ActionIds.RebaseFixup,
        ActionIds.RebaseReword,
        ActionIds.RebaseDrop,
        ActionIds.RebaseInteractiveRoot,
    ];

    /// <summary>Listed only while a merge, rebase, cherry-pick or revert is in progress (the banner shows them too).</summary>
    private static readonly HashSet<ActionId> OperationOnly =
    [
        ActionIds.OperationContinue,
        ActionIds.OperationAbort,
        ActionIds.OperationSkip,
    ];

    /// <summary>Rows for the continuations of <paramref name="pending"/>; a group row names its contents.</summary>
    /// <param name="continuations">What the engine offers after the pending keys.</param>
    /// <param name="pending">The keys typed so far.</param>
    /// <param name="operationInProgress">Whether the active tab has an operation stopped (lists continue, abort, skip).</param>
    public static IReadOnlyList<WhichKeyRow> Build(
        IReadOnlyList<Continuation> continuations,
        KeySequence pending,
        bool operationInProgress = false)
    {
        return [.. continuations
            .Where(continuation => continuation.Action is not { } action
                || (!Unlisted.Contains(action) && (operationInProgress || !OperationOnly.Contains(action))))
            .Select(continuation => (
                Continuation: continuation,
                Label: KeyFormatter.BreakLigatures(KeyFormatter.Format(continuation.Key))))
            .OrderBy(item => item.Continuation.Action is null)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.Label, StringComparer.Ordinal)
            .Select(item => new WhichKeyRow(item.Label, DescriptionOf(item.Continuation, pending)))];
    }

    /// <summary>The formatted prefixes that carry a group name.</summary>
    public static IReadOnlyCollection<string> GroupPrefixes => GroupNames.Keys;

    private static string DescriptionOf(Continuation continuation, KeySequence pending)
    {
        if (continuation.Action is not { } action)
        {
            return MoreText(pending, continuation.Key);
        }

        var description = ActionCatalog.Describe(action);
        if (continuation.HasMore)
        {
            return $"{description} (+more)";
        }

        return description;
    }

    private static string MoreText(KeySequence pending, KeyEvent next)
    {
        var path = KeyFormatter.Format(new KeySequence([.. pending.Keys, next]));
        return GroupNames.TryGetValue(path, out var name) ? $"+more ({name})" : "+more";
    }
}
