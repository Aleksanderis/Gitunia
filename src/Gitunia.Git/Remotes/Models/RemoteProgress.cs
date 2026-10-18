namespace Gitunia.Git.Remotes.Models;

/// <summary>One progress report of a fetch, pull or push, parsed from git's <c>--progress</c> stderr.</summary>
/// <param name="Stage">What git is doing, as it names it (for example <c>Receiving objects</c>).</param>
/// <param name="Percent">0 to 100, or null for a stage that only counts (<c>Enumerating objects: 5, done.</c>).</param>
public sealed record RemoteProgress(string Stage, int? Percent);
