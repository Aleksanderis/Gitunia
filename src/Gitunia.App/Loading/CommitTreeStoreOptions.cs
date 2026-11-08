
namespace Gitunia.App.Loading;

/// <summary>Optional tuning of a <see cref="Trees.CommitTreeStore"/> (<c>Tasks</c> remembers the running reads so the shutdown can wait for them).</summary>
public sealed record CommitTreeStoreOptions(IDelayer? Delayer = null, TimeProvider? TimeProvider = null, ShutdownTasks? Tasks = null);
