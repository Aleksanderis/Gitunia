using System.Text.RegularExpressions;
using Gitunia.Core.Keymap;

namespace Gitunia.Core.Tests.Keymap;

/// <summary>Reads the keymap table in <c>docs/ux.md</c> into bindings so tests can compare it with the defaults.</summary>
public static partial class UxKeymapReader
{
    public static string FindUxDocument()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "ux.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("docs/ux.md not found above " + AppContext.BaseDirectory);
    }

    /// <summary>One binding per documented key token; Picker rows are Insert mode, Palette rows Command mode, all others Normal.</summary>
    public static IReadOnlyList<KeyBinding> ReadBindings()
    {
        var bindings = new List<KeyBinding>();
        var inKeymap = false;
        foreach (var line in File.ReadAllLines(FindUxDocument()))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inKeymap = line.Trim() == "## Keymap";
                continue;
            }

            if (!inKeymap || !line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
            if (cells.Length < 3 || cells[0] == "Mode/View" || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            var view = cells[0];
            const string visualSuffix = " (Visual)";
            var isVisual = view.EndsWith(visualSuffix, StringComparison.Ordinal);
            view = isVisual ? view[..^visualSuffix.Length] : view;
            var mode = isVisual ? KeyMode.Visual : view switch
            {
                DefaultKeymap.PickerView => KeyMode.Insert,
                DefaultKeymap.PaletteView => KeyMode.Command,
                _ => KeyMode.Normal,
            };
            foreach (var token in ReadTokens(cells[1]))
            {
                bindings.Add(new KeyBinding(mode, view, KeySequenceParser.Parse(token), new ActionId(cells[2])));
            }
        }

        return bindings;
    }

    private static IEnumerable<string> ReadTokens(string keyCell)
    {
        var expanded = RangePattern().Replace(keyCell, match => string.Join(' ', Enumerable
            .Range(match.Groups[2].Value[0], match.Groups[3].Value[0] - match.Groups[2].Value[0] + 1)
            .Select(code => $"`{match.Groups[1].Value}{(char)code}`")));
        foreach (Match match in TokenPattern().Matches(expanded))
        {
            var token = match.Groups[1].Value;
            if (token.Length > 1 && token[0] == ':')
            {
                continue;
            }

            yield return token;
        }
    }

    [GeneratedRegex(@"`([A-Za-z]+-)(\d)`\.\.`\1(\d)`")]
    private static partial Regex RangePattern();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex TokenPattern();
}
