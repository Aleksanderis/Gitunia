using Gitunia.Core.FileTree;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>A Files tab row together with what drawing it needs.</summary>
/// <param name="Row">The tree row.</param>
/// <param name="Chevron">The open or closed marker of a directory, a blank on a leaf.</param>
/// <param name="Suffix">The faint text after the name of a leaf (its size or kind), with one leading blank (the
/// renderer adds the other).</param>
public sealed record FileTreeLine(FileTreeRow Row, char Chevron, string Suffix);
