namespace Gitunia.Core.Creation;

/// <summary>
/// Clone and init from the repository picker as the shell state holds them: one creation runs at a time and belongs to
/// no tab. Changed only by <see cref="CreationReducer"/>.
/// </summary>
/// <param name="RunningClone">The running clone, null while none runs.</param>
/// <param name="IsInitRunning">A <c>git init</c> runs.</param>
public sealed record CreationSession(CloneState? RunningClone, bool IsInitRunning)
{
    public static CreationSession Empty { get; } = new(null, false);

    /// <summary>A clone or an init runs.</summary>
    public bool IsBusy => RunningClone is not null || IsInitRunning;
}
