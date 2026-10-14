namespace Gitunia.Core.Tests.Search;

/// <summary>One commit of a <see cref="SearchTestGraph"/>.</summary>
/// <param name="Subject">The commit subject.</param>
/// <param name="Author">The author name.</param>
public sealed record SearchTestRow(string Subject, string Author);
