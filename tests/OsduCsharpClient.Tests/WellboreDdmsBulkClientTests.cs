using System.Net;
using System.Text;
using System.Text.Json;
using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.WellboreDdms.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Xunit;

namespace OsduCsharpClient.Tests;

public class WellboreDdmsBulkClientTests
{
    private const string BaseUrl = "https://example.com/api/os-wellbore-ddms";
    private static readonly Guid SessionId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private sealed record CapturedRequest(
        HttpMethod Method, Uri Uri, string? Accept, string? ContentType, byte[] Body);

    private static WellboreDdmsBulkClient CreateClient(
        Func<CapturedRequest, HttpResponseMessage> responder, List<CapturedRequest>? captured = null)
    {
        var handler = new MockHandler(async request =>
        {
            var body = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync();
            var capture = new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Accept.ToString() is { Length: > 0 } accept ? accept : null,
                request.Content?.Headers.ContentType?.ToString(),
                body);
            captured?.Add(capture);
            return responder(capture);
        });
        var adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(), httpClient: new HttpClient(handler))
        {
            BaseUrl = BaseUrl,
        };
        return new WellboreDdmsBulkClient(adapter);
    }

    private static HttpResponseMessage ParquetResponse(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        };
        response.Content.Headers.ContentType = new("application/x-parquet");
        return response;
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    [Fact]
    public async Task ReadParquet_SendsAcceptHeaderAndQueryParameters()
    {
        var parquetBytes = "parquet-bytes"u8.ToArray();
        var requests = new List<CapturedRequest>();
        var client = CreateClient(_ => ParquetResponse(parquetBytes), requests);

        await using var stream = await client.ReadParquetAsync(
            "opendes:work-product-component--WellLog:abc",
            new WellboreBulkReadOptions { Curves = ["MD", "GR"], Limit = 100 },
            cancellationToken: TestContext.Current.CancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        Assert.Equal(parquetBytes, buffer.ToArray());
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "/api/os-wellbore-ddms/ddms/v3/welllogs/opendes%3Awork-product-component--WellLog%3Aabc/data",
            request.Uri.AbsolutePath);
        Assert.Equal("application/x-parquet", request.Accept);
        Assert.Contains("curves=MD%2CGR", request.Uri.Query);
        Assert.Contains("limit=100", request.Uri.Query);
    }

    [Fact]
    public async Task ReadParquet_UsesVersionedUrlAndExplodesFilter()
    {
        var requests = new List<CapturedRequest>();
        var client = CreateClient(_ => ParquetResponse([]), requests);

        await client.ReadParquetAsync(
            "rid-1",
            new WellboreBulkReadOptions { Version = 7, Filter = ["MD:gt:100", "GR:lt:50"] },
            WellboreBulkEntity.WellboreTrajectories,
            TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal(
            "/api/os-wellbore-ddms/ddms/v3/wellboretrajectories/rid-1/versions/7/data",
            request.Uri.AbsolutePath);
        Assert.Contains("filter=MD%3Agt%3A100", request.Uri.Query);
        Assert.Contains("filter=GR%3Alt%3A50", request.Uri.Query);
    }

    [Fact]
    public async Task ReadParquet_ThrowsApiExceptionOnErrorStatus()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"detail":"not found"}""", Encoding.UTF8, "application/json"),
        });

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            client.ReadParquetAsync("rid-1", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(404, exception.ResponseStatusCode);
    }

    [Fact]
    public async Task WriteParquet_SendsParquetContentType()
    {
        var parquetBytes = "fake-parquet"u8.ToArray();
        var requests = new List<CapturedRequest>();
        var client = CreateClient(
            _ => JsonResponse("""{"recordId":"rid-1"}"""), requests);

        var result = await client.WriteParquetAsync(
            "rid-1", new MemoryStream(parquetBytes),
            cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/os-wellbore-ddms/ddms/v3/welllogs/rid-1/data", request.Uri.AbsolutePath);
        Assert.Equal("application/x-parquet", request.ContentType);
        Assert.Equal(parquetBytes, request.Body);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OpenSession_SendsTypedJsonBodyAndReturnsSession()
    {
        var requests = new List<CapturedRequest>();
        var client = CreateClient(
            _ => JsonResponse($$"""{"id":"{{SessionId}}","recordId":"rid-1","state":"open"}"""),
            requests);

        var session = await client.OpenSessionAsync(
            "rid-1", SessionUpdateMode.Overwrite, fromVersion: 3, timeToLiveMinutes: 60,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SessionId, session.Id);
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/os-wellbore-ddms/ddms/v3/welllogs/rid-1/sessions", request.Uri.AbsolutePath);
        Assert.Equal("application/json", request.ContentType?.Split(';')[0]);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("overwrite", body.RootElement.GetProperty("mode").GetString());
        Assert.Equal(3, body.RootElement.GetProperty("fromVersion").GetInt64());
        Assert.Equal(60, body.RootElement.GetProperty("timeToLive").GetInt64());
    }

    [Fact]
    public async Task WriteSessionChunk_PostsParquetToSessionDataUrl()
    {
        var requests = new List<CapturedRequest>();
        var client = CreateClient(_ => JsonResponse("{}"), requests);

        await client.WriteSessionChunkParquetAsync(
            "rid-1", SessionId, new MemoryStream("chunk"u8.ToArray()),
            cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            $"/api/os-wellbore-ddms/ddms/v3/welllogs/rid-1/sessions/{SessionId}/data",
            request.Uri.AbsolutePath);
        Assert.Equal("application/x-parquet", request.ContentType);
    }

    [Fact]
    public async Task CommitAndAbandon_PatchSessionState()
    {
        var requests = new List<CapturedRequest>();
        var client = CreateClient(
            _ => JsonResponse("""{"recordId":"rid-1","state":"committed"}"""), requests);

        var commit = await client.CommitSessionAsync(
            "rid-1", SessionId, cancellationToken: TestContext.Current.CancellationToken);
        await client.AbandonSessionAsync(
            "rid-1", SessionId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("rid-1", commit?.RecordId);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, r =>
        {
            Assert.Equal(HttpMethod.Patch, r.Method);
            Assert.Equal(
                $"/api/os-wellbore-ddms/ddms/v3/welllogs/rid-1/sessions/{SessionId}",
                r.Uri.AbsolutePath);
        });
        Assert.Equal("commit", JsonDocument.Parse(requests[0].Body).RootElement.GetProperty("state").GetString());
        Assert.Equal("abandon", JsonDocument.Parse(requests[1].Body).RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task WriteParquetSession_OrchestratesOpenChunksCommit()
    {
        var requests = new List<CapturedRequest>();
        var client = CreateClient(
            request => request.Uri.AbsolutePath.EndsWith("/sessions")
                ? JsonResponse($$"""{"id":"{{SessionId}}"}""")
                : JsonResponse("""{"recordId":"rid-1","state":"committed"}"""),
            requests);

        var result = await client.WriteParquetSessionAsync(
            "rid-1",
            [new MemoryStream("c1"u8.ToArray()), new MemoryStream("c2"u8.ToArray())],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("rid-1", result.RecordId);
        Assert.Equal(4, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/sessions", requests[0].Uri.AbsolutePath);
        Assert.EndsWith($"/sessions/{SessionId}/data", requests[1].Uri.AbsolutePath);
        Assert.EndsWith($"/sessions/{SessionId}/data", requests[2].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Patch, requests[3].Method);
        Assert.Equal("commit",
            JsonDocument.Parse(requests[3].Body).RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task WriteParquetSession_AbandonsOnChunkFailure()
    {
        var requests = new List<CapturedRequest>();
        var client = CreateClient(
            request => request switch
            {
                { Method.Method: "POST" } when request.Uri.AbsolutePath.EndsWith("/sessions") =>
                    JsonResponse($$"""{"id":"{{SessionId}}"}"""),
                { Method.Method: "POST" } => new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("""{"detail":"boom"}""", Encoding.UTF8, "application/json"),
                },
                _ => JsonResponse("{}"),
            },
            requests);

        await Assert.ThrowsAsync<ApiException>(() => client.WriteParquetSessionAsync(
            "rid-1", [new MemoryStream("c1"u8.ToArray())],
            cancellationToken: TestContext.Current.CancellationToken));

        var patch = Assert.Single(requests, r => r.Method == HttpMethod.Patch);
        Assert.Equal("abandon",
            JsonDocument.Parse(patch.Body).RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ReadParquet_RejectsBlankRecordId()
    {
        var client = CreateClient(_ => ParquetResponse([]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadParquetAsync(" ", cancellationToken: TestContext.Current.CancellationToken));
    }

    private sealed class MockHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) => handler(request);
    }
}
