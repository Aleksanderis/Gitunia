using Xunit;

namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// Tests that create real <c>FileSystemWatcher</c>s join this group and run one after another. Every watcher takes one
/// inotify instance on Linux and the limit is per user (128 on a stock CI runner, shared with the other test projects),
/// so watcher tests running side by side used it up, the watcher fell back to polling by design and the test waited for
/// state that never came (#1398). The group's tests also keep their own instance count small.
/// </summary>
[CollectionDefinition(Name)]
public sealed class RealFileWatchersGroup
{
    public const string Name = "Real file watchers";
}
