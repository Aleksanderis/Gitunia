namespace Gitunia.Core.Shell;

/// <summary>Stop the program. The terminal layer leaves its main loop; the session is saved on the way out.</summary>
/// <param name="CancelWork">Cancel the running git work of every tab first.</param>
public sealed record QuitEffect(bool CancelWork) : AppEffect;

/// <summary>Save the session now and answer with <see cref="SessionFlushed"/>.</summary>
/// <param name="Force">Echoed in the answer.</param>
public sealed record FlushSessionEffect(bool Force) : AppEffect;
