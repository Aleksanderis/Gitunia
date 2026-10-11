namespace Gitunia.Tui.App;

/// <summary>Parses <c>gitunia [path] [--print-frame WxH]</c>.</summary>
public static class CommandLineParser
{
    public const string Usage = "usage: gitunia <repository path> [--print-frame <columns>x<rows>]";

    public static CommandLineOptions Parse(IReadOnlyList<string> arguments)
    {
        string? path = null;
        int? width = null;
        int? height = null;
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
            else if (argument.StartsWith("--", StringComparison.Ordinal) || path is not null)
            {
                return new CommandLineOptions(null, null, null, $"unexpected argument '{argument}'");
            }
            else
            {
                path = argument;
            }
        }

        return new CommandLineOptions(path, width, height, null);
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
