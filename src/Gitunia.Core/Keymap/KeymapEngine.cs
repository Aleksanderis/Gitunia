using System.Collections.Immutable;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>
/// Pure modal key handling: <c>(state, key, view) -> result</c>. Reads bindings from a
/// <see cref="KeyBindingTable"/>; knows chords (no timeout, Esc cancels), numeric counts and the
/// text-capturing modes, but nothing about what actions do or about any renderer.
/// </summary>
public sealed class KeymapEngine(KeyBindingTable table)
{
    private const int MaximumCount = 999_999;

    public KeyBindingTable Table { get; } = table;

    /// <summary>Feeds one key press for the focused <paramref name="view"/> and returns the next state and what happened.</summary>
    public KeyResult Process(KeymapState state, KeyEvent rawKey, string view)
    {
        var key = rawKey.Normalize();
        var hasPending = state.HasPending;

        if (key is { Name: KeyNames.Escape, Modifiers: KeyModifiers.None } && (hasPending || state.Count is not null))
        {
            return new KeyResult(Cleared(state), KeyOutcome.Cancelled);
        }

        if (!hasPending && IsTextMode(state.Mode) && key.IsPrintable)
        {
            return new KeyResult(state, KeyOutcome.TextInput, Text: key.Name);
        }

        if (!hasPending && AcceptsCounts(state.Mode) && TryExtendCount(state, key, out var counted))
        {
            return new KeyResult(counted, KeyOutcome.Pending, Count: counted.Count);
        }

        var sequence = state.PendingSequence.Append(key);
        var effective = Table.EffectiveBindings(state.Mode, view);
        if (effective.Any(binding => binding.Sequence.Length > sequence.Length && binding.Sequence.StartsWith(sequence)))
        {
            return new KeyResult(state with { Pending = sequence.Keys }, KeyOutcome.Pending, Count: state.Count);
        }

        var exact = effective.FirstOrDefault(binding => binding.Sequence.Equals(sequence));
        if (exact is not null)
        {
            return new KeyResult(Cleared(state), KeyOutcome.Action, exact.Action, state.Count);
        }

        return new KeyResult(Cleared(state), hasPending ? KeyOutcome.Discarded : KeyOutcome.Unbound);
    }

    /// <summary>
    /// The keys that can follow the pending prefix in <paramref name="state"/> (or the first keys when nothing
    /// is pending), for the which-key popup. The renderer decides when to show it (500 ms after the prefix).
    /// </summary>
    public IReadOnlyList<Continuation> Continuations(KeymapState state, string view)
    {
        var prefix = state.PendingSequence;
        var continuations = new List<Continuation>();
        foreach (var binding in Table.EffectiveBindings(state.Mode, view))
        {
            if (binding.Sequence.Length <= prefix.Length || !binding.Sequence.StartsWith(prefix))
            {
                continue;
            }

            var next = binding.Sequence.Keys[prefix.Length];
            var isLast = binding.Sequence.Length == prefix.Length + 1;
            var existing = continuations.FindIndex(continuation => continuation.Key == next);
            if (existing < 0)
            {
                continuations.Add(new Continuation(next, isLast ? binding.Action : null, !isLast));
            }
            else if (isLast)
            {
                continuations[existing] = continuations[existing] with { Action = binding.Action };
            }
            else
            {
                continuations[existing] = continuations[existing] with { HasMore = true };
            }
        }

        return continuations;
    }

    private static bool IsTextMode(KeyMode mode) => mode is KeyMode.Insert or KeyMode.Command;

    private static bool AcceptsCounts(KeyMode mode) => mode is KeyMode.Normal or KeyMode.Visual;

    private static KeymapState Cleared(KeymapState state) => state with { Pending = ImmutableArray<KeyEvent>.Empty, Count = null };

    private static bool TryExtendCount(KeymapState state, KeyEvent key, out KeymapState counted)
    {
        counted = state;
        if (key.Modifiers != KeyModifiers.None || key.Name.Length != 1 || !char.IsAsciiDigit(key.Name[0]))
        {
            return false;
        }

        var digit = key.Name[0] - '0';
        if (digit == 0 && state.Count is null)
        {
            return false;
        }

        var count = Math.Min(((state.Count ?? 0) * 10) + digit, MaximumCount);
        counted = state with { Count = count };
        return true;
    }
}
