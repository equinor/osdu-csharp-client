namespace Osdu.Client.ExampleApp.Caching;

/// <summary>
/// Top-level configuration for all OSDU reference data caches.
/// Bind to the "OsduCache" section in appsettings.json.
/// </summary>
public class OsduCacheOptions
{
    public const string SectionName = "OsduCache";

    public CacheOptions Units { get; set; } = new() { Expiration = TimeSpan.FromHours(4), CacheAll = true };
    public CacheOptions Wells { get; set; } = new() { Expiration = TimeSpan.FromMinutes(30), PageSize = 200, MaxPages = 5 };
    public CacheOptions Wellbores { get; set; } = new() { Expiration = TimeSpan.FromMinutes(15), PageSize = 200, MaxPages = 10 };
    public CacheOptions Fields { get; set; } = new() { Expiration = TimeSpan.FromHours(2), CacheAll = true };
    public CacheOptions Schemas { get; set; } = new() { Expiration = TimeSpan.FromHours(6), CacheAll = true };
}
