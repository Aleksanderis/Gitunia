namespace Gitunia.Core.Keymap;

/// <summary>Stable identifier of an action a key binding triggers, such as <c>list.down</c>.</summary>
public readonly record struct ActionId(string Value)
{
    public override string ToString() => Value;
}
