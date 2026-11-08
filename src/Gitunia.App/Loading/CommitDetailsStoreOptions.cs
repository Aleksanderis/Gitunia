using Gitunia.App.Background;
using Gitunia.App.Loading.Commits;

namespace Gitunia.App.Loading;

/// <summary>Optional tuning of a <see cref="CommitDetailsStore"/>.</summary>
/// <param name="Delayer">Waits out the debounce.</param>
/// <param name="Cache">The cache of loaded details; the default has its default bounds.</param>
/// <param name="TimeProvider">The clock that tells a fresh selection from one in a quick run of moves.</param>
/// <param name="Background">Where prefetch waits for a slot; the default runs it at once.</param>
/// <param name="Tasks">Remembers the running reads so the shutdown can wait for them; the default is a private set.</param>
public sealed record CommitDetailsStoreOptions(
    IDelayer? Delayer = null,
    CommitDetailsCache? Cache = null,
    TimeProvider? TimeProvider = null,
    IBackgroundWork? Background = null,
    ShutdownTasks? Tasks = null);
