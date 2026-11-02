namespace Gitunia.Core.Conflicts;

public sealed partial record ConflictEditorState
{
    /// <summary>
    /// Copies a version of the current conflict into the result (<see cref="ConflictPick.Ours"/>,
    /// <see cref="ConflictPick.Theirs"/>, <see cref="ConflictPick.Base"/> or <see cref="ConflictPick.Both"/>),
    /// replacing whatever the region holds, typed text included (undo brings it back). One step of
    /// <see cref="Undo"/>; nothing changes when the region already holds exactly those lines.
    /// </summary>
    public ConflictEditorState Take(ConflictPick pick)
    {
        var isVersion = pick is ConflictPick.Ours or ConflictPick.Theirs or ConflictPick.Base or ConflictPick.Both;
        if (!CanTake(pick) || !isVersion)
        {
            return this;
        }

        var lines = VersionLines(Hunks[Index], pick);
        var index = Index;

        return (Anchor is null ? this : this with { Anchor = null }).Changed(
            result => result.ReplaceRegion(index, lines, pick));
    }

    /// <summary>
    /// Copies the lines of the Visual selection (from the active version pane, inside the current conflict) into the
    /// result: they replace an unresolved conflict's markers and are appended to a conflict that already has lines.
    /// Always ends the selection; nothing is copied when it holds no line of the conflict.
    /// </summary>
    public ConflictEditorState TakeSelectedLines()
    {
        var lines = SelectedLines();
        if (lines.Count == 0 || IsEditing)
        {
            return EndVisual();
        }

        var index = Index;

        return (this with { Anchor = null }).Changed(result =>
        {
            var combined = new List<string>();
            if (!result.IsUnresolved(index))
            {
                combined.AddRange(result.LinesOf(index));
            }

            combined.AddRange(lines);
            result.ReplaceRegion(index, combined, ConflictPick.Lines);
        });
    }

    /// <summary>
    /// Takes <paramref name="pick"/> (ours or theirs) for every conflict that is still unresolved, leaving settled
    /// ones alone. One step of <see cref="Undo"/>; nothing changes when none is open.
    /// </summary>
    public ConflictEditorState PickAllUnresolved(ConflictPick pick)
    {
        if (IsEditing || pick is not (ConflictPick.Ours or ConflictPick.Theirs) || UnresolvedCount == 0)
        {
            return this;
        }

        return Changed(result =>
        {
            for (var hunk = 0; hunk < Hunks.Count; hunk++)
            {
                if (result.IsUnresolved(hunk))
                {
                    result.ReplaceRegion(hunk, VersionLines(Hunks[hunk], pick), pick);
                }
            }
        });
    }

    /// <summary>True when the current conflict holds a pick (or typed text) that <see cref="Untake"/> can give up.</summary>
    public bool CanUntake => Hunks.Count > 0 && !IsEditing && SelectedPick != ConflictPick.None;

    /// <summary>
    /// The inverse of a take: makes the current conflict unresolved again, its markers come back and whatever the
    /// region held (a pick, picked lines, typed text) goes; undo brings it back. One step of undo; nothing changes
    /// when the conflict is unresolved already.
    /// </summary>
    public ConflictEditorState Untake()
    {
        if (!CanUntake)
        {
            return this;
        }

        var index = Index;
        var raw = ConflictAlignment.Lines(Hunks[index].Raw);

        return (Anchor is null ? this : this with { Anchor = null }).Changed(
            result => result.ReplaceRegion(index, raw, ConflictPick.None));
    }

    /// <summary>
    /// The inverse of <see cref="TakeSelectedLines"/>: removes the selected lines of the active version pane from
    /// the current conflict's result (each one from its last matching line, as a take appends), and puts the markers
    /// back when no line is left. Always ends the selection; nothing changes when the conflict is unresolved or none
    /// of the lines is in its result. One step of undo.
    /// </summary>
    public ConflictEditorState UntakeSelectedLines()
    {
        var (remaining, removed) = LinesLeftAfterUntake();
        if (removed == 0)
        {
            return EndVisual();
        }

        var index = Index;
        var raw = ConflictAlignment.Lines(Hunks[index].Raw);

        return (this with { Anchor = null }).Changed(result => result.ReplaceRegion(
            index,
            remaining.Count == 0 ? raw : remaining,
            remaining.Count == 0 ? ConflictPick.None : ConflictPick.Lines));
    }

    /// <summary>How many of the selected lines <see cref="UntakeSelectedLines"/> would remove from the result.</summary>
    public int UntakeableLineCount => LinesLeftAfterUntake().Removed;

    private (List<string> Remaining, int Removed) LinesLeftAfterUntake()
    {
        var selected = SelectedLines();
        if (selected.Count == 0 || IsEditing || Result.IsUnresolved(Index))
        {
            return ([], 0);
        }

        var remaining = Result.LinesOf(Index).ToList();
        var removed = 0;
        foreach (var line in selected.Reverse())
        {
            var at = remaining.FindLastIndex(candidate => string.Equals(candidate, line, StringComparison.Ordinal));
            if (at >= 0)
            {
                remaining.RemoveAt(at);
                removed++;
            }
        }

        return (remaining, removed);
    }

    /// <summary>
    /// A click on a row of a version pane: the cursor goes there, in that pane (which becomes the active one), and a
    /// Visual selection ends. A row past the end of the file does nothing; neither does a click while typing.
    /// </summary>
    public ConflictEditorState ClickVersionRow(ConflictPane pane, int row)
    {
        if (IsEditing || pane == ConflictPane.Result || row < 0 || row >= Alignment.RowCount)
        {
            return this;
        }

        return (FocusPane(pane) with { Anchor = null }).OnVersionRow(row);
    }

    /// <summary>
    /// A click on a line of the result: the cursor goes there, at the display column of the click (the line end past
    /// the text), in the result, in Normal mode. While typing it only moves the cursor within the result. A line past
    /// the end does nothing.
    /// </summary>
    /// <param name="line">The result line.</param>
    /// <param name="column">The display column in the line.</param>
    public ConflictEditorState ClickResultLine(int line, int column = 0)
    {
        if (line < 0 || line >= ResultLineCount)
        {
            return this;
        }

        var focused = FocusPane(ConflictPane.Result);
        focused.Result.Editor.PlaceCursor(line, column);

        return focused.SyncedFromResult();
    }

    /// <summary>The far end of a Visual selection follows a drag to an aligned row of the version panes.</summary>
    public ConflictEditorState DragToVersionRow(int row) => IsVisual && IsVersionPane ? OnVersionRow(row) : this;

    /// <summary>
    /// Scrolls the text of the base, ours and theirs panes sideways together by <paramref name="columns"/> (negative
    /// to the left), kept between 0 and the widest line.
    /// </summary>
    public ConflictEditorState ScrollSideways(int columns)
    {
        var target = Math.Clamp(HorizontalScroll + columns, 0, MaxHorizontalScroll);

        return target == HorizontalScroll ? this : this with { HorizontalScroll = target };
    }
}
