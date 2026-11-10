using System.Runtime.InteropServices;
using Gitunia.App;
using Gitunia.App.Updates;
using Gitunia.Core;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform;
using Gitunia.Platform.Updates;
using Gitunia.Tui.App;
using Gitunia.Tui.App.CommandLine;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views.Theme;

namespace Gitunia.Tui;

public static class Program
{
    // Same variable the install scripts read: another release JSON to read (tests, mirrors). It moves the archive and
    // its checksum together, so it is no authenticity check.
    private const string ReleaseUrlVariable = "GITUNIA_RELEASE_URL";

    public static int Main(string[] arguments)
    {
        if (arguments is [SequenceEditorCommand.ModeArgument, .. var editorArguments])
        {
            // Git calls this executable as its sequence editor during an interactive rebase: no UI, no discovery.
            return SequenceEditor.Copy(editorArguments);
        }

        RemoveUpdateLeftovers();

        var options = CommandLineParser.Parse(arguments);
        if (options.Error is not null)
        {
            Console.Error.WriteLine($"gitunia: {options.Error}");
            Console.Error.WriteLine(CommandLineParser.Usage);
            return 2;
        }

        if (options.ShowsVersion)
        {
            Console.Out.WriteLine($"{AppInfo.Name} {BuildVersion.Value}");
            return 0;
        }

        if (options.RunsUpdate)
        {
            return RunUpdate(options.UpdateCheckOnly);
        }

        var capabilities = PlatformInfo.Current.DetectConsole();
        if ((options.RepositoryPath is null && !capabilities.IsInteractive) || (options.RepositoryPath is null && options.PrintsFrame))
        {
            // Not attached to a terminal (a pipe, CI) and nothing to show: print the banner and the usage.
            Console.Out.Write(HelloFrame.Render(AppInfo.Banner, capabilities));
            Console.Out.WriteLine(CommandLineParser.Usage);
            return 0;
        }

        var services = GitServices.Create(new GitRunner(), new PaletteLaneColors());
        RepositoryLocation? location = null;
        if (options.RepositoryPath is not null)
        {
            try
            {
                // Entry point of a synchronous Main, nothing else is running yet: blocking is safe.
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
            // The frame follows the user's config for the label caps (#851), so a capture shows what the app draws.
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configured = new ConfigStartup(
                PlatformInfo.Current.ConfigDirectory, home, ConfigStartup.DefaultLabelCaps(capabilities));
            var labelCaps = configured.Load().Options.LabelCaps;
            var hasColors = options.Colors is not null && options.Colors.Depth != ColorDepth.None;
            // The headless frame print is a one-shot command-line path; nothing else is running, so blocking is safe.
            new FramePrinter(services).PrintAsync(
                location!,
                options.FrameWidth!.Value,
                options.FrameHeight!.Value,
                GraphGlyphSet.For(capabilities.Unicode).WithLabelCaps(labelCaps, hasColors),
                Console.Out,
                new PaneArrangement(options.OpenDiff, options.Zoom),
                options.Tab,
                options.Colors,
                CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }

        var application = new TuiApplication(services, capabilities, PlatformInfo.Current);
        return application.Run(location);
    }

    // On Windows an update renames the running .exe aside; the next start removes it. Elsewhere this does nothing.
    private static void RemoveUpdateLeftovers()
    {
        var executable = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(executable))
        {
            new ExecutableSwapper().RemoveLeftovers(executable);
        }
    }

    // `gitunia update`: runs without the UI, writes to the console, Ctrl-C cancels the download.
    private static int RunUpdate(bool checkOnly)
    {
        var releaseUrl = Environment.GetEnvironmentVariable(ReleaseUrlVariable);
        var wanted = string.IsNullOrEmpty(releaseUrl) ? ReleaseClient.DefaultLatestReleaseUrl : releaseUrl;
        if (!Uri.TryCreate(wanted, UriKind.Absolute, out var latestUrl))
        {
            Console.Error.WriteLine($"gitunia update: {ReleaseUrlVariable} is not a valid address: {wanted}");

            return UpdateExitCode.Failed;
        }

        using var httpClient = ReleaseClient.Create(BuildVersion.Value);
        var command = new UpdateCommand(new ReleaseClient(httpClient, latestUrl), new ExecutableSwapper(), Console.Out);
        var request = new UpdateRequest(
            BuildVersion.Value,
            new InstallFactsReader().Read(),
            RuntimeInformation.OSArchitecture,
            checkOnly);
        using var interrupt = new InterruptCancellation();

        try
        {
            // A synchronous Main with nothing else running: blocking is safe.
            return command.RunAsync(request, interrupt.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("gitunia update: cancelled");

            return UpdateExitCode.Failed;
        }
    }
}
