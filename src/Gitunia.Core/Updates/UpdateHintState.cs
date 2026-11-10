namespace Gitunia.Core.Updates;

/// <summary>What the state keeps of the update hint: whether a check is allowed and the newer release it found.</summary>
/// <param name="Allowed">The last request allowed the check; a result that arrives while this is false (the setting was turned off meanwhile) is dropped.</param>
/// <param name="Notice">The newer release to show, null for none.</param>
public sealed record UpdateHintState(bool Allowed, UpdateNotice? Notice)
{
    /// <summary>No request yet: nothing is shown.</summary>
    public static UpdateHintState Empty { get; } = new(false, null);
}
