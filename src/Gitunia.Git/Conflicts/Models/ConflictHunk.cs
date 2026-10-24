namespace Gitunia.Git.Conflicts.Models;

/// <summary>
/// One conflicted region of a file. Every text keeps its line terminators, so joining them back reproduces the file
/// byte for byte (CRLF stays CRLF).
/// </summary>
/// <param name="Ours">The lines of the current side (between the opening marker and the next marker).</param>
/// <param name="Base">
/// The lines of the common ancestor (from the markers for diff3 and zdiff3, else from the stages), null when none could be found.
/// </param>
/// <param name="Theirs">The lines of the other side.</param>
/// <param name="OursLabel">The text after the opening marker (the side's name), empty when there is none.</param>
/// <param name="TheirsLabel">The text after the closing marker, empty when there is none.</param>
/// <param name="Raw">The whole region as it stands in the file, markers included.</param>
public sealed record ConflictHunk(
    string Ours,
    string? Base,
    string Theirs,
    string OursLabel,
    string TheirsLabel,
    string Raw);
