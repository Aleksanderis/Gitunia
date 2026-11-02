using Gitunia.App.Background;
using Gitunia.App.Loading.Diff;

namespace Gitunia.App.Loading;

/// <summary>Optional tuning of a <see cref="DiffStore"/>.</summary>
public sealed record DiffStoreOptions(
    int RowLimit = DiffStore.DefaultRowLimit,
    int FileLineLimit = DiffStore.DefaultFileLineLimit,
    DiffCache? Cache = null,
    IDelayer? Delayer = null,
    IBackgroundWork? Background = null);
