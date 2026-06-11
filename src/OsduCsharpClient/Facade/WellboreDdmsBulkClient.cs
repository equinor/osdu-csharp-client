using Equinor.OsduCsharpClient.WellboreDdms.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// The Wellbore DDMS bulk entity types whose <c>/ddms/v3/{entity}/{id}/data</c>
/// endpoints support Parquet.
/// </summary>
public enum WellboreBulkEntity
{
    WellLogs,
    WellboreTrajectories,
    PpfgDataset,
    WellPressureTestRawMeasurement,
}

/// <summary>Optional query parameters for <see cref="WellboreDdmsBulkClient.ReadParquetAsync"/>.</summary>
public sealed record WellboreBulkReadOptions
{
    /// <summary>Read this specific record version instead of the latest.</summary>
    public long? Version { get; init; }

    /// <summary>Number of rows to skip.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of rows to return.</summary>
    public int? Limit { get; init; }

    /// <summary>Curves (columns) to return, in the given order.</summary>
    public IReadOnlyCollection<string>? Curves { get; init; }

    /// <summary>Row filters of the form <c>column:operator:value</c> (lt, lte, gt, gte, eq, neq, in).</summary>
    public IReadOnlyCollection<string>? Filter { get; init; }
}

/// <summary>
/// Hand-written Wellbore DDMS bulk-data client for <c>application/x-parquet</c>.
///
/// The generated client is JSON-only for the <c>/data</c> bulk endpoints (Kiota models a
/// single content type per direction — microsoft/kiota#3377), while Parquet is the
/// primary, performant format for well-log bulk data. WBDDMS validates the exact media
/// type (it rejects <c>application/octet-stream</c>), so these helpers send real
/// <c>application/x-parquet</c> over the same authenticated transport as the generated
/// client. See issue #53.
///
/// Payloads are raw Parquet <see cref="Stream"/>s; the package takes no dependency on a
/// Parquet library. Encode/decode with e.g. <c>Parquet.Net</c> or <c>Apache.Arrow</c>.
/// </summary>
public sealed class WellboreDdmsBulkClient
{
    /// <summary>The Parquet media type WBDDMS expects on bulk reads and writes.</summary>
    public const string ParquetMediaType = "application/x-parquet";

    private const string JsonMediaType = "application/json";

    private static readonly Dictionary<string, ParsableFactory<IParsable>> ErrorMapping = new()
    {
        { "422", HTTPValidationError.CreateFromDiscriminatorValue },
    };

    private readonly IRequestAdapter _adapter;

    /// <param name="requestAdapter">
    /// Authenticated adapter with <c>BaseUrl</c> set to the Wellbore DDMS service root.
    /// Usually obtained via <see cref="OsduClient.WellboreDdmsBulk"/>.
    /// </param>
    public WellboreDdmsBulkClient(IRequestAdapter requestAdapter)
    {
        _adapter = requestAdapter ?? throw new ArgumentNullException(nameof(requestAdapter));
        ApiClientBuilder.RegisterDefaultSerializer<JsonSerializationWriterFactory>();
        ApiClientBuilder.RegisterDefaultDeserializer<JsonParseNodeFactory>();
    }

    /// <summary>
    /// Reads bulk data as Parquet (<c>Accept: application/x-parquet</c>) and returns the
    /// raw Parquet response stream.
    /// </summary>
    public async Task<Stream> ReadParquetAsync(
        string recordId,
        WellboreBulkReadOptions? options = null,
        WellboreBulkEntity entity = WellboreBulkEntity.WellLogs,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        options ??= new WellboreBulkReadOptions();

        var template = options.Version is null
            ? "{+baseurl}/ddms/v3/{entity}/{record_id}/data{?offset*,limit*,curves*,filter*}"
            : "{+baseurl}/ddms/v3/{entity}/{record_id}/versions/{version}/data{?offset*,limit*,curves*,filter*}";
        var requestInfo = new RequestInformation(
            Method.GET, template, PathParameters(entity, recordId, options.Version));
        if (options.Offset is not null)
            requestInfo.QueryParameters.Add("offset", options.Offset.Value);
        if (options.Limit is not null)
            requestInfo.QueryParameters.Add("limit", options.Limit.Value);
        if (options.Curves is { Count: > 0 })
            requestInfo.QueryParameters.Add("curves", string.Join(",", options.Curves));
        if (options.Filter is { Count: > 0 })
            requestInfo.QueryParameters.Add("filter", options.Filter.ToArray());
        requestInfo.Headers.TryAdd("Accept", ParquetMediaType);

        var stream = await _adapter.SendPrimitiveAsync<Stream>(
            requestInfo, ErrorMapping, cancellationToken).ConfigureAwait(false);
        return stream ?? Stream.Null;
    }

    /// <summary>
    /// Writes bulk data as Parquet (<c>Content-Type: application/x-parquet</c>),
    /// creating a new record version. Returns the server's JSON response (the new
    /// version metadata).
    /// </summary>
    public async Task<UntypedNode?> WriteParquetAsync(
        string recordId,
        Stream parquet,
        WellboreBulkEntity entity = WellboreBulkEntity.WellLogs,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentNullException.ThrowIfNull(parquet);

        var requestInfo = new RequestInformation(
            Method.POST, "{+baseurl}/ddms/v3/{entity}/{record_id}/data",
            PathParameters(entity, recordId));
        requestInfo.Headers.TryAdd("Accept", JsonMediaType);
        requestInfo.SetStreamContent(parquet, ParquetMediaType);

        return await _adapter.SendAsync<UntypedNode>(
            requestInfo, UntypedNode.CreateFromDiscriminatorValue, ErrorMapping,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes bulk data as Parquet in chunks via a session: opens a session, uploads
    /// each chunk, then commits. The session is abandoned (best effort) if any step
    /// fails. Use this for large datasets (&gt; ~10M values or &gt; 3000 columns).
    /// </summary>
    public async Task<CommitSessionResponse> WriteParquetSessionAsync(
        string recordId,
        IEnumerable<Stream> chunks,
        SessionUpdateMode mode = SessionUpdateMode.Update,
        long fromVersion = 0,
        WellboreBulkEntity entity = WellboreBulkEntity.WellLogs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var session = await OpenSessionAsync(
            recordId, mode, fromVersion, timeToLiveMinutes: null, entity,
            cancellationToken).ConfigureAwait(false);
        var sessionId = session.Id
            ?? throw new OsduException("Session creation response contained no session id.");
        try
        {
            foreach (var chunk in chunks)
                await WriteSessionChunkParquetAsync(
                    recordId, sessionId, chunk, entity, cancellationToken).ConfigureAwait(false);
            return await CommitSessionAsync(
                    recordId, sessionId, entity, cancellationToken).ConfigureAwait(false)
                ?? throw new OsduException("Session commit returned an empty response.");
        }
        catch
        {
            try
            {
                // Abandon even when the failure was a cancellation.
                await AbandonSessionAsync(
                    recordId, sessionId, entity, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best effort; surface the original failure.
            }
            throw;
        }
    }

    /// <summary>Opens a bulk-data session. The session id is <c>Session.Id</c>.</summary>
    public async Task<Session> OpenSessionAsync(
        string recordId,
        SessionUpdateMode mode = SessionUpdateMode.Update,
        long fromVersion = 0,
        long? timeToLiveMinutes = null,
        WellboreBulkEntity entity = WellboreBulkEntity.WellLogs,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var body = new CreateDataSessionRequest
        {
            Mode = mode,
            FromVersion = fromVersion,
            TimeToLive = timeToLiveMinutes,
        };
        var requestInfo = new RequestInformation(
            Method.POST, "{+baseurl}/ddms/v3/{entity}/{record_id}/sessions",
            PathParameters(entity, recordId));
        requestInfo.Headers.TryAdd("Accept", JsonMediaType);
        requestInfo.SetContentFromParsable(_adapter, JsonMediaType, body);

        var session = await _adapter.SendAsync<Session>(
            requestInfo, Session.CreateFromDiscriminatorValue, ErrorMapping,
            cancellationToken).ConfigureAwait(false);
        return session ?? throw new OsduException("Session creation returned an empty response.");
    }

    /// <summary>Uploads one Parquet chunk to an open session.</summary>
    public async Task<UntypedNode?> WriteSessionChunkParquetAsync(
        string recordId,
        Guid sessionId,
        Stream parquet,
        WellboreBulkEntity entity = WellboreBulkEntity.WellLogs,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentNullException.ThrowIfNull(parquet);

        var requestInfo = new RequestInformation(
            Method.POST, "{+baseurl}/ddms/v3/{entity}/{record_id}/sessions/{session_id}/data",
            PathParameters(entity, recordId, sessionId: sessionId));
        requestInfo.Headers.TryAdd("Accept", JsonMediaType);
        requestInfo.SetStreamContent(parquet, ParquetMediaType);

        return await _adapter.SendAsync<UntypedNode>(
            requestInfo, UntypedNode.CreateFromDiscriminatorValue, ErrorMapping,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Commits a session, persisting its chunks as a new record version.</summary>
    public Task<CommitSessionResponse?> CommitSessionAsync(
        string recordId,
        Guid sessionId,
        WellboreBulkEntity entity = WellboreBulkEntity.WellLogs,
        CancellationToken cancellationToken = default) =>
        PatchSessionAsync(recordId, sessionId, UpdateSessionStateValue.Commit, entity, cancellationToken);

    /// <summary>Abandons a session, discarding its uploaded chunks.</summary>
    public Task<CommitSessionResponse?> AbandonSessionAsync(
        string recordId,
        Guid sessionId,
        WellboreBulkEntity entity = WellboreBulkEntity.WellLogs,
        CancellationToken cancellationToken = default) =>
        PatchSessionAsync(recordId, sessionId, UpdateSessionStateValue.Abandon, entity, cancellationToken);

    private async Task<CommitSessionResponse?> PatchSessionAsync(
        string recordId,
        Guid sessionId,
        UpdateSessionStateValue state,
        WellboreBulkEntity entity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var requestInfo = new RequestInformation(
            Method.PATCH, "{+baseurl}/ddms/v3/{entity}/{record_id}/sessions/{session_id}",
            PathParameters(entity, recordId, sessionId: sessionId));
        requestInfo.Headers.TryAdd("Accept", JsonMediaType);
        requestInfo.SetContentFromParsable(_adapter, JsonMediaType,
            new UpdateSessionState { State = state });

        return await _adapter.SendAsync<CommitSessionResponse>(
            requestInfo, CommitSessionResponse.CreateFromDiscriminatorValue, ErrorMapping,
            cancellationToken).ConfigureAwait(false);
    }

    private static string PathSegment(WellboreBulkEntity entity) => entity switch
    {
        WellboreBulkEntity.WellLogs => "welllogs",
        WellboreBulkEntity.WellboreTrajectories => "wellboretrajectories",
        WellboreBulkEntity.PpfgDataset => "ppfgdataset",
        WellboreBulkEntity.WellPressureTestRawMeasurement => "wellpressuretestrawmeasurement",
        _ => throw new ArgumentOutOfRangeException(nameof(entity), entity, "Unsupported bulk entity"),
    };

    private static Dictionary<string, object> PathParameters(
        WellboreBulkEntity entity, string recordId, long? version = null, Guid? sessionId = null)
    {
        var parameters = new Dictionary<string, object>
        {
            { "entity", PathSegment(entity) },
            { "record_id", recordId },
        };
        if (version is not null) parameters["version"] = version.Value;
        if (sessionId is not null) parameters["session_id"] = sessionId.Value.ToString();
        return parameters;
    }
}
