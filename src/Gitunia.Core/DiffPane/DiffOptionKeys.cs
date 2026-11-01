using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Core.DiffPane;

/// <summary>The keys that change how a diff is read (context lines, whole file, whitespace) and the sentence for the status bar.</summary>
public static class DiffOptionKeys
{
    /// <summary>Reads the options a key changes, false when the key is not an option key.</summary>
    /// <param name="options">The options now.</param>
    /// <param name="action">The key.</param>
    /// <param name="changed">The options after the key.</param>
    public static bool TryResolve(DiffOptions options, ActionId action, out DiffOptions changed)
    {
        if (action == ActionIds.DiffMoreContext)
        {
            changed = options.MoreContext();
        }
        else if (action == ActionIds.DiffLessContext)
        {
            changed = options.LessContext();
        }
        else if (action == ActionIds.DiffToggleWholeFile)
        {
            changed = options with { WholeFile = !options.WholeFile };
        }
        else if (action == ActionIds.DiffToggleIgnoreWhitespace)
        {
            changed = options with { IgnoreWhitespace = !options.IgnoreWhitespace };
        }
        else
        {
            changed = options;

            return false;
        }

        return true;
    }

    /// <summary>A sentence for the status bar after the options changed, for example <c>Diff: 6 context lines, whitespace ignored</c>.</summary>
    /// <param name="options">The options.</param>
    public static string Describe(DiffOptions options)
    {
        var context = $"{options.ContextLines.ToString(CultureInfo.InvariantCulture)} context lines";
        if (options.WholeFile)
        {
            context = "whole files";
        }

        return options.IgnoreWhitespace ? $"Diff: {context}, whitespace ignored" : $"Diff: {context}";
    }
}
