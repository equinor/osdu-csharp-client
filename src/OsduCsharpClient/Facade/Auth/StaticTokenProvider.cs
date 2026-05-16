namespace Equinor.OsduCsharpClient.Facade.Auth;

/// <summary>
/// Returns a pre-acquired token. Useful for testing or when the caller manages
/// token refresh externally.
/// </summary>
public sealed class StaticTokenProvider(string token) : ITokenProvider
{
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(token);
}
