namespace Gitunia.Tui.App.Tags;

/// <summary>Which status bar prompt of the tag actions is open.</summary>
public enum TagPromptKind
{
    None,

    /// <summary>Typing the name of a new tag.</summary>
    CreateName,

    /// <summary>Typing the message of an annotated tag; empty makes a lightweight one.</summary>
    CreateMessage,

    /// <summary>Choosing the remote a tag is pushed to.</summary>
    PushRemote,

    /// <summary>Choosing the remote a tag is deleted from.</summary>
    DeleteRemote,
}
