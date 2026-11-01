namespace Gitunia.App.Loading.Commits;

/// <summary>Where the details of the selected commit stand.</summary>
public enum CommitDetailsStatus
{
    /// <summary>No commit is selected.</summary>
    None,

    /// <summary>Waiting for the debounce or for git.</summary>
    Loading,
    Loaded,
    Failed,
}
