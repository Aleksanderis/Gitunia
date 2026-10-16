namespace Gitunia.Core.Persistence;

/// <summary>
/// The part of a tab's commit draft that the session file keeps (#347): message, SignOff and No-Verify, not Amend.
/// </summary>
/// <param name="Message">The unsent message text, empty when only an option is set.</param>
/// <param name="SignOff">Adds a <c>Signed-off-by</c> trailer.</param>
/// <param name="NoVerify">Skips the commit hooks.</param>
public sealed record SessionDraft(string Message, bool SignOff, bool NoVerify)
{
    /// <summary>True when there is nothing worth saving.</summary>
    public bool IsEmpty => Message.Trim().Length == 0 && !SignOff && !NoVerify;
}
