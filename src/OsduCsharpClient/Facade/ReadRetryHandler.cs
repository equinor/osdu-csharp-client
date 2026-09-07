using System.Net;

namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// Opt-in response-based retries. Keep final responses intact for Kiota's typed
/// error handling; never retry ambiguous transport failures or mutating requests.
/// </summary>
internal sealed class ReadRetryHandler(int maxRetries, Uri? searchBaseUrl = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (maxRetries == 0 || !CanRetry(request))
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Only allowlisted Search JSON requests have bodies. Snapshot before sending
        // so even a non-seekable source can be replayed without touching upload streams.
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        for (var retry = 0; retry < maxRetries && IsTransient(response.StatusCode); retry++)
        {
            var delay = RetryDelay(response, retry, DateTimeOffset.UtcNow);
            response.Dispose();
            await WaitAsync(delay, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            using var replay = Clone(request, body);
            response = await base.SendAsync(replay, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private bool CanRetry(HttpRequestMessage request)
    {
        if (request.RequestUri is not { IsAbsoluteUri: true } uri)
            return false;

        if (uri.AbsolutePath.Split('/').Contains("sessions", StringComparer.OrdinalIgnoreCase))
            return false;

        if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
            return request.Content is null;

        if (request.Method != HttpMethod.Post || searchBaseUrl is null ||
            !string.Equals(request.Content?.Headers.ContentType?.MediaType,
                "application/json", StringComparison.OrdinalIgnoreCase))
            return false;

        var root = searchBaseUrl.AbsoluteUri.TrimEnd('/');
        return SameEndpoint(uri, new Uri(root + "/query")) ||
            SameEndpoint(uri, new Uri(root + "/query_with_cursor"));
    }

    private static bool SameEndpoint(Uri actual, Uri expected) =>
        actual.Scheme == expected.Scheme &&
        actual.IdnHost == expected.IdnHost &&
        actual.Port == expected.Port &&
        actual.AbsolutePath == expected.AbsolutePath;

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    internal static TimeSpan RetryDelay(HttpResponseMessage response, int retry, DateTimeOffset now)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta ?? (retryAfter?.Date - now);
        if (delay.HasValue)
            return delay.Value > TimeSpan.Zero ? delay.Value : TimeSpan.Zero;

        return TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(retry, 5)) + Random.Shared.NextDouble()));
    }

    private static async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        // Task.Delay has a maximum supported duration. Long Retry-After values
        // must not be shortened; HttpClient's total timeout still cancels this wait.
        while (delay > TimeSpan.Zero)
        {
            var part = delay > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : delay;
            await Task.Delay(part, cancellationToken).ConfigureAwait(false);
            delay -= part;
        }
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request, byte[]? body)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in request.Content!.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}
