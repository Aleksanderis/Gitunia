using Gitunia.Core.FileTree;

namespace Gitunia.Core.Details;

/// <summary>What the Files tab shows under the cursor, read by the terminal layer from the rows it drew.</summary>
/// <param name="Row">The row under the cursor, null when the cursor is outside the rows.</param>
/// <param name="ParentRow">The index of the row that holds <see cref="Row"/>, -1 at the top level.</param>
public sealed record FileTreeCursor(FileTreeRow? Row, int ParentRow);
