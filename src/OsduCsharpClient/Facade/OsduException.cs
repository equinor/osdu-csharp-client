namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// Thrown when an OSDU API call returns a non-2xx status code or encounters an
/// auth/configuration error.
/// </summary>
public class OsduException : Exception
{
    public OsduException(string message) : base(message) { }

    public OsduException(string message, Exception inner) : base(message, inner) { }
}
