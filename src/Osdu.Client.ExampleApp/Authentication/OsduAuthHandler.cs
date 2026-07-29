using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Osdu.Client.ExampleApp.Authentication;

public sealed class OsduAuthHandler : DelegatingHandler
{
    private readonly IConfidentialClientApplication _app;
    private readonly string[] _scopes;

    public OsduAuthHandler(string tenantId, string clientId, string clientSecret, string scope)
    {
        _scopes = [scope];
        _app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .Build();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var result = await _app.AcquireTokenForClient(_scopes)
            .ExecuteAsync(cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
