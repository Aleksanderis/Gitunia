namespace Gitunia.Tui.Rendering.Details;

/// <summary>The path of a changed file in a detail row.</summary>
/// <param name="Path">The path (the new path of a rename).</param>
/// <param name="OldPath">The old path of a rename or copy, null otherwise.</param>
public sealed record FileRowPath(string Path, string? OldPath);
