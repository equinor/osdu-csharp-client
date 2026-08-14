using Microsoft.Extensions.Caching.Memory;
using Osdu.Client.Apis.Schema;

namespace Osdu.Client.ExampleApp.Caching;

/// <summary>
/// Cache for OSDU schema kinds. Uses the Schema API directly instead of Search.
/// </summary>
public class SchemaCache : BaseCache<SchemaKindInfo>
{
    private readonly IOsduClient _osduClient;

    protected override string KeyPrefix => "osdu:schemas";
    protected override string Kind => string.Empty; // Not used — overrides fetch logic

    public SchemaCache(IMemoryCache cache, OsduCacheOptions options, IOsduClient osduClient)
        : base(cache, options.Schemas, osduClient)
    {
        _osduClient = osduClient;
    }

    /// <summary>
    /// Gets all schema kind identifiers, cached.
    /// </summary>
    public Task<List<SchemaKindInfo>> GetAllSchemasAsync(CancellationToken ct = default) =>
        GetOrCreateAsync("all-schemas", async token =>
        {
            var results = new List<SchemaKindInfo>();
            var offset = 0;
            var pagesFetched = 0;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (!Options.CacheAll && Options.MaxPages > 0 && pagesFetched >= Options.MaxPages)
                    break;

                var response = await _osduClient.Schema.GetSchemaAsync(
                    latestVersion: false,
                    limit: Options.PageSize,
                    offset: offset);

                if (response?.SchemaInfos is null || response.SchemaInfos.Count == 0)
                    break;

                foreach (var schema in response.SchemaInfos)
                {
                    var id = schema.SchemaIdentity;
                    var kindId = id.Id ?? $"{id.Authority}:{id.Source}:{id.EntityType}:{id.SchemaVersionMajor}.{id.SchemaVersionMinor}.{id.SchemaVersionPatch}";
                    var category = id.EntityType.Contains("--")
                        ? id.EntityType[..id.EntityType.IndexOf("--")]
                        : "other";
                    var version = $"{id.SchemaVersionMajor}.{id.SchemaVersionMinor}.{id.SchemaVersionPatch}";

                    results.Add(new SchemaKindInfo(kindId, id.EntityType, category, version));
                }

                offset += response.SchemaInfos.Count;
                pagesFetched++;

                if (response.SchemaInfos.Count < Options.PageSize)
                    break;
            }

            return results;
        }, ct);
}

public record SchemaKindInfo(string KindId, string EntityType, string Category, string Version);
