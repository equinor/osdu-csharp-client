using System.Net;
using Equinor.OsduCsharpClient.Facade;
using Xunit;

namespace OsduCsharpClient.Tests;

/// <summary>
/// Storage's <c>GET /query/records</c> answers a bodiless request with
/// <c>415 "Content-Type 'null' is not supported"</c>, so requests need a content type even
/// when they have nothing to send.
/// </summary>
public class JsonContentTypeHandlerTests
{
    private static async Task<HttpRequestMessage> SendAsync(HttpRequestMessage request)
    {
        HttpRequestMessage? captured = null;
        var handler = new JsonContentTypeHandler
        {
            InnerHandler = new MockHandler(req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
        };

        using var client = new HttpClient(handler);
        await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.NotNull(captured);
        return captured;
    }

    [Fact]
    public async Task GivesABodilessGetAJsonContentType()
    {
        var captured = await SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.com/q"));

        Assert.NotNull(captured.Content);
        Assert.Equal("application/json", captured.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TheAddedBodyIsEmpty()
    {
        // Semantically still "no body" — only the header the server insists on is added.
        var captured = await SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.com/q"));

        Assert.Empty(await captured.Content!.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LeavesAnExistingBodyAlone()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/q")
        {
            Content = new StringContent("""{"kind":"x"}""", System.Text.Encoding.UTF8, "application/json"),
        };

        var captured = await SendAsync(request);

        Assert.Equal("""{"kind":"x"}""",
            await captured.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DoesNotOverrideADifferentContentType()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/q")
        {
            Content = new ByteArrayContent([1, 2, 3])
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-parquet") },
            },
        };

        var captured = await SendAsync(request);

        Assert.Equal("application/x-parquet", captured.Content!.Headers.ContentType?.MediaType);
    }

    private sealed class MockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(handler(request));
    }
}
