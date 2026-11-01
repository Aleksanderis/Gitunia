using Gitunia.Core.Picker;

namespace Gitunia.Core.Creation;

/// <summary>What the creation flow reads from outside its own session.</summary>
/// <param name="Picker">The picker the prompts open from and the result is opened through.</param>
/// <param name="BaseDirectory">Where relative paths are taken from: the active repository, or the home directory.</param>
public sealed record CreationContext(PickerSession Picker, string BaseDirectory);
