namespace Gitunia.Git.Pick;

/// <summary>A parent of a merge commit, as the mainline prompt lists it.</summary>
/// <param name="Hash">The full hash.</param>
/// <param name="Subject">The subject line.</param>
public sealed record PickParent(string Hash, string Subject);
