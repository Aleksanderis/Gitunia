namespace Gitunia.Core.Keymap;

public sealed record BindingConflict(BindingConflictKind Kind, KeyMode Mode, string View, KeyBinding First, KeyBinding Second)
{
    public override string ToString() => $"{Kind} in {Mode}/{View}: {First.Sequence} ({First.Action}, {First.View}) vs {Second.Sequence} ({Second.Action}, {Second.View})";
}
