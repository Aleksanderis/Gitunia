using System.Globalization;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The short title marker for the diff options that differ from git's defaults, for example <c>[ctx 6] [-w]</c>.
/// </summary>
public static class DiffOptionsLabel
{
    public static string Of(DiffOptions options) => Of(options, false, false);

    /// <param name="options">The diff options.</param>
    /// <param name="isSideBySide">The side-by-side view is switched on.</param>
    /// <param name="isShown">The pane is wide enough to show it; otherwise the marker names the width it needs.</param>
    public static string Of(DiffOptions options, bool isSideBySide, bool isShown)
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

        if (isSideBySide)
        {
            parts.Add(isShown ? "[split]" : $"[split {DiffSplitRenderer.MinimumWidth.ToString(CultureInfo.InvariantCulture)}+]");
        }

        return string.Join(' ', parts);
    }
}
