namespace Gitunia.Core.Integration;

/// <summary>What the user asks of the operation in progress.</summary>
public enum OperationAction
{
    /// <summary>Go on after the conflicts are resolved.</summary>
    Continue,

    /// <summary>Undo the operation.</summary>
    Abort,

    /// <summary>Drop the commit it stopped on.</summary>
    Skip,
}
