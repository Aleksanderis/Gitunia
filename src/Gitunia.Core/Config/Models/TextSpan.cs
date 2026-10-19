namespace Gitunia.Core.Config.Models;

/// <summary>A range of characters in the config text.</summary>
/// <param name="Start">Index of the first character.</param>
/// <param name="Length">Number of characters.</param>
public readonly record struct TextSpan(int Start, int Length);
