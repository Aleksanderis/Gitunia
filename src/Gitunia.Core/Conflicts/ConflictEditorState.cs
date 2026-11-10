using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// The open conflict editor (ux-workflows.md, workflow 4, "Conflict editor"), modelled on KDiff3: the whole file in four panes,
/// the base, ours and theirs side by side (read-only, rows aligned by <see cref="ConflictAlignment"/>) and the result
/// below, a real text editor over the whole file (<see cref="ConflictResultBuffer"/>). A cursor moves in the active
/// pane; on a conflict a pick copies that version into the result, in a Visual selection only the selected lines; the
/// result accepts typing at any time. A conflict nobody decided keeps its conflict markers in the result, so a partial
/// save leaves the file conflicted exactly there. Every pick, reset and typing session is one step of
/// <see cref="Undo"/> and <see cref="Redo"/>. Pure apart from the result buffer's own text editor; it never reads or
/// writes a file (<see cref="ToText"/> is what the save writes).
/// </summary>
/// <param name="TabId">The tab the editor was opened in; the save runs there.</param>
/// <param name="Path">The conflicted path, relative to the working tree root.</param>
/// <param name="Parts">The file in order: common text and conflicts.</param>
/// <param name="Hunks">The conflicts of <paramref name="Parts"/> in order.</param>
/// <param name="Alignment">The rows of the three version panes.</param>
/// <param name="Result">The result pane's text and where each conflict sits in it.</param>
/// <param name="Format">Line endings of the file, kept on save.</param>
/// <param name="ActivePane">The pane the cursor is in.</param>
/// <param name="TopPane">The version pane the cursor was last in (where <c>Ctrl-k</c> returns from the result).</param>
/// <param name="Row">
/// The cursor's aligned row in the version panes (follows the result cursor while that is active).
/// </param>
/// <param name="SelectedIndex">The current conflict: the one the cursor is in or was last in.</param>
/// <param name="Anchor">The aligned row where a Visual selection started, null when none.</param>
/// <param name="TopScroll">The first aligned row shown in the version panes.</param>
/// <param name="ResultScroll">The first result line shown.</param>
/// <param name="IsDiscardPending">The first <c>Esc</c> on a changed file was pressed; the next one closes it.</param>
/// <param name="IsEditing">The result pane is in Insert mode.</param>
/// <param name="UndoStack">What <see cref="Undo"/> restores, the latest change last.</param>
/// <param name="RedoStack">What <see cref="Redo"/> restores, the latest undone change last.</param>
/// <param name="HorizontalScroll">
/// Columns the text of the base, ours and theirs panes is scrolled to the left, together (0 shows the line starts).
/// </param>
/// <param name="Yank">What the editor last copied or deleted as a line, put back by <c>p</c> and <c>P</c>.</param>
public sealed partial record ConflictEditorState(
    TabId TabId,
    string Path,
    IReadOnlyList<ConflictPart> Parts,
    IReadOnlyList<ConflictHunk> Hunks,
    ConflictAlignment Alignment,
    ConflictResultBuffer Result,
    ConflictFileFormat Format,
    ConflictPane ActivePane = ConflictPane.Ours,
    ConflictPane TopPane = ConflictPane.Ours,
    int Row = 0,
    int SelectedIndex = 0,
    int? Anchor = null,
    int TopScroll = 0,
    int ResultScroll = 0,
    bool IsDiscardPending = false,
    bool IsEditing = false,
    IReadOnlyList<ConflictSnapshot>? UndoStack = null,
    IReadOnlyList<ConflictSnapshot>? RedoStack = null,
    int HorizontalScroll = 0,
    ConflictYank? Yank = null)
{
    /// <summary>The most changes one session remembers; older ones are forgotten.</summary>
    public const int UndoLimit = 100;

    /// <summary>Columns one sideways scroll key press or wheel notch moves the version panes.</summary>
    public const int SidewaysStep = 8;

    private const int CenterDivisor = 3;

    private static readonly ConflictPane[] PaneOrder =
        [ConflictPane.Base, ConflictPane.Ours, ConflictPane.Theirs, ConflictPane.Result];

    /// <summary>The columns the version panes can be scrolled sideways at most: the widest line, less one.</summary>
    public int MaxHorizontalScroll => Math.Max(0, Alignment.WidestLineColumns - 1);

    /// <summary>The state for a freshly read file: nothing is picked and the cursor is on the first conflict.</summary>
    public static ConflictEditorState Create(TabId tabId, string path, IReadOnlyList<ConflictPart> parts)
    {
        var hunks = parts.Where(part => part.Hunk is not null).Select(part => part.Hunk!).ToList();
        var alignment = ConflictAlignment.Create(parts);
        var result = ConflictResultBuffer.Create(parts);
        var format = ConflictFileFormat.Of(TextOf(parts));
        var row = hunks.Count == 0 ? 0 : alignment.FirstRowOf(0);

        return new ConflictEditorState(tabId, path, parts, hunks, alignment, result, format, Row: row);
    }

    /// <summary>The conflict under the cursor (the current one), null for a file with none.</summary>
    public ConflictHunk? Selected => Hunks.Count == 0 ? null : Hunks[Index];

    /// <summary>The pick of the current conflict.</summary>
    public ConflictPick SelectedPick => Hunks.Count == 0 ? ConflictPick.None : Result.Regions[Index].Pick;

    /// <summary>Conflicts that are settled.</summary>
    public int ResolvedCount => Hunks.Count - UnresolvedCount;

    /// <summary>Conflicts still without a pick, or typed on but still holding markers.</summary>
    public int UnresolvedCount => Enumerable.Range(0, Hunks.Count).Count(Result.IsUnresolved);

    /// <summary>True when every conflict is settled.</summary>
    public bool IsComplete => UnresolvedCount == 0;

    /// <summary>True when at least one change was made, which closing would throw away.</summary>
    public bool IsModified => UndoStack is { Count: > 0 };

    /// <summary>True while a Visual selection of lines is open.</summary>
    public bool IsVisual => Anchor is not null;

    /// <summary>True when <see cref="Undo"/> has a change to take back.</summary>
    public bool CanUndo => UndoStack is { Count: > 0 };

    /// <summary>True when <see cref="Redo"/> has a change to bring back.</summary>
    public bool CanRedo => RedoStack is { Count: > 0 };

    /// <summary>The current conflict clamped to the conflicts.</summary>
    public int Index => Hunks.Count == 0 ? 0 : Math.Clamp(SelectedIndex, 0, Hunks.Count - 1);

    /// <summary>First aligned row of the Visual selection, -1 when none.</summary>
    public int SelectionStart => Anchor is { } anchor ? Math.Min(anchor, Row) : -1;

    /// <summary>Last aligned row of the Visual selection, -1 when none.</summary>
    public int SelectionEnd => Anchor is { } anchor ? Math.Max(anchor, Row) : -1;

    /// <summary>True when the active pane is one of the three versions.</summary>
    public bool IsVersionPane => ActivePane != ConflictPane.Result;

    /// <summary>Lines of the result.</summary>
    public int ResultLineCount => Result.Editor.Lines.Count;

    /// <summary>The file as it was read, every conflict with its markers.</summary>
    public string OriginalText => TextOf(Parts);

    /// <summary>The text of parts as they stand in the file: common text and each conflict with its markers.</summary>
    public static string TextOf(IReadOnlyList<ConflictPart> parts) =>
        string.Concat(parts.Select(part => part.Common ?? part.Hunk!.Raw));

    /// <summary>The whole file as it would be saved.</summary>
    public string ToText() => Result.ToText(Format.LineEnding, Format.HasFinalNewline);

    /// <summary>The result line (zero-based) beside an aligned row.</summary>
    public int ResultLineOfRow(int row) => Alignment.ResultLineOf(row, Result.Regions, ResultLineCount);

    /// <summary>Whether a version can be copied into the current conflict (the base may be missing).</summary>
    public bool CanTake(ConflictPick pick)
    {
        if (Hunks.Count == 0 || IsEditing)
        {
            return false;
        }

        return pick != ConflictPick.Base || Hunks[Index].Base is not null;
    }

    /// <summary>
    /// The lines a Visual selection would copy from the active version pane into the current conflict.
    /// </summary>
    public IReadOnlyList<string> SelectedLines()
    {
        if (Anchor is null || !IsVersionPane || Hunks.Count == 0)
        {
            return [];
        }

        var first = Alignment.FirstRowOf(Index);
        var last = first + Alignment.RowCountOf(Index) - 1;
        var lines = new List<string>();
        for (var row = Math.Max(SelectionStart, first); row <= Math.Min(SelectionEnd, last); row++)
        {
            if (Alignment.TextAt(row, ActivePane) is { } text)
            {
                lines.Add(text);
            }
        }

        return lines;
    }

    /// <summary>Moves the cursor by <paramref name="delta"/> lines in the active pane, stopping at the ends.</summary>
    public ConflictEditorState MoveCursor(int delta)
    {
        if (IsEditing)
        {
            return this;
        }

        if (ActivePane != ConflictPane.Result)
        {
            return OnVersionRow(Row + delta);
        }

        var direction = Math.Sign(delta);
        for (var step = 0; step < Math.Abs(delta); step++)
        {
            Result.Editor.MoveLines(direction);
        }

        return SyncedFromResult();
    }

    /// <summary>Moves the cursor to the first line of the active pane.</summary>
    public ConflictEditorState MoveToTop() => MoveToLine(0);

    /// <summary>Moves the cursor to the last line of the active pane.</summary>
    public ConflictEditorState MoveToBottom() =>
        MoveToLine(ActivePane == ConflictPane.Result ? ResultLineCount - 1 : Alignment.RowCount - 1);

    /// <summary>Moves to the next conflict below the cursor, wrapping to the first.</summary>
    public ConflictEditorState NextConflict()
    {
        if (Hunks.Count == 0 || IsEditing)
        {
            return this;
        }

        var target = Enumerable.Range(0, Hunks.Count).Where(hunk => Alignment.FirstRowOf(hunk) > Row)
            .Cast<int?>().FirstOrDefault() ?? 0;

        return GoToConflict(target);
    }

    /// <summary>Moves to the conflict above the cursor, wrapping to the last.</summary>
    public ConflictEditorState PreviousConflict()
    {
        if (Hunks.Count == 0 || IsEditing)
        {
            return this;
        }

        var target = Enumerable.Range(0, Hunks.Count).Where(hunk => Alignment.FirstRowOf(hunk) < Row)
            .Cast<int?>().LastOrDefault() ?? Hunks.Count - 1;

        return GoToConflict(target);
    }

    /// <summary>Moves the cursor to a pane; into the result it lands beside the row it was on.</summary>
    public ConflictEditorState FocusPane(ConflictPane pane)
    {
        if (pane == ActivePane || IsEditing)
        {
            return this;
        }

        var cleared = this with { Anchor = null, IsDiscardPending = false };
        if (pane == ConflictPane.Result)
        {
            Result.Editor.SetCursor(ResultLineOfRow(Row), 0);

            return cleared with { ActivePane = pane, TopPane = ActivePane };
        }

        var row = ActivePane == ConflictPane.Result ? Alignment.RowOf(Result.Editor.CursorRow, Result.Regions) : Row;

        return (cleared with { ActivePane = pane, TopPane = pane }).OnVersionRow(row);
    }

    /// <summary>Moves to the next pane (base, ours, theirs, result), wrapping.</summary>
    public ConflictEditorState NextPane() => FocusPane(PaneOrder[(Array.IndexOf(PaneOrder, ActivePane) + 1) % 4]);

    /// <summary>Moves to the previous pane, wrapping.</summary>
    public ConflictEditorState PreviousPane() => FocusPane(PaneOrder[(Array.IndexOf(PaneOrder, ActivePane) + 3) % 4]);

    /// <summary>Moves one version pane to the left; from the result it does nothing.</summary>
    public ConflictEditorState PaneLeft() =>
        IsVersionPane ? FocusPane(PaneOrder[Math.Max(0, (int)ActivePane - 1)]) : this;

    /// <summary>Moves one version pane to the right; from the result it does nothing.</summary>
    public ConflictEditorState PaneRight() =>
        IsVersionPane ? FocusPane(PaneOrder[Math.Min((int)ConflictPane.Theirs, (int)ActivePane + 1)]) : this;

    /// <summary>Moves from a version pane down to the result.</summary>
    public ConflictEditorState PaneDown() => IsVersionPane ? FocusPane(ConflictPane.Result) : this;

    /// <summary>Moves from the result up to the version pane it came from.</summary>
    public ConflictEditorState PaneUp() => IsVersionPane ? this : FocusPane(TopPane);

    /// <summary>Starts a Visual selection at the cursor row of a version pane, or ends the open one.</summary>
    public ConflictEditorState ToggleVisual()
    {
        if (!IsVersionPane || IsEditing)
        {
            return this;
        }

        return this with { Anchor = IsVisual ? null : Row };
    }

    /// <summary>Ends a Visual selection.</summary>
    public ConflictEditorState EndVisual() => IsVisual ? this with { Anchor = null } : this;
}
