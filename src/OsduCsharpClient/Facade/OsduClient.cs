using Equinor.OsduCsharpClient.CrsCatalog;
using Equinor.OsduCsharpClient.CrsConversion;
using Equinor.OsduCsharpClient.Dataset;
using Equinor.OsduCsharpClient.Entitlements;
using Equinor.OsduCsharpClient.Facade.Auth;
using Equinor.OsduCsharpClient.FileNamespace;
using Equinor.OsduCsharpClient.Geospatial;
using Equinor.OsduCsharpClient.Indexer;
using Equinor.OsduCsharpClient.Legal;
using Equinor.OsduCsharpClient.Notification;
using Equinor.OsduCsharpClient.Partition;
using Equinor.OsduCsharpClient.Policy;
using Equinor.OsduCsharpClient.Register;
using Equinor.OsduCsharpClient.Schema;
using Equinor.OsduCsharpClient.Search;
using Equinor.OsduCsharpClient.SeismicDdms;
using Equinor.OsduCsharpClient.Storage;
using Equinor.OsduCsharpClient.Unit;
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
    private readonly List<HttpClient> _httpClients = [];
    private readonly Dictionary<string, HttpClientRequestAdapter> _adapters = [];
    private bool _disposed;

    private CrsCatalogClient?    _crsCatalog;
    private CrsConversionClient? _crsConversion;
    private DatasetClient?       _dataset;
    private EntitlementsClient?  _entitlements;
    private FileClient?          _file;
    private GeospatialClient?    _geospatial;
    private IndexerClient?       _indexer;
    private LegalClient?         _legal;
    private NotificationClient?  _notification;
    private PartitionClient?     _partition;
    private PolicyClient?        _policy;
    private RegisterClient?      _register;
    private SchemaClient?        _schema;
    private SearchClient?        _search;
    private SeismicDdmsClient?   _seismicDdms;
    private StorageClient?       _storage;
    private UnitClient?          _unit;
    private WellboreDdmsClient?  _wellboreDdms;
    private WorkflowClient?      _workflow;
    private WellboreDdmsBulkClient? _wellboreDdmsBulk;

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

    public CrsCatalogClient    CrsCatalog    => _crsCatalog    ??= Build(ref _crsCatalog,    "crs_catalog");
    public CrsConversionClient CrsConversion => _crsConversion ??= Build(ref _crsConversion, "crs_conversion");
    public DatasetClient       Dataset       => _dataset       ??= Build(ref _dataset,       "dataset");
    public EntitlementsClient  Entitlements  => _entitlements  ??= Build(ref _entitlements,  "entitlements");
    public FileClient          File          => _file          ??= Build(ref _file,          "file");
    public GeospatialClient    Geospatial    => _geospatial    ??= Build(ref _geospatial,    "geospatial");
    public IndexerClient       Indexer       => _indexer       ??= Build(ref _indexer,       "indexer");
    public LegalClient         Legal         => _legal         ??= Build(ref _legal,         "legal");
    public NotificationClient  Notification  => _notification  ??= Build(ref _notification,  "notification");
    public PartitionClient     Partition     => _partition     ??= Build(ref _partition,     "partition");
    public PolicyClient        Policy        => _policy        ??= Build(ref _policy,        "policy");
    public RegisterClient      Register      => _register      ??= Build(ref _register,      "register");
    public SchemaClient        Schema        => _schema        ??= Build(ref _schema,        "schema");
    public SearchClient        Search        => _search        ??= Build(ref _search,        "search");
    public SeismicDdmsClient   SeismicDdms   => _seismicDdms   ??= Build(ref _seismicDdms,   "seismic_ddms");
    public StorageClient       Storage       => _storage       ??= Build(ref _storage,       "storage");
    public UnitClient          Unit          => _unit          ??= Build(ref _unit,          "unit");
    public WellboreDdmsClient  WellboreDdms  => _wellboreDdms  ??= Build(ref _wellboreDdms,  "wellbore_ddms");
    public WorkflowClient      Workflow      => _workflow      ??= Build(ref _workflow,      "workflow");

    /// <summary>
    /// Hand-written Wellbore DDMS bulk-data helpers for <c>application/x-parquet</c>
    /// (read, write, and chunked session writes), which the generated
    /// <see cref="WellboreDdms"/> client cannot express. Shares the same
    /// authenticated transport as <see cref="WellboreDdms"/>.
    /// </summary>
    public WellboreDdmsBulkClient WellboreDdmsBulk =>
        _wellboreDdmsBulk ??= new WellboreDdmsBulkClient(GetOrCreateAdapter("wellbore_ddms"));

    /// <summary>
    /// Returns the authenticated Kiota request adapter for the given service attr name
    /// (e.g. <c>"wellbore_ddms"</c> — see <see cref="ServiceRegistry"/>). Escape hatch for
    /// requests the generated clients cannot express, such as alternate content types:
    /// build a <c>RequestInformation</c> and send it directly. Bearer-token auth,
    /// <c>data-partition-id</c> injection, and logging are all applied.
    /// </summary>
    public IRequestAdapter GetRequestAdapter(string serviceAttr) => GetOrCreateAdapter(serviceAttr);

    private T Build<T>(ref T? field, string serviceAttr) where T : class
    {
        if (field is not null) return field;
        var adapter = GetOrCreateAdapter(serviceAttr);
        field = (T)Activator.CreateInstance(typeof(T), adapter)!;
        return field;
    }

    private HttpClientRequestAdapter GetOrCreateAdapter(string serviceAttr)
    {
        if (_adapters.TryGetValue(serviceAttr, out var adapter)) return adapter;
        adapter = CreateAdapter(_config.UrlFor(serviceAttr));
        _adapters[serviceAttr] = adapter;
        return adapter;
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
        if (_disposed) return;
        _disposed = true;
        foreach (var client in _httpClients)
            client.Dispose();
        _httpClients.Clear();
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
