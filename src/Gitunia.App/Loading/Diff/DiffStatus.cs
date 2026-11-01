namespace Gitunia.App.Loading.Diff;

/// <summary>Where the diff of the shown commit stands.</summary>
public enum DiffStatus
{
    /// <summary>No diff was asked for.</summary>
    None,
    Loading,
    Loaded,
    Failed,
}
