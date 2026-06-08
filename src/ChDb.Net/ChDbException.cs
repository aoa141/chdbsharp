namespace ChDb;

/// <summary>
/// Exception thrown by chDB operations.
/// </summary>
public class ChDbException : Exception
{
    public ChDbException(string message) : base(message) { }
    public ChDbException(string message, Exception innerException) : base(message, innerException) { }
}
