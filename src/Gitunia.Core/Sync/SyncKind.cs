namespace Gitunia.Core.Sync;

/// <summary>Which transfer a key asked for.</summary>
public enum SyncKind
{
    /// <summary>Fetch the remote of the current branch.</summary>
    Fetch,

    /// <summary>Fetch every remote.</summary>
    FetchAll,

    /// <summary>Pull the current branch, fast-forward only.</summary>
    Pull,

    /// <summary>Push the current branch; a branch without an upstream asks for the remote first.</summary>
    Push,

    /// <summary>Push the current branch to a remote chosen from a picker, also when it has an upstream elsewhere.</summary>
    PushTo,

    /// <summary>Force push the current branch, only ever with a lease, after a confirm.</summary>
    PushForce,
}
