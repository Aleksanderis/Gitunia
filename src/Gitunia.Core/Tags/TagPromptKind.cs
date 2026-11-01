namespace Gitunia.Core.Tags;

/// <summary>Which prompt popup of the tag actions is open.</summary>
public enum TagPromptKind
{
    /// <summary>Typing the name of a new tag.</summary>
    CreateName,

    /// <summary>Typing the message of an annotated tag; empty makes a lightweight one.</summary>
    CreateMessage,

    /// <summary>Choosing the remote a tag is pushed to.</summary>
    PushRemote,

    /// <summary>Choosing the remote a tag is deleted from.</summary>
    DeleteRemote,
}
