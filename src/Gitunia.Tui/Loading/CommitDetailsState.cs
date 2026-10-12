using Gitunia.Git;

namespace Gitunia.Tui.Loading;

/// <summary>The details of the selected commit and whether they have arrived.</summary>
/// <param name="Commit">The selected commit, null when none is.</param>
/// <param name="Status">Loading, loaded or failed.</param>
/// <param name="Details">The loaded details, null unless <paramref name="Status"/> is <see cref="CommitDetailsStatus.Loaded"/>.</param>
/// <param name="Error">Why loading failed, empty otherwise.</param>
public sealed record CommitDetailsState(ObjectId? Commit, CommitDetailsStatus Status, CommitDetails? Details, string Error)
{
    public static CommitDetailsState Empty { get; } = new(null, CommitDetailsStatus.None, null, string.Empty);
}
