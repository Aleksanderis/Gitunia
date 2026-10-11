using Gitunia.Core;
using Gitunia.Git;
using Gitunia.Platform;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering;

namespace Gitunia.Tui;

public static class Program
{
    public static int Main(string[] arguments)
    {
        var options = CommandLineParser.Parse(arguments);
        if (options.Error is not null)
        {
            Console.Error.WriteLine($"gitunia: {options.Error}");
            Console.Error.WriteLine(CommandLineParser.Usage);
            return 2;
        }

        var capabilities = PlatformInfo.Current.DetectConsole();
        if (options.RepositoryPath is null)
        {
            // No session restore or repo picker yet: show the banner, like before the UI existed.
            Console.Out.Write(HelloFrame.Render(AppInfo.Banner, capabilities));
            Console.Out.WriteLine(CommandLineParser.Usage);
            return 0;
        }

        var runner = new GitRunner();
        RepositoryLocation location;
        try
        {
            location = RepositoryDiscovery.DiscoverAsync(runner, options.RepositoryPath, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (GitException exception)
        {
            Console.Error.WriteLine($"gitunia: {exception.Message}");
            return 1;
        }

        if (options.PrintsFrame)
        {
            FramePrinter.PrintAsync(
                runner,
                location,
                options.FrameWidth!.Value,
                options.FrameHeight!.Value,
                GraphGlyphSet.For(capabilities.Unicode),
                Console.Out,
                CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }

        using var application = new TuiApplication(runner, capabilities);
        return application.Run(location);
    }
}
