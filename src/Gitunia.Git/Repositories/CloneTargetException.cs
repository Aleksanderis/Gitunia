using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Git.Repositories;

/// <summary>The directory chosen for a clone or init cannot be used; nothing was touched.</summary>
public sealed class CloneTargetException(string message) : GitException(message);
