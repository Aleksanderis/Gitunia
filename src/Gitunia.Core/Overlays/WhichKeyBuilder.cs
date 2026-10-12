using Gitunia.Core.Keymap;

namespace Gitunia.Core.Overlays;

/// <summary>Turns the engine's continuations of a pending chord into popup rows.</summary>
public static class WhichKeyBuilder
{
    public static IReadOnlyList<WhichKeyRow> Build(IReadOnlyList<Continuation> continuations)
    {
        return [.. continuations.Select(continuation => new WhichKeyRow(
            KeyFormatter.Format(continuation.Key),
            continuation is { Action: { } action }
                ? ActionCatalog.Describe(action) + (continuation.HasMore ? " (+more)" : string.Empty)
                : "+more"))];
    }
}
