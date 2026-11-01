namespace Gitunia.Core.DiffPane;

/// <summary>The part of a loaded diff the pane shows.</summary>
/// <param name="File">Index of the file the pane shows, -1 for none.</param>
/// <param name="FolderFiles">The files of the shown folder, empty for a single file.</param>
/// <param name="BuiltFolder">The folder the document is cut for, null for a single file or the whole diff.</param>
public sealed record DiffShownCut(int File, IReadOnlyList<int> FolderFiles, string? BuiltFolder);
