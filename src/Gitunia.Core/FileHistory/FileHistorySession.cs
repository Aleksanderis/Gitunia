namespace Gitunia.Core.FileHistory;

/// <summary>
/// The file history overlay as the shell state holds it: the open history (null while closed) and the bookkeeping of
/// the reads that fill it. Changed only by <see cref="FileHistoryReducer"/>.
/// </summary>
/// <param name="History">The open history, null while it is closed.</param>
/// <param name="Generation">Counts list reads and closes; a list result with another number is stale.</param>
/// <param name="DiffGeneration">Counts diff reads and closes; a diff result with another number is stale.</param>
/// <param name="IsReading">True from the list read until its result arrives; a second open is refused
/// meanwhile.</param>
public sealed record FileHistorySession(
    FileHistoryState? History,
    long Generation,
    long DiffGeneration,
    bool IsReading)
{
    public static FileHistorySession Empty { get; } = new(null, 0, 0, false);

    public bool IsOpen => History is not null;
}
