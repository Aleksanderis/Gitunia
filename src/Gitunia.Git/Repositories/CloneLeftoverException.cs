using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Git.Repositories;

/// <summary>A clone or init stopped and the folder it made could not be removed; the user has to delete it.</summary>
public sealed class CloneLeftoverException(string path, Exception reason)
    : GitException($"stopped, but {path} could not be removed: delete it by hand", reason);
