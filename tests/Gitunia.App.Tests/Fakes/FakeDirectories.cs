using Gitunia.App.Picker;

namespace Gitunia.App.Tests.Fakes;

/// <summary>A file system for picker tests: a map from a directory to the names of its subdirectories, no disk access.</summary>
public sealed class FakeDirectories : IDirectoryLister
{
    private readonly IReadOnlyDictionary<string, string[]> _map;
    private readonly HashSet<string> _repositories;

    public FakeDirectories(IReadOnlyDictionary<string, string[]>? map = null, IEnumerable<string>? repositories = null)
    {
        _map = map ?? new Dictionary<string, string[]>();
        _repositories = [.. repositories ?? []];
    }

    public IReadOnlyList<string> ListDirectories(string directory)
    {
        var key = Path.TrimEndingDirectorySeparator(directory);

        return _map.TryGetValue(key, out var names) ? [.. names.Select(name => Path.Combine(key, name))] : [];
    }

    public bool IsRepository(string directory) => _repositories.Contains(directory);

    public bool DirectoryExists(string directory) => _map.ContainsKey(Path.TrimEndingDirectorySeparator(directory));
}
