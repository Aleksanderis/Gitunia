namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>What a click on the settings overlay landed on.</summary>
public enum SettingsHitKind
{
    /// <summary>Nothing that acts: a border, a heading, empty space.</summary>
    None,

    /// <summary>A category name.</summary>
    Category,

    /// <summary>The label of a row of the settings column.</summary>
    Row,

    /// <summary>The control at the right end of a row (toggle box, choice, number).</summary>
    Control,

    /// <summary>The <c>[+]</c> marker of a path list.</summary>
    Add,

    /// <summary>The <c>[x]</c> marker of a folder.</summary>
    Remove,

    /// <summary>The "yes" half of the removal question.</summary>
    ConfirmYes,

    /// <summary>The "no" half of the removal question.</summary>
    ConfirmNo,
}
