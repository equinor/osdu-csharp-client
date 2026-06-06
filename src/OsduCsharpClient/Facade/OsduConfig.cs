using Microsoft.Extensions.Configuration;

namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// CSP-agnostic OSDU client configuration.
/// Bind from <see cref="IConfiguration"/> via <see cref="FromConfiguration"/>
/// (appsettings.json, environment variables, user secrets, …), or construct directly.
/// </summary>
public record OsduConfig
{
    /// <summary>Default configuration section name bound by <see cref="FromConfiguration"/>.</summary>
    public const string DefaultSectionName = "Osdu";


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
    /// Binds an <see cref="OsduConfig"/> from a configuration section (default
    /// <see cref="DefaultSectionName"/>). Works with any standard .NET
    /// configuration source — <c>appsettings.json</c>, environment variables
    /// (e.g. <c>Osdu__Server</c>), user secrets, command line, etc.
    /// </summary>
    /// <example>
    /// <code>
    /// // appsettings.json: { "Osdu": { "Server": "...", "DataPartitionId": "...", ... } }
    /// var config = OsduConfig.FromConfiguration(builder.Configuration);
    /// </code>
    /// </example>
    /// <param name="configuration">The configuration root or provider to bind from.</param>
    /// <param name="sectionName">Section to bind. Defaults to <see cref="DefaultSectionName"/>.</param>
    /// <exception cref="OsduException">If the section is missing or a required value is absent.</exception>
    public static OsduConfig FromConfiguration(
        IConfiguration configuration, string sectionName = DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);
        if (!section.Exists())
            throw new OsduException($"Missing configuration section '{sectionName}'.");

        var config = section.Get<OsduConfig>()
            ?? throw new OsduException($"Could not bind configuration section '{sectionName}'.");

        config.Validate(sectionName);
        return config;
    }

    /// <summary>Throws <see cref="OsduException"/> if any required value is missing.</summary>
    private void Validate(string sectionName)
    {
        foreach (var (name, value) in new[]
                 {
                     (nameof(Server), Server),
                     (nameof(DataPartitionId), DataPartitionId),
                     (nameof(Authority), Authority),
                     (nameof(ClientId), ClientId),
                     (nameof(Scopes), Scopes),
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new OsduException(
                    $"Missing required configuration value '{sectionName}:{name}'.");
        }
    }
}
