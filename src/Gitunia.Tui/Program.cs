using Gitunia.Core;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;

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

        var services = GitServices.Create(new GitRunner());
        RepositoryLocation? location = null;
        if (options.RepositoryPath is not null)
        {
            try
            {
                location = services.Discovery.DiscoverAsync(options.RepositoryPath, CancellationToken.None).GetAwaiter().GetResult();
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
                services,
                location!,
                options.FrameWidth!.Value,
                options.FrameHeight!.Value,
                GraphGlyphSet.For(capabilities.Unicode),
                Console.Out,
                new PaneArrangement(options.OpenDiff, options.Zoom),
                options.Tab,
                options.Colors,
                CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }

        using var application = new TuiApplication(services, capabilities, PlatformInfo.Current);
        return application.Run(location);
    }
}
