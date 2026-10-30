namespace Gitunia.Core.Picker;

/// <summary>Why a repository or directory is in the picker list. A row can have several tags.</summary>
[Flags]
public enum PickerTags
{
    None = 0,

    /// <summary>The repository is open in a tab.</summary>
    Open = 1,

    /// <summary>The repository is pinned.</summary>
    Pinned = 2,

    /// <summary>The repository is in the recents list.</summary>
    Recent = 4,

    /// <summary>The repository was found by scanning the scan roots.</summary>
    Discovered = 8,

    /// <summary>A directory offered while typing a path.</summary>
    Directory = 16,

    /// <summary>A directory that is a git repository (has a <c>.git</c> entry).</summary>
    Repository = 32,

    /// <summary>The repository directory of an open tab is gone (the tab waits to be located or closed).</summary>
    Missing = 64,
}
