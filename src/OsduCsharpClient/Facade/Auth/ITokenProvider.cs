namespace Equinor.OsduCsharpClient.Facade.Auth;

/// <summary>
/// Abstraction for acquiring a bearer token used to authenticate OSDU API requests.
/// </summary>
public interface ITokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}
