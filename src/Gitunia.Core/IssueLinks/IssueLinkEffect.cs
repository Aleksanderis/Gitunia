namespace Gitunia.Core.IssueLinks;

/// <summary>Start the system opener for an address; the answer is an <see cref="IssueLinkOpenFinished"/>.</summary>
public sealed record OpenUrlEffect(string Url) : AppEffect;
