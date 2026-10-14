namespace Gitunia.Tui.Rendering.Styling;

/// <summary>A title button before it is placed on a row.</summary>
/// <param name="View">The mouse view name that identifies the button.</param>
/// <param name="Label">The text shown, for example <c>[x]</c>.</param>
public readonly record struct ButtonSpecification(string View, string Label);
