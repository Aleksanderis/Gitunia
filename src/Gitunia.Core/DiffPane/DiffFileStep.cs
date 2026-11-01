namespace Gitunia.Core.DiffPane;

/// <summary>The file <c>]f</c> and <c>[f</c> move to.</summary>
public static class DiffFileStep
{
    /// <summary>The file to move to; past the first or last file nothing moves.</summary>
    /// <param name="shown">Index of the file shown now, negative for none.</param>
    /// <param name="fileCount">How many files there are.</param>
    /// <param name="forward">Towards the last file.</param>
    /// <param name="steps">How many files to move.</param>
    /// <returns>The index of the file, -1 when there is none to move to.</returns>
    public static int Target(int shown, int fileCount, bool forward, int steps)
    {
        if (shown < 0 || fileCount <= 0)
        {
            return -1;
        }

        var target = Math.Clamp(shown + (forward ? steps : -steps), 0, fileCount - 1);

        return target == shown ? -1 : target;
    }
}
