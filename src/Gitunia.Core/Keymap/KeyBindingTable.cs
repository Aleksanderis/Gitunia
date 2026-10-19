using System.Collections.Concurrent;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>
/// The data the keymap engine reads: bindings plus the view hierarchy. A view inherits the bindings of its
/// parent (and finally of <see cref="GlobalView"/>); a binding in a more specific view shadows an equal
/// key sequence further up. Nothing here is hardcoded, so user config can build a table the same way.
/// </summary>
public sealed class KeyBindingTable
{
    public const string GlobalView = "Global";

    private readonly Dictionary<string, string> _viewParents;
    private readonly ConcurrentDictionary<EffectiveBindingsKey, IReadOnlyList<KeyBinding>> _effectiveCache = new();

    public KeyBindingTable(IEnumerable<KeyBinding> bindings, IReadOnlyDictionary<string, string>? viewParents = null)
    {
        Bindings = [.. bindings];
        _viewParents = viewParents is null ? [] : new Dictionary<string, string>(viewParents);
    }

    public IReadOnlyList<KeyBinding> Bindings { get; }

    /// <summary>
    /// A table with the same view hierarchy and other bindings (the effective-bindings cache is not
    /// shared).
    /// </summary>
    /// <param name="bindings">The bindings of the new table.</param>
    public KeyBindingTable WithBindings(IEnumerable<KeyBinding> bindings) => new(bindings, _viewParents);

    /// <summary>
    /// Every view name that appears in a binding or as a parent link, including <see cref="GlobalView"/>.
    /// </summary>
    public IReadOnlyCollection<string> Views
    {
        get
        {
            var views = new HashSet<string> { GlobalView };
            views.UnionWith(Bindings.Select(binding => binding.View));
            views.UnionWith(_viewParents.Keys);
            views.UnionWith(_viewParents.Values);
            return views;
        }
    }

    /// <summary>
    /// The view followed by its ancestors, most specific first, always ending in <see cref="GlobalView"/>.
    /// </summary>
    public IReadOnlyList<string> ViewChain(string view)
    {
        var chain = new List<string>();
        var current = view;
        while (current != GlobalView && !chain.Contains(current))
        {
            chain.Add(current);
            current = _viewParents.GetValueOrDefault(current, GlobalView);
        }

        chain.Add(GlobalView);
        return chain;
    }

    /// <summary>
    /// The bindings active in a mode and view after inheritance and shadowing, most specific view first.
    /// </summary>
    public IReadOnlyList<KeyBinding> EffectiveBindings(KeyMode mode, string view)
    {
        return _effectiveCache.GetOrAdd(new EffectiveBindingsKey(mode, view), key => ComputeEffective(key.Mode, key.View));
    }

    /// <summary>
    /// Finds duplicate bindings and prefixes that would hide a longer chord. An empty list means the table
    /// is consistent.
    /// </summary>
    public IReadOnlyList<BindingConflict> FindConflicts()
    {
        var conflicts = new List<BindingConflict>();
        foreach (var group in Bindings.GroupBy(binding => new BindingSlot(binding.Mode, binding.View, binding.Sequence)))
        {
            var members = group.ToList();
            for (var index = 1; index < members.Count; index++)
            {
                conflicts.Add(new BindingConflict(BindingConflictKind.Duplicate, group.Key.Mode, group.Key.View, members[0], members[index]));
            }
        }

        var seen = new HashSet<BindingPair>();
        foreach (var mode in Enum.GetValues<KeyMode>())
        {
            foreach (var view in Views)
            {
                var effective = EffectiveBindings(mode, view);
                foreach (var shorter in effective)
                {
                    foreach (var longer in effective)
                    {
                        if (longer.Sequence.Length > shorter.Sequence.Length
                            && longer.Sequence.StartsWith(shorter.Sequence)
                            && seen.Add(new BindingPair(shorter, longer)))
                        {
                            conflicts.Add(new BindingConflict(BindingConflictKind.PrefixShadow, mode, view, shorter, longer));
                        }
                    }
                }
            }
        }

        return conflicts;
    }

    private List<KeyBinding> ComputeEffective(KeyMode mode, string view)
    {
        var effective = new List<KeyBinding>();
        var taken = new HashSet<KeySequence>();
        foreach (var name in ViewChain(view))
        {
            foreach (var binding in Bindings.Where(candidate => candidate.Mode == mode && candidate.View == name))
            {
                if (taken.Add(binding.Sequence))
                {
                    effective.Add(binding);
                }
            }
        }

        return effective;
    }
}
