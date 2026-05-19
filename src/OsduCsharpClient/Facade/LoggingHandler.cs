using Microsoft.Extensions.Logging;

namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// Delegating handler that logs outgoing HTTP requests and incoming responses.
///
/// Two log categories are used (matching the Python client's pattern):
/// <list type="bullet">
///   <item><c>Equinor.OsduCsharpClient</c> — method, URL, status, elapsed time (Debug)</item>
///   <item><c>Equinor.OsduCsharpClient.Body</c> — request/response bodies (Debug, opt-in)</item>
/// </list>
///
/// Bodies are off by default. Enable them by setting the
/// <c>Equinor.OsduCsharpClient.Body</c> logger to <c>Debug</c> in your
/// logging configuration. Bodies are truncated to <see cref="BodyMaxBytes"/> bytes and
/// sensitive headers (<c>Authorization</c>, <c>Cookie</c>) are redacted.
/// </summary>
internal sealed class LoggingHandler(ILoggerFactory loggerFactory) : DelegatingHandler
{
    internal const int BodyMaxBytes = 2048;

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        { "Authorization", "Cookie", "Proxy-Authorization", "Set-Cookie" };

    private readonly ILogger _log = loggerFactory.CreateLogger("Equinor.OsduCsharpClient");
    private readonly ILogger _bodyLog = loggerFactory.CreateLogger("Equinor.OsduCsharpClient.Body");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LogRequest(request);

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var response = await base.SendAsync(request, cancellationToken);
        var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        LogResponse(request, response, elapsedMs);

        if (_bodyLog.IsEnabled(LogLevel.Debug))
        {
            var body = await ReadBodyAsync(response.Content);
            _bodyLog.LogDebug("← body={Body}", Truncate(body));
        }

        return response;
    }

    private void LogRequest(HttpRequestMessage request)
    {
        _log.LogDebug("→ {Method} {Url}", request.Method, request.RequestUri);
        if (_bodyLog.IsEnabled(LogLevel.Debug) && request.Content is not null)
        {
            // Read body synchronously via a copy so the original stream is preserved.
            var body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            _bodyLog.LogDebug("→ {Method} {Url} headers={Headers} body={Body}",
                request.Method, request.RequestUri,
                RedactHeaders(request.Headers),
                Truncate(body));
        }
    }

    private void LogResponse(HttpRequestMessage request, HttpResponseMessage response, double elapsedMs)
    {
        _log.LogDebug("← {Status} {Method} {Url} ({Elapsed:F1}ms)",
            (int)response.StatusCode, request.Method, request.RequestUri, elapsedMs);
    }

    private static async Task<string> ReadBodyAsync(HttpContent? content)
    {
        if (content is null) return "<none>";
        try { return await content.ReadAsStringAsync(); }
        catch { return "<unreadable>"; }
    }

    private static string Truncate(string? text)
    {
        if (text is null) return "<none>";
        return text.Length > BodyMaxBytes
            ? $"{text[..BodyMaxBytes]}... [truncated {text.Length - BodyMaxBytes} bytes]"
            : text;
    }

    private static Dictionary<string, string> RedactHeaders(
        System.Net.Http.Headers.HttpRequestHeaders headers) =>
        headers.ToDictionary(
            h => h.Key,
            h => SensitiveHeaders.Contains(h.Key) ? "***REDACTED***" : string.Join(", ", h.Value));
}
