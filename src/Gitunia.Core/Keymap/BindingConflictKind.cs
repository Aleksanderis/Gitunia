namespace Gitunia.Core.Keymap;

public enum BindingConflictKind
{
    /// <summary>Two bindings with the same mode, view and key sequence.</summary>
    Duplicate,

    /// <summary>A sequence that is a proper prefix of another one reachable in the same view, so it could never fire (there is no chord timeout).</summary>
    PrefixShadow,
}
