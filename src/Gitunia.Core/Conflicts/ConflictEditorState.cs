using System.Text;
using Gitunia.Core.Editing;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// The open conflict editor (ux.md, workflow 4, "Conflict editor"): a conflicted file as common text and hunks, the
/// pick made for each hunk (or the text typed for it) and the hunk under the cursor. Pure: it never reads or writes a
/// file; <see cref="ToText"/> is what the save writes. A hunk without a pick keeps its conflict markers in the result,
/// so a partial save leaves the file conflicted exactly where the user did not decide. Every pick and every accepted
/// edit is one step of <see cref="Undo"/> and <see cref="Redo"/>.
/// </summary>
/// <param name="TabId">The tab the editor was opened in; the save runs there.</param>
/// <param name="Path">The conflicted path, relative to the working tree root.</param>
/// <param name="Parts">The file in order: common text and hunks.</param>
/// <param name="Hunks">The hunks of <paramref name="Parts"/> in order.</param>
/// <param name="Picks">The pick of each hunk, parallel to <paramref name="Hunks"/>.</param>
/// <param name="Texts">
/// The text the user typed for each hunk, parallel to <paramref name="Hunks"/>; used where the pick is
/// <see cref="ConflictPick.Edited"/>.
/// </param>
/// <param name="SelectedIndex">The hunk under the cursor.</param>
/// <param name="IsDiscardPending">The first <c>Esc</c> on an edited file was pressed; the next one closes it.</param>
/// <param name="Edit">The open free-text edit of the hunk under the cursor, null when none.</param>
/// <param name="IsBaseShown">The base (common ancestor) of the hunk under the cursor is shown.</param>
/// <param name="SideScroll">
/// The first line of our side, their side and the base that is shown; the three scroll together. The frame clamps it
/// to what fits.
/// </param>
/// <param name="UndoStack">What <see cref="Undo"/> restores, the latest change last.</param>
/// <param name="RedoStack">What <see cref="Redo"/> restores, the latest undone change last.</param>
public sealed record ConflictEditorState(
    TabId TabId,
    string Path,
    IReadOnlyList<ConflictPart> Parts,
    IReadOnlyList<ConflictHunk> Hunks,
    IReadOnlyList<ConflictPick> Picks,
    IReadOnlyList<string?> Texts,
    int SelectedIndex = 0,
    bool IsDiscardPending = false,
    ConflictTextEdit? Edit = null,
    bool IsBaseShown = false,
    int SideScroll = 0,
    IReadOnlyList<ConflictSnapshot>? UndoStack = null,
    IReadOnlyList<ConflictSnapshot>? RedoStack = null)
{
    /// <summary>The most changes one session remembers; older ones are forgotten.</summary>
    public const int UndoLimit = 200;

    private const string LineFeed = "\n";
    private const string CarriageReturnLineFeed = "\r\n";

    /// <summary>The state for a freshly read file: nothing is picked and the first hunk is selected.</summary>
    public static ConflictEditorState Create(TabId tabId, string path, IReadOnlyList<ConflictPart> parts)
    {
        var hunks = parts.Where(part => part.Hunk is not null).Select(part => part.Hunk!).ToList();

        var picks = Enumerable.Repeat(ConflictPick.None, hunks.Count).ToList();
        var texts = Enumerable.Repeat<string?>(null, hunks.Count).ToList();

        return new ConflictEditorState(tabId, path, parts, hunks, picks, texts);
    }

    /// <summary>The hunk under the cursor, null for a file with none.</summary>
    public ConflictHunk? Selected => Hunks.Count == 0 ? null : Hunks[Index];

    /// <summary>The pick of the hunk under the cursor.</summary>
    public ConflictPick SelectedPick => Hunks.Count == 0 ? ConflictPick.None : Picks[Index];

    /// <summary>Hunks with a pick.</summary>
    public int ResolvedCount => Picks.Count(pick => pick != ConflictPick.None);

    /// <summary>Hunks still without a pick.</summary>
    public int UnresolvedCount => Hunks.Count - ResolvedCount;

    /// <summary>True when every hunk has a pick.</summary>
    public bool IsComplete => UnresolvedCount == 0;

    /// <summary>True when at least one pick was made, which closing would throw away.</summary>
    public bool IsModified => ResolvedCount > 0;

    /// <summary>True while the text of a hunk is being edited.</summary>
    public bool IsEditing => Edit is not null;

    /// <summary>True when <see cref="Undo"/> has a change to take back.</summary>
    public bool CanUndo => UndoStack is { Count: > 0 };

    /// <summary>True when <see cref="Redo"/> has a change to bring back.</summary>
    public bool CanRedo => RedoStack is { Count: > 0 };

    /// <summary>The cursor clamped to the hunks.</summary>
    public int Index => Hunks.Count == 0 ? 0 : Math.Clamp(SelectedIndex, 0, Hunks.Count - 1);

    /// <summary>Moves to the next hunk, wrapping from the last to the first.</summary>
    public ConflictEditorState Next() => MoveTo(Hunks.Count == 0 ? 0 : (Index + 1) % Hunks.Count);

    /// <summary>Moves to the previous hunk, wrapping from the first to the last.</summary>
    public ConflictEditorState Previous() => MoveTo(Hunks.Count == 0 ? 0 : (Index + Hunks.Count - 1) % Hunks.Count);

    /// <summary>Moves to <paramref name="index"/>, clamped to the hunks.</summary>
    public ConflictEditorState MoveTo(int index) => IsEditing
        ? this
        : this with
        {
            SelectedIndex = Hunks.Count == 0 ? 0 : Math.Clamp(index, 0, Hunks.Count - 1),
            IsDiscardPending = false,
            SideScroll = 0,
        };

    /// <summary>
    /// Scrolls the side panes (ours, theirs and the base together) by <paramref name="delta"/> lines, from the
    /// position that is drawn, never past <paramref name="maximum"/> (the largest scroll the screen allows).
    /// </summary>
    public ConflictEditorState ScrollSides(int delta, int maximum) => IsEditing || Hunks.Count == 0
        ? this
        : this with { SideScroll = Math.Clamp(Math.Min(SideScroll, maximum) + delta, 0, Math.Max(0, maximum)) };

    /// <summary>
    /// Scrolls the side panes to the next line where our side and their side differ, below the first line shown; a
    /// run of differing lines counts once; never past <paramref name="maximum"/>, the largest scroll the screen
    /// allows. Lines are compared row by row, so a line inserted on one side makes the rows after it differ. Stays
    /// put when there is none.
    /// </summary>
    public ConflictEditorState NextDifference(int maximum)
    {
        var starts = DifferenceStarts();
        var next = starts.Where(start => start > Math.Min(SideScroll, maximum)).Cast<int?>().FirstOrDefault();

        return next is { } row && !IsEditing ? this with { SideScroll = Math.Min(row, maximum) } : this;
    }

    /// <summary>Scrolls the side panes to the previous line where the sides differ (see NextDifference).</summary>
    public ConflictEditorState PreviousDifference(int maximum)
    {
        var starts = DifferenceStarts();
        var previous = starts.Where(start => start < Math.Min(SideScroll, maximum)).Cast<int?>().LastOrDefault();

        return previous is { } row && !IsEditing ? this with { SideScroll = Math.Min(row, maximum) } : this;
    }

    /// <summary>The most lines our side or their side of the hunk under the cursor has.</summary>
    public int SideLineCount => Hunks.Count == 0
        ? 0
        : Math.Max(Lines(Hunks[Index].Ours).Count, Lines(Hunks[Index].Theirs).Count);

    /// <summary>The line of the result file (counted from 1) where hunk <paramref name="index"/> starts.</summary>
    public int ResultLineOf(int index)
    {
        var line = 1;
        var hunk = 0;
        foreach (var part in Parts)
        {
            if (part.Common is { } common)
            {
                line += Lines(common).Count;

                continue;
            }

            if (hunk++ == index)
            {
                break;
            }

            line += Lines(ResultOf(hunk - 1)).Count;
        }

        return line;
    }

    /// <summary>
    /// Takes <paramref name="pick"/> for the hunk under the cursor (<see cref="ConflictPick.None"/> makes it
    /// unresolved again; <see cref="ConflictPick.Edited"/> comes from <see cref="AcceptEdit"/>, not from here). A
    /// change is one step of <see cref="Undo"/>.
    /// </summary>
    public ConflictEditorState Pick(ConflictPick pick)
    {
        if (Hunks.Count == 0 || IsEditing || pick == ConflictPick.Edited || Picks[Index] == pick)
        {
            return this;
        }

        return Changed(pick, null);
    }

    /// <summary>Toggles the base pane of the hunk under the cursor.</summary>
    public ConflictEditorState ToggleBase() => this with { IsBaseShown = !IsBaseShown, IsDiscardPending = false };

    /// <summary>
    /// Opens the editor on the result of the hunk under the cursor (both sides, ours first, for an unresolved hunk).
    /// Tab keys insert tab characters.
    /// </summary>
    public ConflictEditorState BeginEdit()
    {
        if (Hunks.Count == 0 || IsEditing)
        {
            return this;
        }

        var hunk = Hunks[Index];
        var editor = new MessageEditor { UsesTabCharacters = true };
        editor.SetText(Picks[Index] == ConflictPick.None ? $"{hunk.Ours}{hunk.Theirs}" : ResultOf(Index));

        return this with { Edit = new ConflictTextEdit(editor, editor.Text), IsDiscardPending = false };
    }

    /// <summary>
    /// Closes the editor and keeps the text as the hunk's result (one step of <see cref="Undo"/>); text left as it
    /// was when the edit began changes nothing. A result that does not end in a line break gets one, and a hunk of a
    /// CRLF file gets CRLF line breaks.
    /// </summary>
    public ConflictEditorState AcceptEdit()
    {
        if (Edit is not { } edit)
        {
            return this;
        }

        var closed = this with { Edit = null };
        if (edit.Editor.Text == edit.Original)
        {
            return closed;
        }

        var text = edit.Editor.Text;
        if (text.Length > 0 && !text.EndsWith(LineFeed, StringComparison.Ordinal))
        {
            text += LineFeed;
        }

        if (Hunks[Index].Raw.Contains(CarriageReturnLineFeed, StringComparison.Ordinal))
        {
            text = text.Replace(LineFeed, CarriageReturnLineFeed, StringComparison.Ordinal);
        }

        return closed.Changed(ConflictPick.Edited, text);
    }

    /// <summary>Takes back the latest change (a pick or an edit) and puts the cursor on its hunk.</summary>
    public ConflictEditorState Undo()
    {
        if (UndoStack is not { Count: > 0 } undo || IsEditing)
        {
            return this;
        }

        return Restored(undo[^1]) with
        {
            UndoStack = undo.Take(undo.Count - 1).ToList(),
            RedoStack = [.. RedoStack ?? [], Snapshot() with { SelectedIndex = undo[^1].SelectedIndex }],
        };
    }

    /// <summary>Brings back the latest undone change.</summary>
    public ConflictEditorState Redo()
    {
        if (RedoStack is not { Count: > 0 } redo || IsEditing)
        {
            return this;
        }

        return Restored(redo[^1]) with
        {
            RedoStack = redo.Take(redo.Count - 1).ToList(),
            UndoStack = [.. UndoStack ?? [], Snapshot() with { SelectedIndex = redo[^1].SelectedIndex }],
        };
    }

    /// <summary>Remembers that the user asked to close an edited file; the next close really closes.</summary>
    public ConflictEditorState AskDiscard() => this with { IsDiscardPending = true };

    /// <summary>The text hunk <paramref name="index"/> gets in the result: the pick, or its conflict markers.</summary>
    public string ResultOf(int index) => Picks[index] switch
    {
        ConflictPick.Ours => Hunks[index].Ours,
        ConflictPick.Theirs => Hunks[index].Theirs,
        ConflictPick.Both => $"{Hunks[index].Ours}{Hunks[index].Theirs}",
        ConflictPick.Edited => Texts[index] ?? Hunks[index].Raw,
        _ => Hunks[index].Raw,
    };

    /// <summary>The file as it was read, every hunk with its conflict markers.</summary>
    public string OriginalText => TextOf(Parts);

    /// <summary>The text of parts as they stand in the file: common text and each hunk with its markers.</summary>
    public static string TextOf(IReadOnlyList<ConflictPart> parts) =>
        string.Concat(parts.Select(part => part.Common ?? part.Hunk!.Raw));

    /// <summary>The whole file as it would be saved.</summary>
    public string ToText()
    {
        var builder = new StringBuilder();
        var hunk = 0;
        foreach (var part in Parts)
        {
            builder.Append(part.Common ?? ResultOf(hunk++));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Up to <paramref name="count"/> lines of unconflicted text right before (<paramref name="before"/>) or after the
    /// hunk <paramref name="index"/>; none when another hunk touches it on that side.
    /// </summary>
    public IReadOnlyList<string> ContextAround(int index, bool before, int count)
    {
        var position = PartIndexOf(index);
        var neighbour = before ? position - 1 : position + 1;
        if (count <= 0 || neighbour < 0 || neighbour >= Parts.Count || Parts[neighbour].Common is not { } common)
        {
            return [];
        }

        var lines = Lines(common);

        return before ? [.. lines.Skip(Math.Max(0, lines.Count - count))] : [.. lines.Take(count)];
    }

    /// <summary>Splits text into lines without their terminators; a trailing terminator adds no empty line.</summary>
    public static IReadOnlyList<string> Lines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (text[^1] == '\n')
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private ConflictSnapshot Snapshot() => new(Picks, Texts, Index);

    private ConflictEditorState Restored(ConflictSnapshot snapshot) => this with
    {
        Picks = snapshot.Picks,
        Texts = snapshot.Texts,
        SelectedIndex = snapshot.SelectedIndex,
        IsDiscardPending = false,
        SideScroll = 0,
    };

    // Records the state before the change for Undo and forgets what Redo could bring back.
    private ConflictEditorState Changed(ConflictPick pick, string? text)
    {
        var picks = Picks.ToList();
        picks[Index] = pick;
        var texts = Texts.ToList();
        texts[Index] = text;
        var undo = (UndoStack ?? []).Append(Snapshot()).TakeLast(UndoLimit).ToList();

        return this with { Picks = picks, Texts = texts, UndoStack = undo, RedoStack = [], IsDiscardPending = false };
    }

    // The first line of each run of lines where the two sides differ, counted from the top of the hunk.
    private List<int> DifferenceStarts()
    {
        if (Hunks.Count == 0)
        {
            return [];
        }

        var ours = Lines(Hunks[Index].Ours);
        var theirs = Lines(Hunks[Index].Theirs);
        var starts = new List<int>();
        var wasDifferent = false;
        for (var row = 0; row < Math.Max(ours.Count, theirs.Count); row++)
        {
            var isDifferent = row >= ours.Count || row >= theirs.Count || ours[row] != theirs[row];
            if (isDifferent && !wasDifferent)
            {
                starts.Add(row);
            }

            wasDifferent = isDifferent;
        }

        return starts;
    }

    private int PartIndexOf(int hunkIndex)
    {
        var seen = -1;
        for (var position = 0; position < Parts.Count; position++)
        {
            if (Parts[position].Hunk is not null && ++seen == hunkIndex)
            {
                return position;
            }
        }

        return -1;
    }
}
