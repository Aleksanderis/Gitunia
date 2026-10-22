using Gitunia.Core.Picker;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

public class PathDisplayTests
{
    private static readonly string _home = Path.Combine(Path.GetTempPath(), "gitunia-home");

    [Fact]
    public void GivenWindows_WhenAbbreviatingPathBelowHome_ThenPathIsShownNativeWithoutTilde()
    {
        var path = Path.Combine(_home, "repos", "app");

        Assert.Equal(path, PathDisplay.Abbreviate(path, _home, windows: true));
        Assert.Equal(_home, PathDisplay.Abbreviate(_home, _home, windows: true));
    }

    [Fact]
    public void GivenNotWindows_WhenAbbreviatingPathBelowHome_ThenTildeIsUsed()
    {
        var path = Path.Combine(_home, "repos", "app");
        var separator = Path.DirectorySeparatorChar;

        Assert.Equal($"~{separator}repos{separator}app", PathDisplay.Abbreviate(path, _home, windows: false));
        Assert.Equal("~", PathDisplay.Abbreviate(_home, _home, windows: false));
    }

    [Fact]
    public void GivenWindows_WhenNamingPathStarts_ThenNoTildeSlashIsShown()
    {
        Assert.DoesNotContain("~/", PathDisplay.PathStartsHint(windows: true), StringComparison.Ordinal);
        Assert.DoesNotContain("~/", PathDisplay.ExamplePath(windows: true), StringComparison.Ordinal);
        Assert.Equal("/ ~/ ./ ../", PathDisplay.PathStartsHint(windows: false));
        Assert.Equal("~/projects/app", PathDisplay.ExamplePath(windows: false));
    }

    [Fact]
    public void GivenTypedSeparator_WhenCompleting_ThenItIsKeptOnEveryPlatform()
    {
        var rows = new List<PickerRow> { new(PickerFixtures.PathOf("code", "only"), "only", PickerTags.Directory) };

        Assert.Equal("~/code/only/", PathCompleter.Complete("~/code/o", rows, 0, windows: true));
        Assert.Equal(@"~\code\only\", PathCompleter.Complete(@"~\code\o", rows, 0, windows: true));
    }

    [Fact]
    public void GivenBareTilde_WhenCompleting_ThenNativeSeparatorIsUsed()
    {
        var rows = new List<PickerRow> { new(PickerFixtures.PathOf("only"), "only", PickerTags.Directory) };

        Assert.Equal(@"~\only\", PathCompleter.Complete("~", rows, 0, windows: true));
        Assert.Equal("~/only/", PathCompleter.Complete("~", rows, 0, windows: false));
    }
}
