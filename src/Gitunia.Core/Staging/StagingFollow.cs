namespace Gitunia.Core.Staging;

/// <summary>What happens after a stage or unstage that worked, once the status has been read again. It travels with the
/// effect and comes back in the result; it is never applied after a failure.</summary>
/// <param name="Then">A command to reduce next (the commit that waits for stage all).</param>
/// <param name="Resolved">The file the conflict editor saved: the report says how many conflicts are left and the
/// cursor goes to the file or to the first conflict left.</param>
/// <param name="Move">Where the diff cursor goes after the diff of the file is read again.</param>
public sealed record StagingFollow(AppCommand? Then = null, ResolvedFile? Resolved = null, DiffCursorMove? Move = null)
{
    /// <summary>Nothing follows.</summary>
    public static StagingFollow None { get; } = new();
}
