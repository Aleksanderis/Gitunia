namespace Gitunia.Core;

/// <summary>
/// A reducer got a command type it has no case for. C# does not check that a switch covers a sealed hierarchy, so every
/// reducer ends in a throw of this and a test finds every concrete command type and checks none reaches it.
/// </summary>
/// <param name="command">The command nobody handles.</param>
public sealed class UnhandledCommandException(object command)
    : InvalidOperationException($"No reducer case for {command.GetType().Name}");
