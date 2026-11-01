namespace Gitunia.Core.Sync;

/// <summary>The terminal retry to offer when a fetch, pull, push or remote delete failed for lack of credentials (#45).</summary>
/// <param name="Noun">What failed, for the dialog.</param>
/// <param name="Arguments">The arguments of the same command (without the background-only options).</param>
public sealed record SyncRetry(string Noun, IReadOnlyList<string> Arguments);
