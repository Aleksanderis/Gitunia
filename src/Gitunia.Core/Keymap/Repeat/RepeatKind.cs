namespace Gitunia.Core.Keymap.Repeat;

/// <summary>The step actions a repeat mode can repeat (docs/ux.md, "Repeat mode").</summary>
public enum RepeatKind
{
    /// <summary>More or fewer context lines around each change in a diff.</summary>
    Context,

    /// <summary>Moving a pane divider.</summary>
    Resize,
}
