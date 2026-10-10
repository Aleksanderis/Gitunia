using Gitunia.Platform;
using Xunit;

namespace Gitunia.Platform.Tests;

public class PlatformTests
{
    [Fact]
    public void Dirs_are_absolute_and_end_in_app_name()
    {
        var p = PlatformInfo.Current;
        Assert.True(Path.IsPathRooted(p.ConfigDir));
        Assert.True(Path.IsPathRooted(p.StateDir));
        Assert.Contains(PlatformInfo.AppDirName, p.ConfigDir);
    }
}
