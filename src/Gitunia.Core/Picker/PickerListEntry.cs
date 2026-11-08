namespace Gitunia.Core.Picker;

/// <summary>One line of the picker list as <see cref="PickerListPlan"/> lays it out.</summary>
/// <param name="Kind">What the line is.</param>
/// <param name="Section">The group it belongs to (meaningless for a spacer and for the flat list).</param>
/// <param name="RowIndex">The index in <see cref="PickerState.Rows"/> of a row; for a more or less line the last row of
/// its section that the line follows (where the highlight is anchored); -1 for spacers and headers.</param>
/// <param name="Shown">The rows of the section that are listed.</param>
/// <param name="Total">The rows the section has.</param>
public sealed record PickerListEntry(PickerEntryKind Kind, PickerSection Section, int RowIndex, int Shown, int Total)
{
    /// <summary>Whether the highlight can sit on the line.</summary>
    public bool IsSelectable => Kind is PickerEntryKind.Row or PickerEntryKind.More or PickerEntryKind.Less;

    /// <summary>Whether the line is a blank spacer.</summary>
    public bool IsSpacer => Kind == PickerEntryKind.Spacer;

    /// <summary>Whether the line is a more or less line.</summary>
    public bool IsToggle => Kind is PickerEntryKind.More or PickerEntryKind.Less;

    /// <summary>The rows the section hides.</summary>
    public int Hidden => Total - Shown;
}
