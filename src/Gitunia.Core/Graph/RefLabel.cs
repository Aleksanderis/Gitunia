using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Graph;

/// <summary>A ref label of a graph row, after merging a local branch with its remote-tracking branch.</summary>
/// <param name="Name">Name shown, for example <c>main</c> or <c>origin/main</c>.</param>
/// <param name="Kind">What the label stands for; a merged local and remote branch is a <see cref="RefKind.LocalBranch"/>.</param>
/// <param name="IsCurrent">HEAD is on this branch.</param>
/// <param name="HasRemote">A remote-tracking branch of the same name points at the same commit and is folded into this label.</param>
/// <param name="IsUpstreamGone">The branch has an upstream that no longer exists (a candidate for pruning).</param>
/// <param name="RemoteNames">
/// The remotes folded into this label, shown after the remote glyph (#1495); empty when the branch has only one
/// remote-tracking branch or all of them sit on the same commit.
/// </param>
public sealed record RefLabel(
    string Name,
    RefKind Kind,
    bool IsCurrent,
    bool HasRemote,
    bool IsUpstreamGone = false,
    string RemoteNames = "");
