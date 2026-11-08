namespace Gitunia.Core.Picker;

/// <summary>What one line of the picker list is.</summary>
public enum PickerEntryKind
{
    /// <summary>A blank line above a header.</summary>
    Spacer,

    /// <summary>A group header.</summary>
    Header,

    /// <summary>A repository or directory row.</summary>
    Row,

    /// <summary>The row that expands a capped section ("N more").</summary>
    More,

    /// <summary>The row that collapses an expanded section.</summary>
    Less,
}
