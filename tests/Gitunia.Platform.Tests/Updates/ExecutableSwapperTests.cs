using Gitunia.Platform.Updates;
using Xunit;

namespace Gitunia.Platform.Tests.Updates;

public sealed class ExecutableSwapperTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("gitunia-swap-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenARunningBinary_WhenReplaced_ThenTheNewContentIsInPlaceAndNoStagedFileRemains(bool renamesAside)
    {
        var executable = WriteFile("gitunia", "old");
        var downloaded = WriteFile("downloaded", "new");

        new ExecutableSwapper(renamesAside).Replace(executable, downloaded);

        Assert.Equal("new", File.ReadAllText(executable));
        Assert.False(File.Exists($"{executable}.new"));
        Assert.False(File.Exists($"{executable}.old"));
        Assert.True(File.Exists(downloaded));
    }

    [Fact]
    public void GivenAnOpenOldBinary_WhenReplacedTheUnixWay_ThenTheOpenFileKeepsItsContent()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows cannot replace an open file; the aside swap covers it.");
        var executable = WriteFile("gitunia", "old");
        var downloaded = WriteFile("downloaded", "new");

        using var running = File.OpenRead(executable);
        new ExecutableSwapper(renamesAside: false).Replace(executable, downloaded);

        using var reader = new StreamReader(running);
        Assert.Equal("old", reader.ReadToEnd());
        Assert.Equal("new", File.ReadAllText(executable));
    }

    [Fact]
    public void GivenALeftoverFromAPreviousSwap_WhenReplacedAside_ThenItIsRemovedAndTheSwapSucceeds()
    {
        var executable = WriteFile("gitunia.exe", "old");
        var downloaded = WriteFile("downloaded", "new");
        WriteFile("gitunia.exe.old", "ancient");

        new ExecutableSwapper(renamesAside: true).Replace(executable, downloaded);

        Assert.Equal("new", File.ReadAllText(executable));
        Assert.False(File.Exists($"{executable}.old"));
    }

    [Fact]
    public void GivenAMissingNewBinary_WhenReplaced_ThenTheOldBinaryIsUntouched()
    {
        var executable = WriteFile("gitunia", "old");

        Assert.ThrowsAny<IOException>(
            () => new ExecutableSwapper(renamesAside: true).Replace(executable, Path.Combine(_directory, "missing")));

        Assert.Equal("old", File.ReadAllText(executable));
        Assert.False(File.Exists($"{executable}.old"));
    }

    [Fact]
    public void GivenAnAsideFileOnWindowsStyle_WhenRemovingLeftovers_ThenOnlyTheAsideFileGoes()
    {
        var executable = WriteFile("gitunia.exe", "current");
        var aside = WriteFile("gitunia.exe.old", "old");

        new ExecutableSwapper(renamesAside: true).RemoveLeftovers(executable);

        Assert.False(File.Exists(aside));
        Assert.True(File.Exists(executable));
    }

    [Fact]
    public void GivenAnAsideFileOnUnixStyle_WhenRemovingLeftovers_ThenNothingIsTouched()
    {
        var executable = WriteFile("gitunia", "current");
        var other = WriteFile("gitunia.old", "someone else's file");

        new ExecutableSwapper(renamesAside: false).RemoveLeftovers(executable);

        Assert.True(File.Exists(other));
    }

    [Fact]
    public void GivenAUnixSwap_WhenReplaced_ThenTheNewBinaryIsExecutable()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no executable bit.");
        var executable = WriteFile("gitunia", "old");
        var downloaded = WriteFile("downloaded", "new");

        new ExecutableSwapper(renamesAside: false).Replace(executable, downloaded);

        Assert.True(!OperatingSystem.IsWindows() && File.GetUnixFileMode(executable).HasFlag(UnixFileMode.UserExecute));
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);

        return path;
    }
}
