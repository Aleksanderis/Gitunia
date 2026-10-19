using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Applies the <c>keys</c> entries of the config file to the default keymap. An entry binds a key to an action
/// (replacing what that key did in that mode and view) or, with the action <c>none</c>, unbinds it. Every entry is
/// checked on its own: a bad one is reported and skipped, the others still apply, so the result is always usable.
/// An entry that would leave a key both complete and the start of a longer chord is refused, because the engine
/// always waits for the longer chord and the short binding could never fire.
/// </summary>
public static class KeymapConfigurator
{
    /// <summary>The action name that removes the binding of a key.</summary>
    public const string UnbindAction = "none";

    private const string KeysPath = "keys";

    /// <summary>Builds the keymap from the defaults and the user's entries.</summary>
    /// <param name="defaults">The default keymap; it must have no conflicts.</param>
    /// <param name="entries">The <c>keys</c> entries, null when the file has none.</param>
    public static KeymapBuildResult Apply(KeyBindingTable defaults, IReadOnlyList<ConfigKeyEntry?>? entries)
    {
        var problems = new List<ConfigProblem>();
        if (entries is not { Count: > 0 })
        {
            return new KeymapBuildResult(defaults, problems);
        }

        var knownActions = defaults.Bindings.Select(binding => binding.Action).ToHashSet();
        var views = defaults.Views;
        var current = defaults.Bindings.ToList();
        var touched = new HashSet<BindingSlot>();
        for (var index = 0; index < entries.Count; index++)
        {
            var path = $"{KeysPath}[{index}]";
            if (!TryRead(entries[index], views, knownActions, out var edit, out var error))
            {
                problems.Add(new ConfigProblem(path, error));
                continue;
            }

            if (!touched.Add(edit.Slot))
            {
                problems.Add(new ConfigProblem(path, $"{DescribeEdit(edit)} is already set by an earlier entry"));
                continue;
            }

            var candidate = Replace(current, edit);
            if (candidate is null)
            {
                problems.Add(new ConfigProblem(path, $"{DescribeEdit(edit)} is not bound, nothing to unbind"));
                continue;
            }

            var conflict = FindConflict(defaults.WithBindings(candidate), edit.Slot);
            if (conflict is not null)
            {
                problems.Add(new ConfigProblem(path, DescribeConflict(conflict)));
                continue;
            }

            current = candidate;
        }

        return new KeymapBuildResult(defaults.WithBindings(current), problems);
    }

    private static bool TryRead(
        ConfigKeyEntry? entry,
        IReadOnlyCollection<string> views,
        HashSet<ActionId> knownActions,
        out KeyEdit edit,
        out string error)
    {
        edit = default!;
        error = string.Empty;
        if (entry is null)
        {
            error = "the entry is empty";
            return false;
        }

        var mode = KeyMode.Normal;
        if (!string.IsNullOrWhiteSpace(entry.Mode)
            && !(Enum.TryParse(entry.Mode, true, out mode) && Enum.IsDefined(mode)))
        {
            error = $"unknown mode '{entry.Mode}' (use {string.Join(", ", Enum.GetNames<KeyMode>())})";
            return false;
        }

        var view = string.IsNullOrWhiteSpace(entry.View)
            ? KeyBindingTable.GlobalView
            : views.FirstOrDefault(name => name.Equals(entry.View, StringComparison.OrdinalIgnoreCase));
        if (view is null)
        {
            error = $"unknown view '{entry.View}' (use {string.Join(", ", views.Order())})";
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.Key) || !KeySequenceParser.TryParse(entry.Key, out var sequence))
        {
            error = $"'{entry.Key}' is not a key sequence (examples: J, gg, Ctrl-w c, <space>o)";
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.Action))
        {
            error = "the entry has no action";
            return false;
        }

        var isUnbind = entry.Action.Equals(UnbindAction, StringComparison.OrdinalIgnoreCase);
        var action = new ActionId(entry.Action.Trim());
        if (!isUnbind && !knownActions.Contains(action))
        {
            error = $"unknown action '{entry.Action}' (the palette lists the actions)";
            return false;
        }

        edit = new KeyEdit(new BindingSlot(mode, view, sequence), isUnbind ? null : action);
        return true;
    }

    // The bindings with the slot's binding replaced, added or removed; null when there was nothing to unbind.
    private static List<KeyBinding>? Replace(List<KeyBinding> bindings, KeyEdit edit)
    {
        var result = bindings
            .Where(binding => new BindingSlot(binding.Mode, binding.View, binding.Sequence) != edit.Slot)
            .ToList();
        if (edit.Action is not { } action)
        {
            return result.Count == bindings.Count ? null : result;
        }

        result.Add(new KeyBinding(edit.Slot.Mode, edit.Slot.View, edit.Slot.Sequence, action));

        return result;
    }

    // Looks only at the keys that share a prefix with the slot's sequence, in the slot's view and every view
    // that inherits from it, so the cost does not grow with the size of the keymap (a full conflict scan does).
    private static BindingConflict? FindConflict(KeyBindingTable table, BindingSlot slot)
    {
        foreach (var view in table.Views.Where(name => table.ViewChain(name).Contains(slot.View)))
        {
            var effective = table.EffectiveBindings(slot.Mode, view);
            var own = effective.FirstOrDefault(binding => binding.Sequence.Equals(slot.Sequence));
            var other = own is null ? null : effective.FirstOrDefault(binding => IsStrictlyRelated(binding, own));
            if (own is not null && other is not null)
            {
                var shorter = other.Sequence.Length < own.Sequence.Length ? other : own;
                var longer = shorter == own ? other : own;

                return new BindingConflict(BindingConflictKind.PrefixShadow, slot.Mode, view, shorter, longer);
            }
        }

        return null;
    }

    private static bool IsStrictlyRelated(KeyBinding candidate, KeyBinding own)
    {
        return candidate.Sequence.Length != own.Sequence.Length
            && (candidate.Sequence.StartsWith(own.Sequence) || own.Sequence.StartsWith(candidate.Sequence));
    }

    private static string DescribeConflict(BindingConflict conflict)
    {
        var first = KeyFormatter.Format(conflict.First.Sequence);
        var second = KeyFormatter.Format(conflict.Second.Sequence);
        return $"{first} ({conflict.First.Action}) cannot also start the longer key {second} ({conflict.Second.Action})"
            + $" in {conflict.Mode}/{conflict.View}: a key is either complete or the start of a chord";
    }

    private static string DescribeEdit(KeyEdit edit)
    {
        return $"{KeyFormatter.Format(edit.Slot.Sequence)} in {edit.Slot.Mode}/{edit.Slot.View}";
    }
}
