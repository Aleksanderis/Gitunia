using Gitunia.Core.FileTree;

namespace Gitunia.Core.Details;

/// <summary>Moves the cursor of the detail list to a row (the terminal layer owns the cursor).</summary>
/// <param name="Row">The row.</param>
public sealed record MoveDetailCursorEffect(int Row) : AppEffect;

/// <summary>Shows the content of a file row of the Files tab in the diff pane's place and reports why it cannot.</summary>
/// <param name="Row">The file row.</param>
public sealed record OpenFileContentEffect(FileTreeRow Row) : AppEffect;
