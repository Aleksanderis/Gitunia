namespace Gitunia.Core.IssueTracker.Models;

/// <summary>A piece of a line that a tracker rule turned into a link.</summary>
/// <param name="Start">Index of the first character in the line.</param>
/// <param name="Length">Number of characters the link covers.</param>
/// <param name="Url">The address to open (http or https).</param>
public sealed record IssueReference(int Start, int Length, string Url);
