using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Platform;
using Gitunia.Tui.Views.Theme;

namespace Gitunia.Tui.App.CommandLine;

/// <summary>Parses <c>gitunia [--version] [path] [--print-frame WxH [--diff] [--zoom graph|detail|diff] [--tab details|changes|files] [--colors truecolor|16|none [--light]]]</c>.</summary>
public static class CommandLineParser
{
    public const string Usage = "usage: gitunia <repository path> | --version [--print-frame <columns>x<rows> [--diff] [--zoom graph|detail|diff] [--tab details|changes|files] [--colors truecolor|16|none [--light]]]";

    public static CommandLineOptions Parse(IReadOnlyList<string> arguments)
    {
        string? path = null;
        int? width = null;
        int? height = null;
        var openDiff = false;
        PaneKind? zoom = null;
        BottomTab? tab = null;
        ColorDepth? colors = null;
        var isLight = false;
        if (arguments.Contains("--version"))
        {
            return new CommandLineOptions(null, null, null, null, ShowsVersion: true);
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument == "--print-frame")
            {
                if (index + 1 >= arguments.Count || !TryParseSize(arguments[index + 1], out var parsedWidth, out var parsedHeight))
                {
                    return new CommandLineOptions(null, null, null, "--print-frame needs a size such as 120x40");
                }

                width = parsedWidth;
                height = parsedHeight;
                index++;
            }
            else if (argument == "--diff")
            {
                openDiff = true;
            }
            else if (argument == "--zoom")
            {
                if (index + 1 >= arguments.Count || !TryParseZoom(arguments[index + 1], out var parsedZoom))
                {
                    return new CommandLineOptions(null, null, null, "--zoom needs graph, detail or diff");
                }

                zoom = parsedZoom;
                index++;
            }
            else if (argument == "--tab")
            {
                if (index + 1 >= arguments.Count || !TryParseTab(arguments[index + 1], out var parsedTab))
                {
                    return new CommandLineOptions(null, null, null, "--tab needs details, changes or files");
                }

                tab = parsedTab;
                index++;
            }
            else if (argument == "--colors")
            {
                if (index + 1 >= arguments.Count || !TryParseColors(arguments[index + 1], out var parsedColors))
                {
                    return new CommandLineOptions(null, null, null, "--colors needs truecolor, 16 or none");
                }

                colors = parsedColors;
                index++;
            }
            else if (argument == "--light")
            {
                isLight = true;
            }
            else if (argument.StartsWith('-') || path is not null)
            {
                return new CommandLineOptions(null, null, null, $"unexpected argument '{argument}'");
            }
            else
            {
                path = argument;
            }
        }

        if ((openDiff || zoom is not null || tab is not null || colors is not null || isLight) && width is null)
        {
            return new CommandLineOptions(null, null, null, "--diff, --zoom, --tab, --colors and --light need --print-frame");
        }

        if (isLight && colors is null)
        {
            return new CommandLineOptions(null, null, null, "--light needs --colors");
        }

        return new CommandLineOptions(
            path,
            width,
            height,
            null,
            openDiff || zoom == PaneKind.Diff,
            zoom,
            tab ?? BottomTab.Changes,
            colors is { } depth ? new PaletteSettings(depth, isLight) : null);
    }

    private static bool TryParseColors(string text, out ColorDepth depth)
    {
        depth = ColorDepth.None;
        switch (text)
        {
            case "truecolor":
                depth = ColorDepth.TrueColor;
                return true;
            case "16":
                depth = ColorDepth.Ansi16;
                return true;
            case "none":
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseTab(string text, out BottomTab tab)
    {
        tab = BottomTab.Changes;
        switch (text)
        {
            case "details":
                tab = BottomTab.Details;
                return true;
            case "changes":
                return true;
            case "files":
                tab = BottomTab.Files;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseZoom(string text, out PaneKind zoom)
    {
        zoom = text switch
        {
            "graph" => PaneKind.Graph,
            "detail" => PaneKind.Detail,
            "diff" => PaneKind.Diff,
            _ => PaneKind.Sidebar,
        };
        return zoom != PaneKind.Sidebar;
    }

    private static bool TryParseSize(string text, out int width, out int height)
    {
        width = 0;
        height = 0;
        var parts = text.Split('x');
        return parts.Length == 2
            && int.TryParse(parts[0], out width)
            && int.TryParse(parts[1], out height)
            && width is > 0 and <= 1000
            && height is > 0 and <= 1000;
    }
}
