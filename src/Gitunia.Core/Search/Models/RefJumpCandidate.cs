using Gitunia.Git;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Search.Models;

/// <summary>One thing the ref jump prompt can jump to: a ref or a commit found by hash prefix.</summary>
/// <param name="Label">What the prompt shows: the ref name, or the short hash for a commit.</param>
/// <param name="CommitId">The commit to select.</param>
/// <param name="Kind">The kind of ref, null for a commit found by hash.</param>
public sealed record RefJumpCandidate(string Label, ObjectId CommitId, RefKind? Kind);
