using System.Collections.Concurrent;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// Finds the key text a chip shows next to its label (<c>&lt;space&gt;F fetch</c>): the chip's mouse view names its
/// action (<see cref="DefaultMouseMap"/>) and the keymap names the keys, so a rebinding follows and nothing is
/// hand-written.
/// </summary>
public sealed class ChipKeys
{
    private const char CacheSeparator = '|';

    private readonly KeyBindingTable _table;
    private readonly Dictionary<string, ActionId> _actions;
    private readonly ConcurrentDictionary<string, string?> _cache = new();

    /// <summary>Creates the lookup over <paramref name="table"/>.</summary>
    public ChipKeys(KeyBindingTable table)
    {
        _table = table;
        _actions = DefaultMouseMap.CreateBindings()
            .Where(binding => binding.Gesture == MouseGesture.Click)
            .GroupBy(binding => binding.View)
            .ToDictionary(group => group.Key, group => group.First().Action);
    }

    /// <summary>The lookup over the default keymap, for rendering without a configured one (tests).</summary>
    public static ChipKeys Default { get; } = new(DefaultKeymap.Create());

    /// <summary>
    /// The shortest Normal-mode key of the action behind <paramref name="mouseView"/> in
    /// <paramref name="keyView"/>
    /// (and the views it inherits from), or null when the view has no click action or the action has no
    /// key.
    /// </summary>
    /// <param name="mouseView">The chip's mouse view, for example <c>RemoteButton.Fetch</c>.</param>
    /// <param name="keyView">The keymap view the chip sits in, for example <c>Diff</c> or <c>Global</c>.</param>
    public string? For(string mouseView, string keyView) =>
        _cache.GetOrAdd($"{mouseView}{CacheSeparator}{keyView}", _ => Find(mouseView, keyView));

    private string? Find(string mouseView, string keyView)
    {
        if (!_actions.TryGetValue(mouseView, out var action))
        {
            return null;
        }

        var binding = _table.EffectiveBindings(KeyMode.Normal, keyView)
            .Where(candidate => candidate.Action == action)
            .OrderBy(candidate => candidate.Sequence.Length)
            .FirstOrDefault();

        return binding is null ? null : KeyFormatter.Format(binding.Sequence);
    }
}
