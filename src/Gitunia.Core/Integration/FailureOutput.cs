namespace Gitunia.Core.Integration;

/// <summary>Git's whole output of a failed call, for the scrollable output box.</summary>
/// <param name="Title">The box title.</param>
/// <param name="Heading">The heading of the text section.</param>
/// <param name="Lines">The output, one entry per line.</param>
public sealed record FailureOutput(string Title, string Heading, IReadOnlyList<string> Lines);
