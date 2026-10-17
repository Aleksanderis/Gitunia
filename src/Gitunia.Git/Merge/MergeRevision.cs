namespace Gitunia.Git.Merge;

/// <summary>What is passed to <c>git merge</c>, and the message to force with <c>-m</c> (null keeps git's).</summary>
/// <param name="Revision">The revision argument.</param>
/// <param name="Message">The merge message, or null.</param>
internal sealed record MergeRevision(string Revision, string? Message);
