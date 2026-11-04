namespace Gitunia.Core.Picker;

/// <summary>The groups of the unfiltered picker list, in display order. Flags, so a set of expanded sections is one value.</summary>
[Flags]
public enum PickerSection
{
    None = 0,
    Open = 1,
    Pinned = 2,
    Recent = 4,
    Found = 8,
}
