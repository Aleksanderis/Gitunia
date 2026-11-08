using Gitunia.Core.Keymap;
using Gitunia.App.Background;
using Gitunia.App.Loading.Diff;

namespace Gitunia.App.Loading;

/// <summary>Optional tuning of a <see cref="DiffStore"/>; <c>Keymap</c> is where its notes read the key to load all; <c>Tasks</c> remembers the running reads so the shutdown can wait for them.</summary>
public sealed record DiffStoreOptions(
    int RowLimit = DiffStore.DefaultRowLimit,
    int FileLineLimit = DiffStore.DefaultFileLineLimit,
    DiffCache? Cache = null,
    IDelayer? Delayer = null,
    IBackgroundWork? Background = null,
    KeyBindingTable? Keymap = null,
    ShutdownTasks? Tasks = null);
