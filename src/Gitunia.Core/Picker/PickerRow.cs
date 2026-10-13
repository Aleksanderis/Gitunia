namespace Gitunia.Core.Picker;

/// <summary>One line of the picker list.</summary>
/// <param name="Path">The absolute path of the repository root or directory.</param>
/// <param name="Name">The last path segment, shown first.</param>
/// <param name="Tags">Why the row is listed.</param>
public sealed record PickerRow(string Path, string Name, PickerTags Tags)
{
    public bool IsOpen => Tags.HasFlag(PickerTags.Open);

    public bool IsPinned => Tags.HasFlag(PickerTags.Pinned);

    public bool IsRecent => Tags.HasFlag(PickerTags.Recent);
}
