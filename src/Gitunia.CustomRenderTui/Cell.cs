namespace Gitunia.CustomRenderTui;

/// <summary>One terminal cell. Foreground is a 256-colour palette index; 0 means the terminal default.</summary>
public readonly record struct Cell(char Character, Style Style, byte Foreground = 0);
