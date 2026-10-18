namespace Gitunia.Tui.App.Remote;

/// <summary>Which prompt popup of the remote management actions is open.</summary>
public enum RemotePromptKind
{
    None,

    /// <summary>Typing the name of a new remote.</summary>
    AddName,

    /// <summary>Typing the URL of a new remote.</summary>
    AddUrl,

    /// <summary>Choosing the remote to remove.</summary>
    RemovePick,

    /// <summary>Choosing the remote to rename.</summary>
    RenamePick,

    /// <summary>Typing the new name of a remote.</summary>
    RenameName,

    /// <summary>Choosing the remote whose URL changes.</summary>
    UrlPick,

    /// <summary>Typing the new URL of a remote.</summary>
    UrlText,
}
