using System.Runtime.CompilerServices;

namespace Gitunia.Core.Tests.Fixtures;

/// <summary>
/// Finds every concrete subtype of a sealed-by-convention hierarchy (commands, effects) and builds an empty instance of
/// each, so a test can check that a reducer or runner has a case for it. C# does not check the completeness of a switch
/// over a hierarchy. Tests may use reflection; the binary may not. Linked into Gitunia.App.Tests.
/// </summary>
public static class ConcreteSubtypes
{
    /// <summary>One uninitialised instance (fields null or zero) of every concrete type derived from <typeparamref name="T"/>.</summary>
    public static IReadOnlyList<T> InstancesOf<T>()
        where T : class
    {
#pragma warning disable IL2026, IL2067, IL2072 // Reflection is fine in tests; they are never trimmed.
        return
        [
            .. typeof(T).Assembly.GetTypes()
                .Where(type => type.IsSubclassOf(typeof(T)) && !type.IsAbstract)
                .Select(type => (T)RuntimeHelpers.GetUninitializedObject(type)),
        ];
#pragma warning restore IL2026, IL2067, IL2072
    }
}
