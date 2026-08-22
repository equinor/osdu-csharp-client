using Equinor.OsduCsharpClient.CrsCatalog;
using Equinor.OsduCsharpClient.CrsConversion;
using Equinor.OsduCsharpClient.Dataset;
using Equinor.OsduCsharpClient.Entitlements;
using Equinor.OsduCsharpClient.Facade.Auth;
using Equinor.OsduCsharpClient.FileNamespace;
using Equinor.OsduCsharpClient.Indexer;
using Equinor.OsduCsharpClient.Legal;
using Equinor.OsduCsharpClient.Notification;
using Equinor.OsduCsharpClient.Partition;
using Equinor.OsduCsharpClient.Policy;
using Equinor.OsduCsharpClient.Register;
using Equinor.OsduCsharpClient.SchemaService;
using Equinor.OsduCsharpClient.Search;
using Equinor.OsduCsharpClient.Storage;
using Equinor.OsduCsharpClient.UnitV2;
using Equinor.OsduCsharpClient.UnitV3;
using Equinor.OsduCsharpClient.WellboreDdms;
using Equinor.OsduCsharpClient.Workflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// High-level OSDU client facade. Exposes a typed property for each OSDU service,
/// pre-configured with authentication and automatic <c>data-partition-id</c> header injection.
/// </summary>
/// <example>
/// <code>
/// using var client = new OsduClient(OsduConfig.FromConfiguration(builder.Configuration));
/// var result = await client.Search.Query.PostAsync(request, cancellationToken: ct);
/// </code>
/// </example>
public sealed class OsduClient : IDisposable
{
    private readonly OsduConfig _config;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Guards all lazy initialisation state below. Service clients and their adapters are
    /// built on first access, so concurrent first calls would otherwise race on the
    /// dictionaries and on <see cref="_httpClients"/>. Contention is negligible: each
    /// service is built at most once per client instance.
    /// </summary>
    private readonly Lock _sync = new();

    private readonly List<HttpClient> _httpClients = [];
    private readonly Dictionary<string, HttpClientRequestAdapter> _adapters = [];

    /// <summary>Built service clients, keyed by client type. Guarded by <see cref="_sync"/>.</summary>
    private readonly Dictionary<Type, object> _clients = [];

    private bool _disposed;

    /// <param name="config">OSDU configuration. Use <see cref="OsduConfig.FromConfiguration"/> to bind from <c>IConfiguration</c>.</param>
    /// <param name="tokenProvider">
    /// Token provider. Defaults to <see cref="MsalInteractiveTokenProvider"/> when null.
    /// </param>
    /// <param name="loggerFactory">
    /// Logger factory for HTTP request/response logging. Defaults to <see cref="NullLoggerFactory.Instance"/> (no logging).
    /// Pass your application's <c>ILoggerFactory</c> to enable logging.
    /// Set logger category <c>Equinor.OsduCsharpClient</c> to <c>Debug</c> for request/response logs,
    /// or <c>Equinor.OsduCsharpClient.Body</c> to <c>Debug</c> to also log bodies (truncated, sensitive headers redacted).
    /// </param>
    public OsduClient(OsduConfig config, ITokenProvider? tokenProvider = null, ILoggerFactory? loggerFactory = null)
    {
        _config = config;
        _tokenProvider = tokenProvider ?? new MsalInteractiveTokenProvider(config);
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public CrsCatalogClient    CrsCatalog    => Client<CrsCatalogClient>("crs_catalog");
    public CrsConversionClient CrsConversion => Client<CrsConversionClient>("crs_conversion");
    public DatasetClient       Dataset       => Client<DatasetClient>("dataset");
    public EntitlementsClient  Entitlements  => Client<EntitlementsClient>("entitlements");
    public FileClient          File          => Client<FileClient>("file");
    public IndexerClient       Indexer       => Client<IndexerClient>("indexer");
    public LegalClient         Legal         => Client<LegalClient>("legal");
    public NotificationClient  Notification  => Client<NotificationClient>("notification");
    public PartitionClient     Partition     => Client<PartitionClient>("partition");
    public PolicyClient        Policy        => Client<PolicyClient>("policy");
    public RegisterClient      Register      => Client<RegisterClient>("register");
    public SchemaServiceClient SchemaService => Client<SchemaServiceClient>("schema_service");

    /// <summary>Renamed to <see cref="SchemaService"/>, matching the /api/schema-service route.</summary>
    [Obsolete("Renamed to SchemaService. This alias will be removed in a future major release.")]
    public SchemaServiceClient Schema        => SchemaService;
    public SearchClient        Search        => Client<SearchClient>("search");
    public StorageClient       Storage       => Client<StorageClient>("storage");
    public UnitV2Client        UnitV2        => Client<UnitV2Client>("unit_v2");
    public UnitV3Client        UnitV3        => Client<UnitV3Client>("unit_v3");

    /// <summary>The current default Unit version. Tracks <see cref="UnitV3"/>.</summary>
    public UnitV3Client        Unit          => UnitV3;
    public WellboreDdmsClient  WellboreDdms  => Client<WellboreDdmsClient>("wellbore_ddms");
    public WorkflowClient      Workflow      => Client<WorkflowClient>("workflow");

    /// <summary>
    /// Hand-written Wellbore DDMS bulk-data helpers for <c>application/x-parquet</c>
    /// (read, write, and chunked session writes), which the generated
    /// <see cref="WellboreDdms"/> client cannot express. Shares the same
    /// authenticated transport as <see cref="WellboreDdms"/>.
    /// </summary>
    public WellboreDdmsBulkClient WellboreDdmsBulk => Client<WellboreDdmsBulkClient>("wellbore_ddms");

    /// <summary>
    /// Returns the authenticated Kiota request adapter for the given service attr name
    /// (e.g. <c>"wellbore_ddms"</c> — see <see cref="ServiceRegistry"/>). Escape hatch for
    /// requests the generated clients cannot express, such as alternate content types:
    /// build a <c>RequestInformation</c> and send it directly. Bearer-token auth,
    /// <c>data-partition-id</c> injection, and logging are all applied.
    /// </summary>
    public IRequestAdapter GetRequestAdapter(string serviceAttr) => GetOrCreateAdapter(serviceAttr);

    /// <summary>
    /// Returns the cached service client of type <typeparamref name="T"/>, building it
    /// (and its adapter) on first access. Keyed by client type rather than by
    /// <paramref name="serviceAttr"/> so that two client types may share one adapter —
    /// <see cref="WellboreDdms"/> and <see cref="WellboreDdmsBulk"/> both use
    /// <c>wellbore_ddms</c>.
    /// </summary>
    private T Client<T>(string serviceAttr) where T : class
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_clients.TryGetValue(typeof(T), out var existing)) return (T)existing;

            var client = (T)Activator.CreateInstance(typeof(T), GetOrCreateAdapter(serviceAttr))!;
            _clients[typeof(T)] = client;
            return client;
        }
    }

    private HttpClientRequestAdapter GetOrCreateAdapter(string serviceAttr)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_adapters.TryGetValue(serviceAttr, out var adapter)) return adapter;

            adapter = CreateAdapter(_config.UrlFor(serviceAttr));
            _adapters[serviceAttr] = adapter;
            return adapter;
        }
    }

    /// <summary>
    /// Creates a Kiota <see cref="HttpClientRequestAdapter"/> for the given base URL,
    /// with bearer-token auth and data-partition-id header injection built in.
    /// </summary>
    private HttpClientRequestAdapter CreateAdapter(string baseUrl)
    {
        var httpClient = new HttpClient(
            new LoggingHandler(_loggerFactory)
            {
                InnerHandler = new DataPartitionHandler(_config.DataPartitionId)
                {
                    InnerHandler = new HttpClientHandler()
                }
            })
        {
            Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds),
        };

        _httpClients.Add(httpClient);

        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenProviderAdapter(_tokenProvider));

        return new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
        {
            BaseUrl = baseUrl
        };
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var client in _httpClients)
                client.Dispose();

            _httpClients.Clear();
            _adapters.Clear();
            _clients.Clear();
        }
    }

    /// <summary>Adapts <see cref="ITokenProvider"/> to Kiota's <see cref="IAccessTokenProvider"/>.</summary>
    private sealed class TokenProviderAdapter(ITokenProvider provider) : IAccessTokenProvider
    {
        public async Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default) =>
            await provider.GetTokenAsync(cancellationToken);

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();
    }
}
