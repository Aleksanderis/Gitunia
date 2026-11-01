namespace Gitunia.Core.Effects;

/// <summary>
/// The runner's last-resort result for an effect without a feature slice (a feature's own effects fail with that
/// feature's result command, which clears its running flag): a handler let an exception escape. It reports the message.
/// Handlers report their expected failures with their own typed result commands instead.
/// </summary>
/// <param name="Message">What to show.</param>
public sealed record EffectFailedCommand(string Message) : AppCommand;
