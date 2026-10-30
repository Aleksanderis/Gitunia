using Gitunia.Core.Editing;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;

namespace Gitunia.Core.Rebase;

/// <summary>
/// The open todo editor of an interactive rebase (ux-workflows.md, workflow 3, "Interactive rebase"): the commits of the range in
/// replay order (the first row is replayed first, as in git's own todo), the action of each, the highlighted row and
/// the message editor of a reword. Pure: it never starts git; <see cref="ToEntries"/> is what the engine takes.
/// </summary>
/// <param name="TabId">The tab the editor was opened in; the rebase runs there.</param>
/// <param name="BranchName">The branch being rebased.</param>
/// <param name="TargetLabel">The commit, branch or tag the commits are replayed onto, as the user chose it.</param>
/// <param name="Base">Where the range starts.</param>
/// <param name="OldTip">HEAD when the editor opened: the undo hint, and the check that nothing moved.</param>
/// <param name="Rows">The todo as edited.</param>
/// <param name="Original">The todo as read, to tell whether anything changed.</param>
/// <param name="SelectedIndex">Index into <paramref name="Rows"/>.</param>
/// <param name="Edit">The open message editor, null when the list is shown.</param>
/// <param name="IsDiscardPending">The first <c>Esc</c> on an edited todo was pressed; the next one closes it.</param>
public sealed record RebaseEditorState(
    TabId TabId,
    string BranchName,
    string TargetLabel,
    RebaseBase Base,
    string OldTip,
    IReadOnlyList<RebaseEditorRow> Rows,
    IReadOnlyList<RebaseEditorRow> Original,
    int SelectedIndex = 0,
    RebaseMessageEdit? Edit = null,
    bool IsDiscardPending = false)
{
    /// <summary>The state for a freshly read range: every commit is a pick.</summary>
    public static RebaseEditorState Create(
        TabId tabId,
        string branchName,
        string targetLabel,
        RebaseBase rebaseBase,
        string oldTip,
        IReadOnlyList<RebaseTodoCommit> commits)
    {
        var rows = commits
            .Select(commit => new RebaseEditorRow(
                commit.Hash,
                commit.Subject,
                commit.Action,
                Argument: commit.Argument))
            .ToList();
        return new RebaseEditorState(tabId, branchName, targetLabel, rebaseBase, oldTip, rows, rows);
    }

    /// <summary>The highlighted row, null for an empty todo.</summary>
    public RebaseEditorRow? Selected =>
        Rows.Count == 0 ? null : Rows[Math.Clamp(SelectedIndex, 0, Rows.Count - 1)];

    /// <summary>True when the order, an action or a message differs from the todo as read.</summary>
    public bool IsModified => !Rows.Select(Effective).SequenceEqual(Original.Select(Effective));

    /// <summary>True when the message editor is open.</summary>
    public bool IsEditingMessage => Edit is not null;

    /// <summary>Commits the rebase keeps (everything but drops).</summary>
    public int KeptCount => Rows.Count(row => !row.IsStructure && row.Action != RebaseTodoAction.Drop);

    /// <summary>Commits the rebase removes.</summary>
    public int DroppedCount => Rows.Count(row => row.Action == RebaseTodoAction.Drop);

    /// <summary>The rows that are commits (label and reset lines are not).</summary>
    public int CommitCount => Rows.Count(row => !row.IsStructure);

    /// <summary>Why the todo cannot be started, null when it can.</summary>
    public string? Problem
    {
        get
        {
            var first = Rows.FirstOrDefault(row => !row.IsStructure && row.Action != RebaseTodoAction.Drop);
            if (first?.Action is RebaseTodoAction.Squash or RebaseTodoAction.Fixup)
            {
                return "The first commit that is kept cannot be a squash or a fixup: it has nothing to meld into";
            }

            return RebaseTodoValidator.FindProblem(ToEntries());
        }
    }

    /// <summary>The todo in the form the engine takes, in replay order.</summary>
    public IReadOnlyList<RebaseTodoEntry> ToEntries() =>
        [
            .. Rows.Select(row => new RebaseTodoEntry(
                row.Hash,
                row.Action,
                row.Action == RebaseTodoAction.Reword ? row.Message : null,
                row.Argument)),
        ];

    /// <summary>Moves the highlight to <paramref name="index"/>, clamped to the rows.</summary>
    public RebaseEditorState MoveTo(int index) =>
        this with
        {
            SelectedIndex = Rows.Count == 0 ? 0 : Math.Clamp(index, 0, Rows.Count - 1),
            IsDiscardPending = false,
        };

    /// <summary>
    /// Moves the highlighted commit <paramref name="delta"/> rows, -1 replaying it earlier (up) and 1 later (down),
    /// stopping at the ends; the highlight follows the commit. At an end nothing moves.
    /// </summary>
    public RebaseEditorState MoveRow(int delta)
    {
        if (Rows.Count == 0 || SelectedIndex < 0 || SelectedIndex >= Rows.Count)
        {
            return this;
        }

        var target = Math.Clamp(SelectedIndex + delta, 0, Rows.Count - 1);
        if (target == SelectedIndex)
        {
            return this;
        }

        var rows = Rows.ToList();
        var row = rows[SelectedIndex];
        rows.RemoveAt(SelectedIndex);
        rows.Insert(target, row);
        return this with { Rows = rows, SelectedIndex = target, IsDiscardPending = false };
    }

    /// <summary>Sets the action of the highlighted commit; a reword needs a message (<see cref="SaveEdit"/>).</summary>
    public RebaseEditorState SetAction(RebaseTodoAction action)
    {
        if (Selected is not { IsEditable: true } row || action == RebaseTodoAction.Reword)
        {
            return this;
        }

        var rows = Rows.ToList();
        rows[Math.Clamp(SelectedIndex, 0, Rows.Count - 1)] = row with { Action = action };
        return this with { Rows = rows, IsDiscardPending = false };
    }

    /// <summary>Opens the message editor on the highlighted commit with <paramref name="message"/> as text.</summary>
    public RebaseEditorState BeginEdit(string message)
    {
        if (Selected is not { IsEditable: true } row)
        {
            return this;
        }

        var editor = new MessageEditor();
        editor.SetText(message);
        return this with { Edit = new RebaseMessageEdit(row.Hash, editor), IsDiscardPending = false };
    }

    /// <summary>Closes the message editor and keeps the previous action and message.</summary>
    public RebaseEditorState CancelEdit() => this with { Edit = null };

    /// <summary>
    /// Makes the commit being edited a reword with the typed message and closes the editor; a blank message changes
    /// nothing (git refuses it), the editor stays open.
    /// </summary>
    public RebaseEditorState SaveEdit()
    {
        if (Edit is not { } edit || edit.Editor.IsBlank)
        {
            return this;
        }

        var index = Rows.ToList().FindIndex(row => row.Hash == edit.Hash);
        if (index < 0)
        {
            return CancelEdit();
        }

        var rows = Rows.ToList();
        rows[index] = rows[index] with { Action = RebaseTodoAction.Reword, Message = edit.Editor.Text.TrimEnd() };
        return this with { Rows = rows, Edit = null };
    }

    /// <summary>Remembers that the user asked to close an edited todo; the next close really closes.</summary>
    public RebaseEditorState AskDiscard() => this with { IsDiscardPending = true };

    // What the engine would get: a message only counts for a reword, so a kept but unused message is no edit.
    private static RebaseEditorRow Effective(RebaseEditorRow row) =>
        row.Action == RebaseTodoAction.Reword ? row : row with { Message = null };
}
