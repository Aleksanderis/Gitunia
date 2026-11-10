namespace Gitunia.App.Updates;

/// <summary>An update step failed; the message is written for the user and shown as it is.</summary>
public sealed class UpdateException : Exception
{
    public UpdateException(string message)
        : base(message)
    {
    }

    public UpdateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
