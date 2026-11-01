namespace Gitunia.Core.Sync;

/// <summary>Why the remotes were read.</summary>
public enum SyncRemotesPurpose
{
    /// <summary>Choose the remote of the first push of a branch.</summary>
    FirstPush,

    /// <summary>Split the upstream of the branch into remote and branch for the force push confirm.</summary>
    Force,

    /// <summary>Choose or resolve the remote branch to delete.</summary>
    Delete,
}
