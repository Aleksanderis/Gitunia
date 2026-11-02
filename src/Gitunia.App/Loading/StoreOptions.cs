using Gitunia.App.Background;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Diff;

namespace Gitunia.App.Loading;

/// <summary>Waits out a debounce. The default is <see cref="Task.Delay(TimeSpan, CancellationToken)"/>; tests inject one they control.</summary>
public interface IDelayer
{
    /// <summary>Completes after <paramref name="duration"/>, or throws when <paramref name="token"/> is cancelled first.</summary>
    Task DelayAsync(TimeSpan duration, CancellationToken token);
}

/// <summary>The real wait.</summary>
public sealed class TaskDelayer : IDelayer
{
    public static readonly TaskDelayer Instance = new();

    public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
}

/// <summary>Optional tuning of a <see cref="CommitDetailsStore"/>.</summary>
/// <param name="Delayer">Waits out the debounce.</param>
/// <param name="Cache">The cache of loaded details; the default has its default bounds.</param>
/// <param name="TimeProvider">The clock that tells a fresh selection from one in a quick run of moves.</param>
/// <param name="Background">Where prefetch waits for a slot; the default runs it at once.</param>
public sealed record CommitDetailsStoreOptions(
    IDelayer? Delayer = null,
    CommitDetailsCache? Cache = null,
    TimeProvider? TimeProvider = null,
    IBackgroundWork? Background = null);

/// <summary>Optional tuning of a <see cref="Trees.CommitTreeStore"/>.</summary>
public sealed record CommitTreeStoreOptions(IDelayer? Delayer = null, TimeProvider? TimeProvider = null);

/// <summary>Optional tuning of a <see cref="DiffStore"/>.</summary>
public sealed record DiffStoreOptions(
    int RowLimit = DiffStore.DefaultRowLimit,
    int FileLineLimit = DiffStore.DefaultFileLineLimit,
    DiffCache? Cache = null,
    IDelayer? Delayer = null,
    IBackgroundWork? Background = null);

/// <summary>The sizes after which a <see cref="WorkingDiffStore"/> cuts a diff short.</summary>
public sealed record WorkingDiffLimits(
    int RowLimit = DiffStore.DefaultRowLimit,
    int FileLineLimit = DiffStore.DefaultFileLineLimit,
    int FolderFileLimit = WorkingDiffStore.DefaultFolderFileLimit);
