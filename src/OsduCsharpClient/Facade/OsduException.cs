namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// Reports configuration errors and selected facade or token-provider failures.
/// HTTP error responses use Kiota ApiException or its generated subclasses.
/// Authentication providers may also propagate their own exception types.
/// </summary>
public class OsduException : Exception
{
    public OsduException(string message) : base(message) { }

    public OsduException(string message, Exception inner) : base(message, inner) { }
}
