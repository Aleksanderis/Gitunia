namespace Gitunia.Core.Staging;

/// <summary>A conflicted file staged because it is resolved; the report counts what is left once the status is read
/// again.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="Name">The printable name for the report.</param>
public sealed record ResolvedFile(string Path, string Name);
