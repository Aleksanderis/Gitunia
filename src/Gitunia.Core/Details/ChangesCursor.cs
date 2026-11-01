namespace Gitunia.Core.Details;

/// <summary>What the tree style of the Changes tab shows under the cursor, read by the terminal layer from the rows it drew.</summary>
/// <param name="Folder">The path of the folder row under the cursor, null on a file row.</param>
/// <param name="ParentRow">The index of the folder row that holds the cursor row, -1 at the top level.</param>
public sealed record ChangesCursor(string? Folder, int ParentRow);
