using Gitunia.Core.Keymap;

namespace Gitunia.Core.Operations;

/// <summary>
/// The key tokens of the actions on an operation in progress, for the texts that name them (#1198); the effect runner
/// resolves them against the running keymap.
/// </summary>
public static class OperationKeys
{
    /// <summary>Continue the operation and its key.</summary>
    public static readonly string Continue = KeyTokens.Of(ActionIds.OperationContinue, "continue");

    /// <summary>Abort the operation and its key.</summary>
    public static readonly string Abort = KeyTokens.Of(ActionIds.OperationAbort, "abort");

    /// <summary>Skip the commit and its key.</summary>
    public static readonly string Skip = KeyTokens.Of(ActionIds.OperationSkip, "skip");

    /// <summary>Why another action must wait for the stopped operation <paramref name="name"/>.</summary>
    public static string InProgress(string name) =>
        $"A {name} is in progress: finish or abort the {name} first: {Continue} or {Abort}";
}
