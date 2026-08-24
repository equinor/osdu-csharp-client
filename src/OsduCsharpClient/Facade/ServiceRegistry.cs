namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// Describes how a generated service client is exposed on <see cref="OsduClient"/>.
/// </summary>
/// <param name="Attr">Property name on <see cref="OsduClient"/> and key for endpoint overrides.</param>
/// <param name="DefaultEndpoint">
/// Base path appended to <see cref="OsduConfig.Server"/>.
/// <para>
/// This must match the <c>servers</c> entry in the service's own OpenAPI spec. The generated
/// clients append each spec path verbatim, so where a spec declares <c>servers: /api/unit</c>
/// and paths like <c>/v3/unit</c>, the version belongs to the path and not here -- an endpoint
/// of <c>/api/unit/v3</c> produces <c>/api/unit/v3/v3/unit</c> and makes every operation on
/// that service unreachable.
/// </para>
/// </param>
/// <param name="Aliases">
/// Extra names resolving to this same spec, so that renaming or splitting a service does not
/// break <see cref="OsduConfig.EndpointOverrides"/> keys written against the old name.
/// </param>
public record ServiceSpec(string Attr, string DefaultEndpoint, IReadOnlyList<string>? Aliases = null);

/// <summary>
/// Static registry of all OSDU services with their default API endpoints.
/// </summary>
public static class ServiceRegistry
{
    public static readonly IReadOnlyList<ServiceSpec> Services =
    [
        new("search",                    "/api/search/v2"),
        new("storage",                   "/api/storage/v2"),
        new("schema_service",            "/api/schema-service/v1", ["schema"]),
        new("entitlements",              "/api/entitlements/v2"),
        new("legal",                     "/api/legal/v1"),
        new("file",                      "/api/file"),
        new("dataset",                   "/api/dataset/v1"),
        new("indexer",                   "/api/indexer/v2"),
        new("notification",              "/api/notification/v1"),
        new("partition",                 "/api/partition/v1"),
        new("policy",                    "/api/policy/v1"),
        new("register",                  "/api/register/v1"),
        new("unit_v2",                   "/api/unit"),
        new("unit_v3",                   "/api/unit", ["unit"]),
        new("crs_catalog",               "/api/crs/catalog"),
        new("crs_conversion",            "/api/crs/converter"),
        new("wellbore_ddms",             "/api/os-wellbore-ddms"),
        new("workflow",                  "/api/workflow"),
    ];

    /// <summary>Every service by its attr name, and by any alias it claims.</summary>
    public static readonly IReadOnlyDictionary<string, ServiceSpec> ByAttr =
        Services
            .SelectMany(s => new[] { s.Attr }.Concat(s.Aliases ?? []).Select(name => (name, s)))
            .ToDictionary(x => x.name, x => x.s);
}
