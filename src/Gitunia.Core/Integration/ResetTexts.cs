using System.Collections.Immutable;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Reset;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Integration;

/// <summary>The words of the reset: the mode prompt, the two Confirm dialogs and the status line. Pure, so every
/// text is testable without a terminal.</summary>
public static class ResetTexts
{
    /// <summary>Shown when no commit, branch or tag is selected.</summary>
    public const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) to reset to";

    /// <summary>Shown when a reset is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: the last reset is still running";

    /// <summary>Shown when the dialog does not fit the screen.</summary>
    public const string TooSmallNotice = "The screen is too small to ask: nothing was reset";

    private const int ListedFiles = 6;

    /// <summary>The options of the mode prompt, each picked by one key.</summary>
    public static ImmutableArray<PromptKeyChoice> ModeChoices { get; } =
    [
        new("s", "soft: keep the index and the files", ResetModeAnswer.Soft),
        new("m", "mixed: keep the files, clear the index", ResetModeAnswer.Mixed),
        new("h", "hard: discard the changes", ResetModeAnswer.Hard),
    ];

    /// <summary>The name of a stopped operation in the refusal.</summary>
    public static string OperationName(RepositoryOperation operation) => operation switch
    {
        RepositoryOperation.CherryPick => "cherry-pick",
        RepositoryOperation.ApplyMailbox => "mailbox apply",
        _ => operation.ToString().ToLowerInvariant(),
    };

    /// <summary>Why a reset must not start while an operation is stopped.</summary>
    public static string OperationRefusal(RepositoryOperation operation) =>
        $"A {OperationName(operation)} is in progress: finish or abort the {OperationName(operation)} first";

    /// <summary>The git command that moves the branch back to <paramref name="oldHash"/>.</summary>
    public static string UndoCommand(ResetMode mode, string oldHash) =>
        mode == ResetMode.Hard ? $"git reset --hard {oldHash}" : $"git reset {OptionOf(mode)} {oldHash}";

    /// <summary>The status line after a reset: where the branch is and where it was.</summary>
    public static string Done(ResetTarget target, string oldHash) =>
        $"Reset {target.BranchName} to {ShortHash.Of(target.CommitHash)} ({ModeName(target.Mode)}), " +
        $"was {ShortHash.Of(oldHash)}";

    /// <summary>The message history detail after a reset: the command that goes back.</summary>
    public static string DoneDetail(ResetTarget target, string oldHash) =>
        UndoNotes.Undo($"{UndoCommand(target.Mode, oldHash)} (or ORIG_HEAD, the reflog)");

    /// <summary>The status line when the status could not be read.</summary>
    public static string StatusFailure(string reason) =>
        $"Could not read the status, nothing was reset: {reason}";

    /// <summary>The status line when git refused the reset.</summary>
    public static string Failure(string reason) => $"Could not reset: {reason}";

    /// <summary>Whether a tracked change is among what a mixed or hard reset reports.</summary>
    public static bool IsTracked(StatusEntry entry) =>
        entry.Kind is StatusEntryKind.Ordinary or StatusEntryKind.Renamed or StatusEntryKind.Copied
            or StatusEntryKind.Unmerged;

    /// <summary>The dialog of a hard reset: what is overwritten and the undo.</summary>
    public static ConfirmPrompt HardPrompt(ResetTarget target, ImmutableArray<StatusEntry> changes)
    {
        List<string> lines =
        [
            $"Reset {target.BranchName} to {ShortHash.Of(target.CommitHash)} with --hard: " +
            "HEAD, the index and the tracked files move to that commit.",
        ];
        if (changes.IsEmpty)
        {
            lines.Add("There are no uncommitted changes to lose.");
        }
        else
        {
            lines.Add($"{CountText.Plural(changes.Length, "file")} with uncommitted changes will be overwritten:");
            lines.AddRange(changes.Take(ListedFiles).Select(entry => $"  {entry.Path}"));
            if (changes.Length > ListedFiles)
            {
                lines.Add($"  and {changes.Length - ListedFiles} more");
            }
        }

        lines.Add("Untracked files stay unless the target commit tracks a file at the same path.");

        return new ConfirmPrompt(
            "Hard reset",
            lines,
            $"The commits can be brought back afterwards from {UndoNotes.Reflog}. " +
            "Uncommitted changes are never in the reflog and cannot be restored.",
            "Reset hard");
    }

    /// <summary>The dialog of a mixed reset that clears staged changes.</summary>
    public static ConfirmPrompt MixedPrompt(ResetTarget target, ImmutableArray<StatusEntry> changes)
    {
        var staged = changes.Count(entry => entry.IsStaged);
        var partial = changes.Count(entry => entry is { IsStaged: true, IsUnstaged: true });
        var detail = partial == 0 ? string.Empty : $" ({partial} partially staged)";

        return new ConfirmPrompt(
            "Mixed reset",
            [
                $"Reset {target.BranchName} to {ShortHash.Of(target.CommitHash)} with --mixed: the index is cleared; " +
                "the files in the working tree stay.",
                $"{CountText.Plural(staged, "file")} {(staged == 1 ? "loses" : "lose")} the staged state{detail}.",
            ],
            "The staged state cannot be recovered from the reflog. The commits can be brought back afterwards " +
            $"from {UndoNotes.Reflog}.",
            "Reset mixed");
    }

    private static string OptionOf(ResetMode mode) => mode switch
    {
        ResetMode.Soft => "--soft",
        ResetMode.Hard => "--hard",
        _ => "--mixed",
    };

    private static string ModeName(ResetMode mode) => OptionOf(mode)[2..];
}
