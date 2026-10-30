using System.Text;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>Writes keys the way <c>docs/ux-keymap.md</c> does (<c>gg</c>, <c>Ctrl-w c</c>, <c>&lt;space&gt;o</c>), for display.</summary>
public static class KeyFormatter
{
    private const char ThinSpace = '\u2009';

    public static string Format(KeyEvent key)
    {
        if (key.Name == " ")
        {
            return "<space>";
        }

        var prefix = string.Concat(
            key.Modifiers.HasFlag(KeyModifiers.Ctrl) ? "Ctrl-" : string.Empty,
            key.Modifiers.HasFlag(KeyModifiers.Alt) ? "Alt-" : string.Empty,
            key.Modifiers.HasFlag(KeyModifiers.Shift) ? "Shift-" : string.Empty);
        return prefix + key.Name;
    }

    /// <summary>Joins the keys; a space separates them only where a plain run of characters would be ambiguous.</summary>
    public static string Format(KeySequence sequence)
    {
        var tokens = sequence.Keys.Select(key => sequence.Length > 1 && IsPlainNamed(key) ? $"<{key.Name}>" : Format(key)).ToList();
        var builder = new StringBuilder();
        for (var index = 0; index < tokens.Count; index++)
        {
            if (index > 0 && (IsLong(tokens[index]) || IsLong(tokens[index - 1])))
            {
                builder.Append(' ');
            }

            builder.Append(tokens[index]);
        }

        return builder.ToString();
    }

    /// <summary>The sequence for display: <see cref="Format(KeySequence)"/>, no ligatures.</summary>
    public static string Display(KeySequence sequence) => BreakLigatures(Format(sequence));

    /// <summary>
    /// Puts a thin space between a <c>&gt;</c> and a following <c>=</c>, <c>-</c> or <c>&gt;</c>, so a ligature font
    /// cannot draw <c>&lt;space&gt;=</c> as <c>&gt;=</c> (#753). The thin space still takes one terminal cell.
    /// </summary>
    public static string BreakLigatures(string text)
    {
        if (!text.Contains('>'))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 1);
        for (var index = 0; index < text.Length; index++)
        {
            builder.Append(text[index]);
            if (text[index] == '>' && index + 1 < text.Length && text[index + 1] is '=' or '-' or '>')
            {
                builder.Append(ThinSpace);
            }
        }

        return builder.ToString();
    }

    private static bool IsPlainNamed(KeyEvent key) => key.Modifiers == KeyModifiers.None && KeyNames.IsNamed(key.Name);

    private static bool IsLong(string token)
    {
        var isBracketed = token[0] == '<' && token[^1] == '>';

        return token == "<" || (token.Length > 1 && token != "<space>" && !isBracketed);
    }
}
