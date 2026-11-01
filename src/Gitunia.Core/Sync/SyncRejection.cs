namespace Gitunia.Core.Sync;

/// <summary>A push was rejected as a non-fast-forward, the case a force push with a lease can answer.</summary>
/// <param name="Branch">The branch that was pushed.</param>
/// <param name="Remote">The remote of a first push (set upstream), null for the plain push of a branch with an upstream.</param>
public sealed record SyncRejection(string Branch, string? Remote);
