using System.Globalization;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>The short title marker for the diff options that differ from git's defaults, for example <c>[ctx 6] [-w]</c>.</summary>
public static class DiffOptionsLabel
{
    public static string Of(DiffOptions options)
    {
        var parts = new List<string>(3);
        if (options.WholeFile)
        {
            parts.Add("[file]");
        }
        else if (options.ContextLines != DiffOptions.DefaultContextLines)
        {
            parts.Add($"[ctx {options.ContextLines.ToString(CultureInfo.InvariantCulture)}]");
        }

        if (options.IgnoreWhitespace)
        {
            parts.Add("[-w]");
        }

        return string.Join(' ', parts);
    }
}
