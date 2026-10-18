using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>Turns the engine's continuations of a pending chord into popup rows.</summary>
public static class WhichKeyBuilder
{
    /// <summary>Group names by formatted prefix; hand-written, the keymap has no group metadata (#443).</summary>
    private static readonly Dictionary<string, string> GroupNames = new(StringComparer.Ordinal)
    {
        ["<space>b"] = "branch operations",
        ["<space>g"] = "merge, rebase, reset, pick",
        ["<space>T"] = "tag operations",
        ["<space>w"] = "worktree operations",
        ["<space>S"] = "stash operations",
        ["<space>t"] = "external tools",
        ["<space>v"] = "diff view options",
    };

    /// <summary>Rows for the continuations of <paramref name="pending"/>; a group row names its contents.</summary>
    public static IReadOnlyList<WhichKeyRow> Build(
        IReadOnlyList<Continuation> continuations,
        KeySequence pending)
    {
        return [.. continuations
            .Select(continuation => (Continuation: continuation, Label: KeyFormatter.Format(continuation.Key)))
            .OrderBy(item => item.Continuation.Action is null)
            .ThenBy(item => item.Label, LabelComparer.Instance)
            .Select(item => item.Continuation)
            .Select(continuation => new WhichKeyRow(
            KeyFormatter.Format(continuation.Key),
            continuation is { Action: { } action }
                ? ActionCatalog.Describe(action) + (continuation.HasMore ? " (+more)" : string.Empty)
                : MoreText(pending, continuation.Key)))];
    }

    /// <summary>Alphabetical by label, lowercase before uppercase of the same letter.</summary>
    private sealed class LabelComparer : IComparer<string>
    {
        public static readonly LabelComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var folded = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            return folded != 0 ? folded : string.CompareOrdinal(y, x);
        }
    }

    /// <summary>The formatted prefixes that carry a group name.</summary>
    public static IReadOnlyCollection<string> GroupPrefixes => GroupNames.Keys;

    private static string MoreText(KeySequence pending, KeyEvent next)
    {
        var path = KeyFormatter.Format(new KeySequence([.. pending.Keys, next]));
        return GroupNames.TryGetValue(path, out var name) ? $"+more ({name})" : "+more";
    }
}
