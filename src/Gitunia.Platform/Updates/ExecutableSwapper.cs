namespace Gitunia.Platform.Updates;

/// <summary>
/// Puts a new binary in place of the running one. Linux and macOS rename the new file over the old one (atomic, the
/// running process keeps its open inode). Windows cannot overwrite a running <c>.exe</c> but can rename it, so the old
/// file is renamed aside to <c>.old</c>, the new one takes its name, and the <c>.old</c> file is removed now when
/// possible and otherwise by <see cref="RemoveLeftovers"/> at the next start.
/// </summary>
public sealed class ExecutableSwapper
{
    private const string StagedSuffix = ".new";
    private const string AsideSuffix = ".old";

    private const UnixFileMode ExecutableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private readonly bool _renamesAside;

    /// <param name="renamesAside">Use the Windows way (rename the old file aside); default: true on Windows only.</param>
    public ExecutableSwapper(bool? renamesAside = null)
    {
        _renamesAside = renamesAside ?? OperatingSystem.IsWindows();
    }

    /// <summary>
    /// Replaces the file at <paramref name="executablePath"/> with a copy of <paramref name="newBinaryPath"/>. On failure
    /// the old binary is still in place and the exception (an <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/>) is thrown.
    /// </summary>
    public void Replace(string executablePath, string newBinaryPath)
    {
        var staged = executablePath + StagedSuffix;
        try
        {
            File.Copy(newBinaryPath, staged, overwrite: true);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(staged, ExecutableMode);
            }

            if (_renamesAside)
            {
                SwapAside(executablePath, staged);
            }
            else
            {
                File.Move(staged, executablePath, overwrite: true);
            }
        }
        finally
        {
            DeleteQuietly(staged);
        }
    }

    /// <summary>
    /// Removes what a Windows swap could not delete while the old binary was running. A no-op elsewhere, and cheap: one
    /// existence check.
    /// </summary>
    public void RemoveLeftovers(string executablePath)
    {
        if (_renamesAside)
        {
            DeleteQuietly(executablePath + AsideSuffix);
        }
    }

    private static void SwapAside(string executablePath, string staged)
    {
        var aside = executablePath + AsideSuffix;
        DeleteQuietly(aside);
        File.Move(executablePath, aside);
        try
        {
            File.Move(staged, executablePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Put the old binary back, so a failed update never leaves the user without Gitunia.
            File.Move(aside, executablePath, overwrite: true);
            throw;
        }

        DeleteQuietly(aside);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file is still in use (a running Windows binary): RemoveLeftovers retries at the next start.
        }
    }
}
