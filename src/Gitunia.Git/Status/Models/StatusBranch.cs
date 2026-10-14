using Gitunia.Git.Refs.Models;

namespace Gitunia.Git.Status.Models;

/// <summary>The <c># branch.*</c> header of <c>git status --branch</c>.</summary>
/// <param name="Head">Branch, detached or unborn, with the current commit when there is one.</param>
/// <param name="UpstreamName">Short name of the upstream, for example <c>origin/main</c>; null without one.</param>
/// <param name="Ahead">Commits ahead of the upstream; null when git printed no counts (no upstream, or it is gone).</param>
/// <param name="Behind">Commits behind the upstream; null when git printed no counts.</param>
public sealed record StatusBranch(HeadState Head, string? UpstreamName, int? Ahead, int? Behind);
