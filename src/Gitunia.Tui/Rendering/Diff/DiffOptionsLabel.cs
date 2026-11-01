using System.Globalization;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The short title marker for the diff options that differ from git's defaults, for example <c>[ctx 6] [-w]</c>.
/// </summary>
public static class DiffOptionsLabel
{
    /// <param name="options">The diff options.</param>
    /// <param name="isSideBySide">The side-by-side view is switched on.</param>
    /// <param name="isShown">The pane is wide enough to show it; otherwise the marker names the width it needs.</param>
    /// <param name="showsWhitespace">Whitespace characters are drawn as marks.</param>
    public static string Of(
        DiffOptions options,
        bool isSideBySide = false,
        bool isShown = false,
        bool showsWhitespace = false)
    {
        var parts = new List<string>(4);
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

        if (showsWhitespace)
        {
            parts.Add("[ws]");
        }

        if (isSideBySide)
        {
            var needed = DiffSplitRenderer.MinimumWidth.ToString(CultureInfo.InvariantCulture);
            parts.Add(isShown ? "[split]" : $"[split {needed}+]");
        }

        return string.Join(' ', parts);
    }
}
