namespace Gitunia.Git.Diff.Models;

/// <summary>The two paths of a <c>diff --git</c> header.</summary>
/// <param name="OldPath">The path on the old side, without the <c>a/</c> prefix.</param>
/// <param name="NewPath">The path on the new side, without the <c>b/</c> prefix.</param>
public readonly record struct DiffPaths(string OldPath, string NewPath);
