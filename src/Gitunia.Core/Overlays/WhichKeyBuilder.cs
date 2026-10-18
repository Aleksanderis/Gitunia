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

    /// <summary>Rows for the continuations of <paramref name="pending"/>; a group row names its contents.</summary>
    public static IReadOnlyList<WhichKeyRow> Build(
        IReadOnlyList<Continuation> continuations,
        KeySequence pending)
    {
        return [.. continuations
            .Select(continuation => (Continuation: continuation, Label: KeyFormatter.Format(continuation.Key)))
            .OrderBy(item => item.Continuation.Action is null)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.Label, StringComparer.Ordinal)
            .Select(item => new WhichKeyRow(
                item.Label,
                item.Continuation is { Action: { } action }
                    ? ActionCatalog.Describe(action) + (item.Continuation.HasMore ? " (+more)" : string.Empty)
                    : MoreText(pending, item.Continuation.Key)))];
    }

    /// <summary>The formatted prefixes that carry a group name.</summary>
    public static IReadOnlyCollection<string> GroupPrefixes => GroupNames.Keys;

    private static string MoreText(KeySequence pending, KeyEvent next)
    {
        var path = KeyFormatter.Format(new KeySequence([.. pending.Keys, next]));
        return GroupNames.TryGetValue(path, out var name) ? $"+more ({name})" : "+more";
    }
}
