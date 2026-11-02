using Gitunia.Git.Blame.Models;

namespace Gitunia.App.Blame;

/// <summary>
/// The lines of the blamed file and the commit of each, for the view to read by index. The lines are set once; the
/// handler fills <see cref="Owners"/> while git finds the commits (a reference write is atomic, so a reader sees either
/// null or the commit).
/// </summary>
public sealed class BlameData
{
    public BlameData(long generation, IReadOnlyList<string> lines)
    {
        Generation = generation;
        Lines = lines;
        Owners = new BlameCommit?[lines.Count];
    }

    /// <summary>The read number the data belongs to.</summary>
    public long Generation { get; }

    /// <summary>The lines of the file without their line endings.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>The commit of each line, null while git has not found it.</summary>
    public BlameCommit?[] Owners { get; }

    /// <summary>The commit of a line, null while unknown or out of range.</summary>
    /// <param name="line">The line (zero-based).</param>
    public BlameCommit? OwnerOf(int line) => line >= 0 && line < Owners.Length ? Volatile.Read(ref Owners[line]) : null;

    /// <summary>Records the commit of a run of lines.</summary>
    /// <param name="chunk">The run.</param>
    /// <returns>How many lines of the run were new to the file (inside it).</returns>
    public int Apply(BlameChunk chunk)
    {
        var applied = 0;
        var end = Math.Min(chunk.FirstLine + chunk.LineCount, Owners.Length);
        for (var line = Math.Max(0, chunk.FirstLine); line < end; line++)
        {
            Volatile.Write(ref Owners[line], chunk.Commit);
            applied++;
        }

        return applied;
    }
}
