namespace Gitunia.Git.Blame.Models;

/// <summary>The lines of the file to blame.</summary>
/// <param name="Kind">Whether there are lines.</param>
/// <param name="Lines">The lines without their line ending; empty unless <paramref name="Kind"/> is text.</param>
public sealed record BlameContent(BlameContentKind Kind, IReadOnlyList<string> Lines);
