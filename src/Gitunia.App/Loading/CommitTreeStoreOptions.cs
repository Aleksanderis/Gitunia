
namespace Gitunia.App.Loading;

/// <summary>Optional tuning of a <see cref="Trees.CommitTreeStore"/>.</summary>
public sealed record CommitTreeStoreOptions(IDelayer? Delayer = null, TimeProvider? TimeProvider = null);
