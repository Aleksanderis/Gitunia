namespace Gitunia.Core.Tags;

/// <summary>The terminal retry to offer when a remote tag call failed for lack of credentials (#512).</summary>
/// <param name="Noun">What failed, for the dialog.</param>
/// <param name="Arguments">The arguments of the same command.</param>
public sealed record TagRetry(string Noun, IReadOnlyList<string> Arguments);
