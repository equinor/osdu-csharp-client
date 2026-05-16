namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// Describes how a generated service client is exposed on <see cref="OsduClient"/>.
/// </summary>
/// <param name="Attr">Property name on <see cref="OsduClient"/> and key for endpoint overrides.</param>
/// <param name="DefaultEndpoint">Base path appended to <see cref="OsduConfig.Server"/>.</param>
public record ServiceSpec(string Attr, string DefaultEndpoint);

/// <summary>
/// Static registry of all OSDU services with their default API endpoints.
/// </summary>
public static class ServiceRegistry
{
    public static readonly IReadOnlyList<ServiceSpec> Services =
    [
        new("seismic_ddms",              "/seistore-svc/api/v3"),
        new("search",                    "/api/search/v2"),
        new("storage",                   "/api/storage/v2"),
        new("schema",                    "/api/schema-service/v1"),
        new("entitlements",              "/api/entitlements/v2"),
        new("legal",                     "/api/legal/v1"),
        new("file",                      "/api/file/v2"),
        new("geospatial",                "/api/geospatial/v2"),
        new("dataset",                   "/api/dataset/v1"),
        new("indexer",                   "/api/indexer/v2"),
        new("notification",              "/api/notification/v1"),
        new("partition",                 "/api/partition/v1"),
        new("policy",                    "/api/policy/v1"),
        new("register",                  "/api/register/v1"),
        new("unit",                      "/api/unit/v3"),
        new("crs_catalog",               "/api/crs/catalog/v2"),
        new("crs_conversion",            "/api/crs/converter/v2"),
        new("wellbore_ddms",             "/api/os-wellbore-ddms"),
        new("workflow",                  "/api/workflow/v1"),
    ];

    public static readonly IReadOnlyDictionary<string, ServiceSpec> ByAttr =
        Services.ToDictionary(s => s.Attr);
}
