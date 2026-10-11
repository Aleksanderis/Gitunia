namespace Gitunia.Git;

/// <summary>What a parser produced, plus a warning for every input it had to skip or could not understand.</summary>
public sealed record ParseResult<TItem>(IReadOnlyList<TItem> Items, IReadOnlyList<string> Warnings);
