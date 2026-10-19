namespace Gitunia.Core.Settings.Models;

/// <summary>What a key or click asks of the settings screen, apart from moving the cursor.</summary>
public enum SettingsCommandKind
{
    /// <summary><c>Esc</c> and <c>h</c>: undo the innermost thing, else go back a column, else close.</summary>
    Back,

    /// <summary><c>q</c> or the open key again: close at once.</summary>
    Close,

    /// <summary><c>Enter</c> and <c>l</c>: enter a column, edit the setting or keep the option.</summary>
    Activate,

    /// <summary><c>x</c>: flip a toggle.</summary>
    Toggle,

    /// <summary><c>a</c>: add a folder.</summary>
    Add,

    /// <summary><c>d</c>: ask to remove the folder under the cursor.</summary>
    Remove,

    /// <summary><c>J</c>: move the folder down.</summary>
    ItemDown,

    /// <summary><c>K</c>: move the folder up.</summary>
    ItemUp,

    /// <summary><c>+</c>: raise a number.</summary>
    Increase,

    /// <summary><c>-</c>: lower a number.</summary>
    Decrease,

    /// <summary><c>/</c>: start typing a filter.</summary>
    FilterOpen,

    /// <summary><c>Enter</c> in a text box.</summary>
    InputAccept,

    /// <summary><c>y</c> on the removal question.</summary>
    ConfirmYes,

    /// <summary><c>n</c> on the removal question.</summary>
    ConfirmNo,
}
