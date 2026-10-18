using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Git.Remotes;

/// <summary>A fast-forward-only pull found local and remote history diverged; nothing was changed.</summary>
public sealed class PullDivergedException(string gitMessage)
    : GitException($"Cannot fast-forward: local and remote have diverged ({gitMessage})");
