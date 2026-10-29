namespace Gitunia.Core.Graph;

/// <summary>The colour slot of one branch label on a commit (#849).</summary>
/// <param name="Name">The branch name without a remote prefix (a local branch and its remote-tracking branch share it).</param>
/// <param name="Slot">Colour slot (see <see cref="BranchColors"/>) of the label's chip.</param>
public readonly record struct BranchSlot(string Name, int Slot);
