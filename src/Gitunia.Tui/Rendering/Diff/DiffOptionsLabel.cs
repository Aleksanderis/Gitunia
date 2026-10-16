using System.Globalization;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The short title marker for the diff options that differ from git's defaults, for example <c>[ctx 6] [-w]</c>.
/// </summary>
public static class DiffOptionsLabel
{
    /// <summary>A sentence for the status bar after the options changed, for example <c>Diff: 6 context lines, whitespace ignored</c>.</summary>
    public static string Describe(DiffOptions options)
    {
        var context = options.WholeFile
            ? "whole files"
            : $"{options.ContextLines.ToString(CultureInfo.InvariantCulture)} context lines";
        return options.IgnoreWhitespace ? $"Diff: {context}, whitespace ignored" : $"Diff: {context}";
    }

    /// <param name="options">The diff options.</param>
    /// <param name="isSideBySide">The side-by-side view is switched on.</param>
    /// <param name="isShown">The pane is wide enough to show it; otherwise the marker names the width it needs.</param>
    public static string Of(DiffOptions options, bool isSideBySide = false, bool isShown = false)
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
            var needed = DiffSplitRenderer.MinimumWidth.ToString(CultureInfo.InvariantCulture);
            parts.Add(isShown ? "[split]" : $"[split {needed}+]");
        }

        return string.Join(' ', parts);
    }
}
