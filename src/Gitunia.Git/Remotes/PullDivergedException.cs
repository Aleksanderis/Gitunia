using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Git.Remotes;

/// <summary>A fast-forward-only pull found history diverged; no merge or checkout happened (the fetch ran).</summary>
public sealed class PullDivergedException(string gitMessage)
    : GitException($"Cannot fast-forward: local and remote have diverged ({gitMessage})");
