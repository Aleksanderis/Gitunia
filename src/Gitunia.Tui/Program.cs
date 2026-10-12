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
        if ((options.RepositoryPath is null && !capabilities.IsInteractive) || (options.RepositoryPath is null && options.PrintsFrame))
        {
            // Not attached to a terminal (a pipe, CI) and nothing to show: print the banner and the usage.
            Console.Out.Write(HelloFrame.Render(AppInfo.Banner, capabilities));
            Console.Out.WriteLine(CommandLineParser.Usage);
            return 0;
        }

        var runner = new GitRunner();
        RepositoryLocation? location = null;
        if (options.RepositoryPath is not null)
        {
            try
            {
                location = RepositoryDiscovery.DiscoverAsync(runner, options.RepositoryPath, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (GitException exception)
            {
                Console.Error.WriteLine($"gitunia: {exception.Message}");
                return 1;
            }
        }

        if (options.PrintsFrame)
        {
            FramePrinter.PrintAsync(
                runner,
                location!,
                options.FrameWidth!.Value,
                options.FrameHeight!.Value,
                GraphGlyphSet.For(capabilities.Unicode),
                Console.Out,
                CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }

        using var application = new TuiApplication(runner, capabilities, PlatformInfo.Current);
        return application.Run(location);
    }
}
