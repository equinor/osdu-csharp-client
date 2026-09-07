using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.Facade.Auth;
using Equinor.OsduCsharpClient.Search;
using Equinor.OsduCsharpClient.Search.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Xunit;

namespace OsduCsharpClient.Tests;

public class ReadRetryHandlerTests
{
    private static readonly Uri SearchRoot = new("https://example.com/custom/search");

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    }

    private sealed class TrackingContent() : ByteArrayContent([])
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class UnreadableUpload : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("Retry middleware must not buffer uploads.");
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            return send(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Transient(HttpStatusCode status = HttpStatusCode.ServiceUnavailable)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }

    private static HttpClient Client(StubHandler transport, int retries = 2) =>
        new(new ReadRetryHandler(retries, SearchRoot) { InnerHandler = transport });

    [Theory]
    [InlineData(false, null, 1)]
    [InlineData(true, null, 2)]
    [InlineData(true, "explicit-partition", 2)]
    public async Task FacadePipeline_RespectsOptInAndInjectsPartitionOnce(
        bool enabled, string? explicitPartition, int expectedAttempts)
    {
        var attempts = 0;
        var transport = new StubHandler((request, _) =>
        {
            attempts++;
            Assert.Equal(explicitPartition ?? "configured-partition",
                Assert.Single(request.Headers.GetValues("data-partition-id")));
            return Task.FromResult(attempts == 1 ? Transient() : new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var facade = new OsduClient(new OsduConfig
        {
            Server = "https://example.com",
            DataPartitionId = "configured-partition",
            EnableReadRetries = enabled,
            EndpointOverrides = new Dictionary<string, string> { ["search"] = SearchRoot.AbsoluteUri }
        }, new StaticTokenProvider("token"));
        using var http = new HttpClient(facade.CreateHttpPipeline("search", transport));
        using var request = new HttpRequestMessage(HttpMethod.Post, SearchRoot + "/query")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        if (explicitPartition is not null)
            request.Headers.Add("data-partition-id", explicitPartition);

        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expectedAttempts, transport.Attempts);
    }

    [Theory]
    [InlineData("GET", 429)]
    [InlineData("GET", 503)]
    [InlineData("HEAD", 504)]
    public async Task EligibleReads_RetryOnlyConfiguredNumber(string method, int status)
    {
        var transport = new StubHandler((_, _) => Task.FromResult(Transient((HttpStatusCode)status)));
        using var client = Client(transport);
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://example.com/records");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(3, transport.Attempts);
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Fact]
    public async Task ZeroRetries_LeavesTransientResponseUntouched()
    {
        var transport = new StubHandler((_, _) => Task.FromResult(Transient()));
        using var client = Client(transport, 0);
        using var response = await client.GetAsync("https://example.com/records", TestContext.Current.CancellationToken);
        Assert.Equal(1, transport.Attempts);
    }

    [Theory]
    [InlineData("POST", "https://example.com/records", "application/json")]
    [InlineData("PUT", "https://example.com/records", "application/json")]
    [InlineData("PATCH", "https://example.com/records", "application/json")]
    [InlineData("DELETE", "https://example.com/records", "application/json")]
    [InlineData("POST", "https://example.com/custom/search/query", "application/x-parquet")]
    [InlineData("POST", "https://other.example.com/custom/search/query", "application/json")]
    [InlineData("POST", "https://example.com/custom/search/query/extra", "application/json")]
    [InlineData("POST", "https://example.com/ddms/v3/welllogs/id/sessions", "application/json")]
    [InlineData("POST", "https://example.com/ddms/v3/welllogs/id/data", "application/x-parquet")]
    [InlineData("GET", "https://example.com/records", "application/json")]
    public async Task WritesUploadsAndNonAllowlistedBodies_AreNotRetried(
        string method, string url, string contentType)
    {
        var transport = new StubHandler((_, _) => Task.FromResult(Transient()));
        using var client = Client(transport);
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent("{}", Encoding.UTF8, contentType)
        };
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task SessionReads_AreNotRetried()
    {
        var transport = new StubHandler((_, _) => Task.FromResult(Transient()));
        using var client = Client(transport);
        using var response = await client.GetAsync(
            "https://example.com/ddms/v3/welllogs/id/sessions/sid", TestContext.Current.CancellationToken);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task UploadBodies_AreNotReadByRetryMiddleware()
    {
        var transport = new StubHandler((_, _) => Task.FromResult(Transient()));
        using var client = Client(transport);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/ddms/v3/welllogs/id/data")
        {
            Content = new UnreadableUpload()
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-parquet");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(1, transport.Attempts);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(502)]
    public async Task OtherStatuses_AreNotRetried(int status)
    {
        var transport = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        using var client = Client(transport);
        using var response = await client.GetAsync("https://example.com/records", TestContext.Current.CancellationToken);
        Assert.Equal(1, transport.Attempts);
    }

    [Theory]
    [InlineData("query")]
    [InlineData("query_with_cursor?searchAfter=true")]
    public async Task SearchPost_ReplaysJsonAndPreservesHeadersAndOptions(string endpoint)
    {
        var bodies = new List<string>();
        var requests = new List<HttpRequestMessage>();
        var option = new HttpRequestOptionsKey<string>("test-option");
        var transport = new StubHandler(async (request, ct) =>
        {
            requests.Add(request);
            bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("partition", Assert.Single(request.Headers.GetValues("data-partition-id")));
            Assert.Equal("Bearer token", request.Headers.Authorization?.ToString());
            Assert.Equal("application/json", request.Content.Headers.ContentType?.MediaType);
            Assert.True(request.Options.TryGetValue(option, out var value));
            Assert.Equal("preserved", value);
            Assert.Equal(HttpVersion.Version20, request.Version);
            return requests.Count == 1 ? Transient() : new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = Client(transport);
        using var request = new HttpRequestMessage(HttpMethod.Post, SearchRoot + "/" + endpoint)
        {
            Content = new StreamContent(new NonSeekableStream("""{"kind":"osdu:*:*:*"}"""u8.ToArray())),
            Version = HttpVersion.Version20
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("data-partition-id", "partition");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        request.Options.Set(option, "preserved");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, transport.Attempts);
        Assert.NotSame(requests[0], requests[1]);
        Assert.Equal(bodies[0], bodies[1]);
    }

    [Fact]
    public async Task Retry_DisposesSupersededResponseButNotFinalResponse()
    {
        var contents = new List<TrackingContent>();
        var transport = new StubHandler((_, _) =>
        {
            var response = Transient();
            var content = new TrackingContent();
            contents.Add(content);
            response.Content = content;
            return Task.FromResult(response);
        });
        using var client = Client(transport, 1);
        using var response = await client.GetAsync("https://example.com/records", TestContext.Current.CancellationToken);
        Assert.True(contents[0].IsDisposed);
        Assert.False(contents[1].IsDisposed);
    }

    [Fact]
    public async Task TransportExceptions_AreNeverRetried()
    {
        var transport = new StubHandler((_, _) => throw new HttpRequestException("ambiguous failure"));
        using var client = Client(transport);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("https://example.com/records", TestContext.Current.CancellationToken));
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task Cancellation_InterruptsRetryAfterWait()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var transport = new StubHandler((_, _) =>
        {
            var response = Transient();
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1));
            cancellation.Cancel();
            return Task.FromResult(response);
        });
        using var client = Client(transport);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync("https://example.com/records", cancellation.Token));
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task HttpClientTimeout_BoundsRetryAfterWait()
    {
        var transport = new StubHandler((_, _) =>
        {
            var response = Transient();
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromDays(60));
            return Task.FromResult(response);
        });
        using var client = Client(transport);
        client.Timeout = TimeSpan.FromMilliseconds(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync("https://example.com/records", TestContext.Current.CancellationToken));
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public void RetryAfter_UsesFullDeltaOrUtcDateWithoutShortening()
    {
        var now = DateTimeOffset.Parse("2026-09-06T12:00:00Z");
        using var response = Transient();
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(10), ReadRetryHandler.RetryDelay(response, 0, now));
        response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(10), ReadRetryHandler.RetryDelay(response, 0, now));
        response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(-1));
        Assert.Equal(TimeSpan.Zero, ReadRetryHandler.RetryDelay(response, 0, now));
        response.Headers.RetryAfter = null;
        Assert.InRange(ReadRetryHandler.RetryDelay(response, 0, now), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(30), ReadRetryHandler.RetryDelay(response, int.MaxValue, now));
    }

    [Fact]
    public async Task ExhaustedRetries_PreserveKiotaTypedErrors()
    {
        var transport = new StubHandler((_, _) =>
        {
            var response = Transient();
            response.Content = new StringContent(
                """{"code":503,"message":"busy","reason":"try later"}""", Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        });
        using var httpClient = Client(transport);
        using var adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(), httpClient: httpClient) { BaseUrl = SearchRoot.AbsoluteUri };
        var search = new SearchClient(adapter);

        var error = await Assert.ThrowsAsync<AppError>(() =>
            search.Query.PostAsync(new QueryRequest(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, transport.Attempts);
        Assert.Equal(503, error.ResponseStatusCode);
        Assert.Equal("busy", error.Message);
        Assert.Equal("try later", error.Reason);
    }
}
