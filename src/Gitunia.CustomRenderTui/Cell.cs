namespace Gitunia.CustomRenderTui;

/// <summary>One terminal cell. Fg is a 256-colour palette index; 0 means the terminal default.</summary>
public readonly record struct Cell(char Ch, Style Style, byte Fg = 0);
