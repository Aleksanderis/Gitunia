namespace Gitunia.App;

/// <summary>
/// The runner or a handler got an effect type it has no case for. C# does not check that a switch covers a sealed
/// hierarchy, so every effect switch ends in a throw of this and a test checks that no concrete effect reaches it.
/// </summary>
/// <param name="effect">The effect nobody handles.</param>
public sealed class UnhandledEffectException(object effect)
    : InvalidOperationException($"No handler for {effect.GetType().Name}");
