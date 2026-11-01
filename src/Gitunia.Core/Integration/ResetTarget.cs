using Gitunia.Git.Reset;

namespace Gitunia.Core.Integration;

/// <summary>What one reset acts on, fixed when the mode was chosen.</summary>
/// <param name="CommitHash">The commit the branch moves to.</param>
/// <param name="BranchName">The branch that moves.</param>
/// <param name="Mode">How much the reset touches.</param>
public sealed record ResetTarget(string CommitHash, string BranchName, ResetMode Mode);
