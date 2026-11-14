using Gitunia.Git.Remotes;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Git.Tests.Remotes;

public sealed class SshConfigCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gitunia-sshcache-{Guid.NewGuid():N}");
    private readonly ManualTimeProvider _clock = new();
    private readonly Dictionary<string, string?> _environment = [];
    private readonly RepositoryLocation _location;
    private readonly SshConfigCache _cache;
    private int _reads;

    public SshConfigCacheTests()
    {
        var gitDirectory = Path.Combine(_root, "repo", ".git");
        Directory.CreateDirectory(gitDirectory);
        Directory.CreateDirectory(Path.Combine(_root, "home"));
        _environment["HOME"] = Path.Combine(_root, "home");
        _environment["XDG_CONFIG_HOME"] = Path.Combine(_root, "xdg");
        _location = new RepositoryLocation(Path.Combine(_root, "repo"), gitDirectory, gitDirectory, false, false);
        _cache = new SshConfigCache(name => _environment.GetValueOrDefault(name), _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task GivenUnchangedFiles_WhenAskingTwice_ThenTheValueIsReadOnce()
    {
        await GetAsync();
        await GetAsync();

        Assert.Equal(1, _reads);
    }

    [Fact]
    public async Task GivenARepositoryConfigEdit_WhenAskingAgain_ThenItIsReadAgain()
    {
        await GetAsync();

        await File.WriteAllTextAsync(Path.Combine(_location.CommonDirectory, "config"), "[core]\n\tsshCommand = ssh -i k\n", Token);
        await GetAsync();

        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task GivenAUserConfigEdit_WhenAskingAgain_ThenItIsReadAgain()
    {
        await GetAsync();

        await File.WriteAllTextAsync(Path.Combine(_environment["HOME"]!, ".gitconfig"), "[core]\n\tsshCommand = ssh -i k\n", Token);
        await GetAsync();

        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task GivenAnXdgConfigEdit_WhenAskingAgain_ThenItIsReadAgain()
    {
        await GetAsync();

        var directory = Path.Combine(_environment["XDG_CONFIG_HOME"]!, "git");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "config"), "[core]\n\tsshCommand = ssh -i k\n", Token);
        await GetAsync();

        Assert.Equal(2, _reads);
    }

    [Theory]
    [InlineData("[include]\n\tpath = other\n")]
    [InlineData("[includeIf \"gitdir:~/\"]\n\tpath = other\n")]
    public async Task GivenAnIncludeDirective_WhenAskingTwice_ThenNothingIsCached(string config)
    {
        await File.WriteAllTextAsync(Path.Combine(_location.CommonDirectory, "config"), config, Token);

        await GetAsync();
        await GetAsync();

        Assert.Equal(2, _reads);
    }

    [Theory]
    [InlineData("GIT_CONFIG_GLOBAL")]
    [InlineData("GIT_CONFIG_COUNT")]
    [InlineData("GIT_CONFIG_PARAMETERS")]
    public async Task GivenAConfigOverrideVariable_WhenAskingTwice_ThenNothingIsCached(string variable)
    {
        _environment[variable] = "1";

        await GetAsync();
        await GetAsync();

        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task GivenAnOversizedFile_WhenAskingTwice_ThenNothingIsCached()
    {
        await File.WriteAllTextAsync(Path.Combine(_location.CommonDirectory, "config"), new string('#', 300 * 1024), Token);

        await GetAsync();
        await GetAsync();

        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task GivenTheTimeToLivePassed_WhenAskingAgain_ThenItIsReadAgain()
    {
        await GetAsync();

        _clock.Advance(SshConfigCache.TimeToLive + TimeSpan.FromSeconds(1));
        await GetAsync();

        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task GivenTwoRepositories_WhenAskingEach_ThenEachIsReadOnce()
    {
        var otherDirectory = Path.Combine(_root, "other", ".git");
        Directory.CreateDirectory(otherDirectory);
        var other = new RepositoryLocation(Path.Combine(_root, "other"), otherDirectory, otherDirectory, false, false);

        await GetAsync();
        await _cache.GetAsync(other, ReadAsync, Token);
        await GetAsync();
        await _cache.GetAsync(other, ReadAsync, Token);

        Assert.Equal(2, _reads);
    }

    private async Task GetAsync() => await _cache.GetAsync(_location, ReadAsync, Token);

    private Task<string> ReadAsync()
    {
        _reads++;

        return Task.FromResult("ssh");
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan span) => _now += span;
    }
}
