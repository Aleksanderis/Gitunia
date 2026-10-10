using Gitunia.Core;
using Gitunia.Platform;

namespace Gitunia.Tui;

public static class Program
{
    public static int Main()
    {
        var caps = PlatformInfo.Current.DetectConsole();
        Console.Out.Write(HelloFrame.Render(AppInfo.Banner, caps));
        return 0;
    }
}
