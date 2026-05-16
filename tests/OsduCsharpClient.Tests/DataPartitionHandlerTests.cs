using System.Net;
using Equinor.OsduCsharpClient.Facade;
using Xunit;

namespace OsduCsharpClient.Tests;

public class DataPartitionHandlerTests
{
    [Fact]
    public async Task InjectsDataPartitionIdHeader()
    {
        HttpRequestMessage? captured = null;

        var handler = new DataPartitionHandler("my-partition")
        {
            InnerHandler = new MockHandler(req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        };

        var httpClient = new HttpClient(handler);
        await httpClient.GetAsync("https://example.com/test", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.True(captured.Headers.TryGetValues("data-partition-id", out var values));
        Assert.Equal("my-partition", values.Single());
    }

    [Fact]
    public async Task DoesNotOverrideExistingHeader()
    {
        HttpRequestMessage? captured = null;

        var handler = new DataPartitionHandler("default-partition")
        {
            InnerHandler = new MockHandler(req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        };

        var httpClient = new HttpClient(handler);
        var req = new HttpRequestMessage(HttpMethod.Get, "https://example.com/test");
        req.Headers.TryAddWithoutValidation("data-partition-id", "explicit-partition");
        await httpClient.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        // TryAddWithoutValidation does not replace an existing header; first value wins
        Assert.True(captured.Headers.TryGetValues("data-partition-id", out var values));
        Assert.Equal("explicit-partition", values.First());
    }

    private sealed class MockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(handler(request));
    }
}
