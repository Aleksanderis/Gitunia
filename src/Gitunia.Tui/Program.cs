using Gitunia.Core;
using Gitunia.Platform;

namespace Gitunia.Tui;

public static class Program
{
    public static int Main()
    {
        var capabilities = PlatformInfo.Current.DetectConsole();
        Console.Out.Write(HelloFrame.Render(AppInfo.Banner, capabilities));
        return 0;
    }
}
