using Gitunia.Core.Config;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Settings.Models;

namespace Gitunia.Core.Settings;

/// <summary>
/// Builds the key reference of the settings screen from the key binding table (the defaults with the user's
/// <c>keys</c> entries applied) and the action titles. Nothing is written by hand, so it cannot drift from the keymap.
/// Normal and Visual mode bindings are listed, grouped by the view they are bound in; the text-input and confirm modes
/// are not (their keys are fixed by the dialogs).
/// </summary>
public static class KeyReferenceBuilder
{
    private const string GlobalName = KeyBindingTable.GlobalView;

    /// <summary>The reference lines, in view order (Global first, then the order the views are bound in).</summary>
    /// <param name="table">The key binding table of the running app.</param>
    public static IReadOnlyList<KeyReferenceEntry> Build(KeyBindingTable table)
    {
        var bindings = table.Bindings.Where(binding => binding.Mode is KeyMode.Normal or KeyMode.Visual).ToList();
        var sections = bindings
            .Select(binding => (binding.View, binding.Mode))
            .Distinct()
            .OrderBy(section => section.View == GlobalName ? 0 : 1)
            .ThenBy(section => section.Mode)
            .ToList();
        var entries = new List<KeyReferenceEntry>();
        foreach (var (view, mode) in sections)
        {
            var heading = mode == KeyMode.Normal ? view : $"{view} ({mode})";
            var groups = bindings
                .Where(binding => binding.View == view && binding.Mode == mode)
                .GroupBy(binding => binding.Action);
            foreach (var group in groups)
            {
                var first = group.First();
                entries.Add(new KeyReferenceEntry(
                    heading,
                    string.Join(" / ", group.Select(binding => KeyFormatter.Format(binding.Sequence))),
                    ActionCatalog.Describe(group.Key),
                    group.Key.Value,
                    Snippet(first)));
            }
        }

        return entries;
    }

    private static string Snippet(KeyBinding binding)
    {
        var parts = new List<string>();
        if (binding.Mode != KeyMode.Normal)
        {
            parts.Add($"\"mode\": {ConfigTextEditor.StringValue(binding.Mode.ToString())}");
        }

        if (binding.View != GlobalName)
        {
            parts.Add($"\"view\": {ConfigTextEditor.StringValue(binding.View)}");
        }

        parts.Add($"\"key\": {ConfigTextEditor.StringValue(KeyFormatter.Format(binding.Sequence))}");
        parts.Add($"\"action\": {ConfigTextEditor.StringValue(binding.Action.Value)}");

        return $"{{ {string.Join(", ", parts)} }}";
    }
}
