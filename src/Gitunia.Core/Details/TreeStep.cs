namespace Gitunia.Core.Details;

/// <summary>What h does in a file tree.</summary>
public enum TreeStep
{
    /// <summary>Close the open directory or folder under the cursor.</summary>
    Close,

    /// <summary>Move the cursor to the row that holds this one.</summary>
    Parent,

    /// <summary>Nothing: the cursor is at the top level.</summary>
    Leave,
}
