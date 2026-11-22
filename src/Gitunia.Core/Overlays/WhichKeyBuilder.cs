using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Turns the engine's continuations of a pending chord into popup rows.</summary>
public static class WhichKeyBuilder
{
    /// <summary>Group names by formatted prefix: one noun per chord group (#840), spelled out here.</summary>
    private static readonly Dictionary<string, string> GroupNames = new(StringComparer.Ordinal)
    {
        ["<space>b"] = "branch",
        ["<space>br"] = "rebase",
        ["<space>t"] = "tag",
        ["<space>r"] = "remote",
        ["<space>w"] = "worktree",
        ["<space>k"] = "stash",
        ["<space>o"] = "repositories and tabs, Ctrl-t picker",
        ["<space>g"] = "operation and conflicts",
        ["<space>e"] = "external tools",
        ["<space>v"] = "view options",
    };

    private const string MoreKey = "+";
    private const string VariantsText = "+more (variants and shortcuts)";

    private sealed record Item(Continuation Continuation, string Label);

    /// <summary>Bound, but listed after a +more row to keep the main list short: variants and shortcuts of listed entries.</summary>
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

    /// <summary>Whether the which-key popup lists <paramref name="action"/> given the context of the active tab.</summary>
    /// <param name="action">The bound action.</param>
    /// <param name="operationInProgress">Whether the active tab has an operation stopped.</param>
    /// <param name="hasConflicts">Whether the active tab's working tree has conflicted files, with or without an
    /// operation (a stash pop leaves them too); the conflict editor is listed then.</param>
    public static bool IsAvailable(ActionId action, bool operationInProgress, bool hasConflicts = false)
    {
        if (action == ActionIds.ConflictOpen)
        {
            return hasConflicts;
        }

        return operationInProgress || !OperationOnly.Contains(action);
    }

    /// <summary>Rows for the continuations of <paramref name="pending"/>; a group row names its contents.</summary>
    /// <param name="continuations">What the engine offers after the pending keys.</param>
    /// <param name="pending">The keys typed so far.</param>
    /// <param name="operationInProgress">Whether the active tab has an operation stopped (lists continue, abort, skip).</param>
    /// <param name="hasConflicts">Whether the active tab has conflicted files (lists the conflict editor).</param>
    public static IReadOnlyList<WhichKeyRow> Build(
        IReadOnlyList<Continuation> continuations,
        KeySequence pending,
        bool operationInProgress = false,
        bool hasConflicts = false)
    {
        var items = continuations
            .Where(continuation => continuation.Action is not { } action || IsAvailable(action, operationInProgress, hasConflicts))
            .Select(continuation => new Item(
                continuation,
                KeyFormatter.BreakLigatures(KeyFormatter.Format(continuation.Key))))
            .OrderBy(item => item.Continuation.Action is null)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.Label, StringComparer.Ordinal)
            .ToList();
        var listed = items.Where(item => item.Continuation.Action is not { } action || !Unlisted.Contains(action)).ToList();
        var hidden = items.Except(listed).ToList();
        var actions = listed.Where(item => item.Continuation.Action is not null).ToList();

        var rows = new List<WhichKeyRow>();
        string? previous = null;
        foreach (var entry in ActionClusters.Arrange(actions, item => item.Continuation.Action!.Value))
        {
            rows.Add(new WhichKeyRow(
                entry.Item.Label,
                DescriptionOf(entry.Item.Continuation, pending),
                rows.Count > 0 && entry.Cluster != previous));
            previous = entry.Cluster;
        }

        rows.AddRange(listed
            .Where(item => item.Continuation.Action is null)
            .Select(item => new WhichKeyRow(item.Label, DescriptionOf(item.Continuation, pending))));

        // The variants and shortcuts stay out of the main list; they follow a +more row (#838).
        if (hidden.Count > 0)
        {
            rows.Add(new WhichKeyRow(MoreKey, VariantsText));
            rows.AddRange(hidden.Select(item => new WhichKeyRow(item.Label, DescriptionOf(item.Continuation, pending))));
        }

        return rows;
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
