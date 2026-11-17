namespace Gitunia.Core.Overlays;

/// <summary>
/// The size of the candidate list in a prompt popup. The rows themselves come from the prompt's
/// <see cref="Filtering.FilterList{T}"/> (<see cref="Prompts.PromptPicker.Rows"/>), drawn as the selection bar like the
/// repository picker and the palette, not as a text mark.
/// </summary>
public static class PickerRows
{
    /// <summary>How many candidate rows are shown at most; the list scrolls to keep the highlight in view.</summary>
    public const int VisibleRows = 8;
}
