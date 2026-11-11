using System.Collections.Concurrent;
using System.Text;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Remotes;

/// <summary>
/// Remembers the <c>core.sshCommand</c> a repository (or the global configuration) resolved to, so a fetch, pull or
/// push does not start a <c>git config</c> process first. An entry is valid only while the text of the configuration
/// files git reads (repository, worktree, user level) is byte for byte what it was when the value was read, so an
/// edit between two calls is always noticed. Anything the files cannot tell is not cached: an include directive, a
/// <c>GIT_CONFIG_*</c> variable, a file too large to compare. The system-level file is not read, so an entry also
/// expires after <see cref="TimeToLive"/>.
/// </summary>
/// <param name="environmentLookup">Reads a process environment variable.</param>
/// <param name="clock">The time source for the expiry.</param>
public sealed class SshConfigCache(Func<string, string?> environmentLookup, TimeProvider clock)
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(30);

    private const int MaxFileLength = 256 * 1024;
    private const string IncludeDirective = "include";
    private const string ConfigFileName = "config";
    private const string UserConfigFileName = ".gitconfig";
    private const string WorktreeConfigFileName = "config.worktree";
    private const string GitConfigDirectoryName = "git";
    private const string HomeVariable = "HOME";
    private const string XdgConfigHomeVariable = "XDG_CONFIG_HOME";
    private const string ConfigDirectoryName = ".config";
    private const string GlobalKey = "";

    private static readonly string[] OverrideVariables = ["GIT_CONFIG_GLOBAL", "GIT_CONFIG_SYSTEM", "GIT_CONFIG_COUNT", "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_NOSYSTEM"];

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>The cached value when the configuration is unchanged, else the result of <paramref name="read"/> (cached when it can be).</summary>
    /// <param name="location">The repository, or null for the global configuration.</param>
    /// <param name="read">Reads the value with a git process.</param>
    public async Task<string> GetAsync(RepositoryLocation? location, Func<Task<string>> read, CancellationToken cancellationToken)
    {
        var key = location?.GitDirectory ?? GlobalKey;
        var now = clock.GetUtcNow();

        // The snapshot is taken before the read: an edit in between makes the next call's snapshot differ.
        var snapshot = await SnapshotAsync(location, cancellationToken).ConfigureAwait(false);
        if (snapshot is not null
            && _entries.TryGetValue(key, out var entry)
            && entry.Snapshot == snapshot
            && now - entry.ReadAt < TimeToLive)
        {
            return entry.Value;
        }

        var value = await read().ConfigureAwait(false);
        if (snapshot is null)
        {
            _entries.TryRemove(key, out _);
        }
        else
        {
            _entries[key] = new Entry(snapshot, value, now);
        }

        return value;
    }

    /// <summary>The joined text of the files that decide the value, or null when it cannot be told from them.</summary>
    private async Task<string?> SnapshotAsync(RepositoryLocation? location, CancellationToken cancellationToken)
    {
        foreach (var variable in OverrideVariables)
        {
            if (!string.IsNullOrEmpty(environmentLookup(variable)))
            {
                return null;
            }
        }

        var snapshot = new StringBuilder();
        foreach (var path in ConfigFiles(location))
        {
            if (!File.Exists(path))
            {
                snapshot.Append('\0').Append(path).Append('\n');
                continue;
            }

            if (new FileInfo(path).Length > MaxFileLength)
            {
                return null;
            }

            string text;
            try
            {
                text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            // Includes pull in files this snapshot does not cover.
            if (text.Contains(IncludeDirective, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            snapshot.Append('\0').Append(path).Append('\n').Append(text);
        }

        return snapshot.ToString();
    }

    private IEnumerable<string> ConfigFiles(RepositoryLocation? location)
    {
        // Git reads HOME first (on Windows too), then the profile directory.
        var home = environmentLookup(HomeVariable);
        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (home.Length > 0)
        {
            yield return Path.Combine(home, UserConfigFileName);
        }

        var configHome = environmentLookup(XdgConfigHomeVariable);
        if (string.IsNullOrEmpty(configHome) && home.Length > 0)
        {
            configHome = Path.Combine(home, ConfigDirectoryName);
        }

        if (!string.IsNullOrEmpty(configHome))
        {
            yield return Path.Combine(configHome, GitConfigDirectoryName, ConfigFileName);
        }

        if (location is not null)
        {
            yield return Path.Combine(location.CommonDirectory, ConfigFileName);
            yield return Path.Combine(location.GitDirectory, WorktreeConfigFileName);
        }
    }

    private sealed record Entry(string Snapshot, string Value, DateTimeOffset ReadAt);
}
