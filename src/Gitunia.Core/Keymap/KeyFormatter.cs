using System.Text;

namespace Gitunia.Core.Keymap;

/// <summary>Writes keys the way <c>docs/ux.md</c> does (<c>gg</c>, <c>Ctrl-w c</c>, <c>&lt;space&gt;o</c>), for display.</summary>
public static class KeyFormatter
{
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

    private static bool IsPlainNamed(KeyEvent key) => key.Modifiers == KeyModifiers.None && KeyNames.IsNamed(key.Name);

    private static bool IsLong(string token) => token.Length > 1 && token != "<space>" && !(token[0] == '<' && token[^1] == '>');
}
