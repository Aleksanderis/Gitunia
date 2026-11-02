namespace Gitunia.Git.Blame.Models;

/// <summary>What the file to blame turned out to be.</summary>
public enum BlameContentKind
{
    /// <summary>Text, split into lines.</summary>
    Text,

    /// <summary>Binary data (a NUL byte near the start): there are no lines to blame.</summary>
    Binary,

    /// <summary>The file is not in the working tree.</summary>
    Missing,
}
