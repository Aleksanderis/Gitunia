namespace Gitunia.Core.Remotes;

/// <summary>Which remote management action was asked for.</summary>
public enum RemoteAction
{
    /// <summary>Add a remote: name, then URL.</summary>
    Add,

    /// <summary>Remove a remote after a confirm.</summary>
    Remove,

    /// <summary>Rename a remote.</summary>
    Rename,

    /// <summary>Change the URL of a remote.</summary>
    SetUrl,
}
