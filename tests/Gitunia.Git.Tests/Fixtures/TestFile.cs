namespace Gitunia.Git.Tests.Fixtures;

/// <summary>A file to write into a test repository.</summary>
/// <param name="RelativePath">Path relative to the repository root.</param>
/// <param name="Content">File text.</param>
public sealed record TestFile(string RelativePath, string Content);
