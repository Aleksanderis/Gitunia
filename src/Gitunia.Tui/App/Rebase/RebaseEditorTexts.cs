using Gitunia.Core.Overlays;
using Gitunia.Core.Rebase;
using Gitunia.Core.Status;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Rebase;

/// <summary>The words of the interactive rebase: its Confirm and the report after it ran. Pure.</summary>
public static class RebaseEditorTexts
{
    private const int ShortHashLength = 7;
    private const int NamedDropLimit = 5;

    /// <summary>The Confirm before the rebase starts: what is kept, which commits are dropped, the undo.</summary>
    /// <param name="state">The editor state.</param>
    /// <param name="current">The checked-out branch.</param>
    /// <param name="autoStashNote">The line about carried uncommitted changes, null for none.</param>
    public static ConfirmPrompt ConfirmFor(RebaseEditorState state, GitRef? current, string? autoStashNote)
    {
        List<string> lines =
        [
            $"Rebase {state.BranchName} onto {state.TargetLabel} with this todo?",
            Summary(state),
            "The kept commits are replayed in this order and get new hashes.",
        ];
        var dropped = state.Rows.Where(row => row.Action == RebaseTodoAction.Drop).ToList();
        if (dropped.Count > 0)
        {
            lines.Add($"Dropped from {state.BranchName}:");
            lines.AddRange(dropped.Take(NamedDropLimit).Select(row => $"  {ShortHash(row.Hash)} {row.Subject}"));
            if (dropped.Count > NamedDropLimit)
            {
                lines.Add($"  and {dropped.Count - NamedDropLimit} more");
            }
        }

        if (RebaseGuard.UpstreamWarning(current) is { } warning)
        {
            lines.Add(warning);
        }

        if (autoStashNote is not null)
        {
            lines.Add(autoStashNote);
        }

        return new ConfirmPrompt(
            "Interactive rebase",
            lines,
            $"Undo: git reset --keep {state.OldTip} (also ORIG_HEAD, the reflog). A stop on conflicts leaves a " +
            "rebase in progress: <space>gc continues, <space>ga aborts.",
            "Rebase");
    }

    /// <summary>The status line after the engine returned.</summary>
    public static OperationReport ReportOf(RebaseEditorState state, InteractiveRebaseResult result)
    {
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new OperationReport(
                $"Rebase of {state.BranchName} onto {state.TargetLabel} stopped on conflicts: resolve the files " +
                $"marked !, then <space>gc (continue), <space>ga (abort, back to {ShortHash(result.OriginalHead)})",
                StatusSeverity.Warning);
        }

        return new OperationReport(
            $"Rebased {state.BranchName} onto {state.TargetLabel} interactively " +
            $"({ShortHash(result.Head ?? string.Empty)}), was {ShortHash(result.OriginalHead)}; " +
            $"undo: git reset --keep {result.OriginalHead} (or ORIG_HEAD, the reflog)",
            StatusSeverity.Info);
    }

    private static string Summary(RebaseEditorState state)
    {
        var total = OperationText.Plural(state.CommitCount, "commit");
        var parts = new List<string> { $"{state.KeptCount} of {total} kept" };
        AddCount(parts, state, RebaseTodoAction.Reword, "reworded");
        AddCount(parts, state, RebaseTodoAction.Squash, "squashed");
        AddCount(parts, state, RebaseTodoAction.Fixup, "fixed up");
        if (state.DroppedCount > 0)
        {
            parts.Add($"{state.DroppedCount} dropped");
        }

        return string.Join(", ", parts);
    }

    private static void AddCount(List<string> parts, RebaseEditorState state, RebaseTodoAction action, string word)
    {
        var count = state.Rows.Count(row => row.Action == action);
        if (count > 0)
        {
            parts.Add($"{count} {word}");
        }
    }

    private static string ShortHash(string hash) => hash[..Math.Min(ShortHashLength, hash.Length)];
}
