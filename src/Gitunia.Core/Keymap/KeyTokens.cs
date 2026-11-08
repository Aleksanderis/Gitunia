using System.Text;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>
/// Lets a message name the key of an action without knowing the keymap (#1196). The pure layers write a token,
/// <c>{key:status.history|the message history}</c>; where the effective keymap is known (the effect runner, before a
/// status message or a dialog is shown) <see cref="Resolve"/> turns it into <c>the message history (&lt;space&gt;m)</c>,
/// or into just <c>the message history</c> when the user left the action unbound. So a remapped key is followed and a
/// message never points at nothing.
/// </summary>
public static class KeyTokens
{
    private const string Open = "{key:";

    /// <summary>The token for <paramref name="action"/>, with <paramref name="name"/> as the words that stand for it.</summary>
    public static string Of(ActionId action, string name) => $"{Open}{action.Value}|{name}}}";

    /// <summary>Replaces every token of <paramref name="text"/>; with a null <paramref name="table"/> the names stand alone.</summary>
    public static string Resolve(string text, KeyBindingTable? table)
    {
        var start = text.IndexOf(Open, StringComparison.Ordinal);
        if (start < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var from = 0;
        while (start >= 0)
        {
            var bar = text.IndexOf('|', start);
            var end = bar < 0 ? -1 : text.IndexOf('}', bar);
            if (end < 0)
            {
                break;
            }

            builder.Append(text, from, start - from);
            var action = text[(start + Open.Length)..bar];
            var name = text[(bar + 1)..end];
            builder.Append(name);
            if (KeyOf(table, action) is { } key)
            {
                builder.Append(" (").Append(key).Append(')');
            }

            from = end + 1;
            start = text.IndexOf(Open, from, StringComparison.Ordinal);
        }

        return builder.Append(text, from, text.Length - from).ToString();
    }

    /// <summary>The key shown for <paramref name="action"/>, null when the table is null or the action is unbound.</summary>
    public static string? KeyOf(KeyBindingTable? table, ActionId action) => KeyOf(table, action.Value);

    private static string? KeyOf(KeyBindingTable? table, string action)
    {
        if (table is null)
        {
            return null;
        }

        var id = new ActionId(action);
        var binding = table.Bindings
            .Where(candidate => candidate.Action == id)
            .OrderBy(candidate => candidate.Mode == KeyMode.Normal ? 0 : 1)
            .ThenBy(candidate => candidate.View == KeyBindingTable.GlobalView ? 0 : 1)
            .ThenBy(candidate => candidate.Sequence.Length)
            .FirstOrDefault();

        return binding is null ? null : KeyFormatter.Display(binding.Sequence);
    }
}
