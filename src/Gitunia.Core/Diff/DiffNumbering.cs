namespace Gitunia.Core.Diff;

/// <summary>Whose line numbers a line history takes from a shown diff.</summary>
public enum DiffNumbering
{
    /// <summary>The file after the change (a commit's diff): added and context lines.</summary>
    NewSide,

    /// <summary>The file before the change (a working tree diff): removed and context lines, the numbers of HEAD.</summary>
    OldSide,
}
