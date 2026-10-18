namespace Gitunia.Tui.App.Remote;

/// <summary>Which prompt popup of the push extras is open.</summary>
public enum PushPromptKind
{
    None,

    /// <summary>Choosing the remote of the first push of a branch (which then becomes its upstream).</summary>
    FirstPushRemote,

    /// <summary>Choosing the remote a branch is deleted from.</summary>
    DeleteRemote,

    /// <summary>Choosing the branch to delete on the chosen remote.</summary>
    DeleteBranch,
}
