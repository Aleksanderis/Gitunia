using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.App;

/// <summary>Parses <c>gitunia [path] [--print-frame WxH [--diff] [--zoom graph|detail|diff] [--tab details|changes|files]]</c>.</summary>
public static class CommandLineParser
{
    public const string Usage = "usage: gitunia <repository path> [--print-frame <columns>x<rows> [--diff] [--zoom graph|detail|diff] [--tab details|changes|files]]";

    public static CommandLineOptions Parse(IReadOnlyList<string> arguments)
    {
        string? path = null;
        int? width = null;
        int? height = null;
        var openDiff = false;
        PaneKind? zoom = null;
        BottomTab? tab = null;
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
            else if (argument.StartsWith('-') || path is not null)
            {
                return new CommandLineOptions(null, null, null, $"unexpected argument '{argument}'");
            }
            else
            {
                path = argument;
            }
        }

        if ((openDiff || zoom is not null || tab is not null) && width is null)
        {
            return new CommandLineOptions(null, null, null, "--diff, --zoom and --tab need --print-frame");
        }

        return new CommandLineOptions(path, width, height, null, openDiff || zoom == PaneKind.Diff, zoom, tab ?? BottomTab.Changes);
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
