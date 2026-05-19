namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// CSP-agnostic OSDU client configuration.
/// Load from environment variables via <see cref="FromEnvironment"/>, or construct directly.
/// </summary>
public record OsduConfig
{
    public required string Server { get; init; }
    public required string DataPartitionId { get; init; }
    public required string Authority { get; init; }
    public required string ClientId { get; init; }

    /// <summary>Space-separated OAuth scopes, e.g. <c>https://example.com/.default</c>.</summary>
    public required string Scopes { get; init; }

    public double TimeoutSeconds { get; init; } = 30.0;
    public int RetryAttempts { get; init; } = 3;

    /// <summary>
    /// Override the base URL for specific services. Keys match <see cref="ServiceSpec.Attr"/>
    /// (e.g. <c>"search"</c>). Values replace the full URL (server + endpoint).
    /// </summary>
    public IReadOnlyDictionary<string, string> EndpointOverrides { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Returns the base URL for the given service attr name.</summary>
    public string UrlFor(string service)
    {
        if (EndpointOverrides.TryGetValue(service, out var overrideUrl))
            return overrideUrl;

        if (!ServiceRegistry.ByAttr.TryGetValue(service, out var spec))
            throw new OsduException($"Unknown service '{service}'. Check ServiceRegistry.");

        return $"{Server.TrimEnd('/')}{spec.DefaultEndpoint}";
    }

    /// <summary>Parses <see cref="Scopes"/> into an array for MSAL.</summary>
    public string[] ScopesArray =>
        Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Creates an <see cref="OsduConfig"/> from the standard environment variables:
    /// <c>SERVER</c>, <c>DATA_PARTITION_ID</c>, <c>AUTHORITY</c>, <c>CLIENT_ID</c>, <c>SCOPES</c>.
    /// </summary>
    public static OsduConfig FromEnvironment() => new()
    {
        Server          = Required("SERVER"),
        DataPartitionId = Required("DATA_PARTITION_ID"),
        Authority       = Required("AUTHORITY"),
        ClientId        = Required("CLIENT_ID"),
        Scopes          = Required("SCOPES"),
        TimeoutSeconds  = double.TryParse(Env("OSDU_TIMEOUT_SECONDS"), out var t) ? t : 30.0,
        RetryAttempts   = int.TryParse(Env("OSDU_RETRY_ATTEMPTS"), out var r) ? r : 3,
    };

    private static string Required(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new OsduException($"Missing required environment variable: {key}");

    private static string? Env(string key) => Environment.GetEnvironmentVariable(key);
}
