using System.Net.Http.Headers;

namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// Gives bodiless requests an empty JSON body, so they carry a <c>Content-Type</c>.
/// </summary>
/// <remarks>
/// Several OSDU endpoints reject a request that arrives without a <c>Content-Type</c>, even
/// when the operation takes no body. Storage's <c>GET /query/records</c> answers:
///
/// <code>
/// 415  {"title":"Unsupported Media Type","detail":"Content-Type 'null' is not supported."}
/// </code>
///
/// The cause is server-side: the controller declares <c>consumes</c> for the sibling
/// <c>POST /query/records</c> and Spring enforces it on the GET as well. Python clients do
/// not notice, because their HTTP libraries will send a bare <c>Content-Type</c> header on a
/// request with no body. .NET will not — <c>Content-Type</c> is a content header, and
/// <c>HttpRequestMessage</c> has nowhere to put it without content.
///
/// So the request is given content: zero bytes, typed <c>application/json</c>. Semantically
/// identical to no body, and accepted by the endpoints that demand the header.
///
/// Only applied where there is no content already, and never to GET requests that some
/// intermediary might object to carrying a body — see the method allow-list below.
/// </remarks>
public sealed class JsonContentTypeHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            request.Content = new ByteArrayContent([]);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
