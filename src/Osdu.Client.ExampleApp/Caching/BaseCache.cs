using Microsoft.Extensions.Caching.Memory;
using Osdu.Client.Apis.Search;
using Osdu.Client.Extensions;

namespace Osdu.Client.ExampleApp.Caching;

/// <summary>
/// Base class for OSDU reference data caches. Provides paginated fetching
/// with configurable page size, max pages, cache-all, and expiration.
/// Results are deserialized into strongly-typed <typeparamref name="TItem"/> instances.
/// </summary>
public abstract class BaseCache<TItem>
{
    private readonly IMemoryCache _cache;
    private readonly IOsduClient _osduClient;

    protected CacheOptions Options { get; }

    /// <summary>
    /// A prefix used to namespace cache keys and avoid collisions.
    /// </summary>
    protected abstract string KeyPrefix { get; }

    /// <summary>
    /// The OSDU kind pattern for this cache (e.g. "osdu:wks:master-data--Well:*").
    /// </summary>
    protected abstract string Kind { get; }

    protected BaseCache(IMemoryCache cache, CacheOptions options, IOsduClient osduClient)
    {
        _cache = cache;
        Options = options;
        _osduClient = osduClient;
    }

    /// <summary>
    /// Gets all cached records for this kind, fetching from OSDU if not cached.
    /// Respects PageSize, MaxPages, and CacheAll settings.
    /// </summary>
    public Task<CachedResult<TItem>> GetAllAsync(CancellationToken ct = default) =>
        GetOrCreateAsync("all", "*", ct);

    /// <summary>
    /// Gets cached records for a specific query.
    /// </summary>
    public Task<CachedResult<TItem>> GetByQueryAsync(string query, CancellationToken ct = default) =>
        GetOrCreateAsync($"query:{query}", query, ct);

    /// <summary>
    /// Invalidates a specific cached entry by key.
    /// </summary>
    public void Invalidate(string key) => _cache.Remove($"{KeyPrefix}:{key}");

    /// <summary>
    /// Invalidates the default "all" entry.
    /// </summary>
    public void InvalidateAll() => Invalidate("all");

    /// <summary>
    /// Invalidates all entries matching the given keys.
    /// </summary>
    public void Invalidate(params string[] keys)
    {
        foreach (var key in keys)
            _cache.Remove($"{KeyPrefix}:{key}");
    }

    /// <summary>
    /// Gets a cached value or creates it by fetching paginated results from OSDU.
    /// </summary>
    protected async Task<CachedResult<TItem>> GetOrCreateAsync(string cacheKey, string query, CancellationToken ct)
    {
        var fullKey = $"{KeyPrefix}:{cacheKey}";

        if (_cache.TryGetValue(fullKey, out CachedResult<TItem>? cached) && cached is not null)
            return cached;

        var result = await FetchPaginatedAsync(query, ct);

        _cache.Set(fullKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Options.Expiration
        });

        return result;
    }

    /// <summary>
    /// Gets or creates a typed cached value using a custom factory.
    /// </summary>
    protected async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        var fullKey = $"{KeyPrefix}:{cacheKey}";

        if (_cache.TryGetValue(fullKey, out T? cached) && cached is not null)
            return cached;

        var value = await factory(ct);

        _cache.Set(fullKey, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Options.Expiration
        });

        return value;
    }

    private async Task<CachedResult<TItem>> FetchPaginatedAsync(string query, CancellationToken ct)
    {
        var allItems = new List<TItem>();
        string? cursor = null;
        var pagesFetched = 0;
        long totalCount = 0;
        var isComplete = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (!Options.CacheAll && Options.MaxPages > 0 && pagesFetched >= Options.MaxPages)
                break;

            var response = await _osduClient.Search.PostQueryWithCursorAsync(new CursorQueryRequest
            {
                Kind = Kind,
                Query = query,
                Limit = Options.PageSize,
                Cursor = cursor,
                TrackTotalCount = pagesFetched == 0
            }, cancellationToken: ct);

            if (pagesFetched == 0 && response?.TotalCount is > 0)
                totalCount = response.TotalCount.Value;

            if (response?.Results is null || response.Results.Count == 0)
            {
                isComplete = true;
                break;
            }

            allItems.AddRange(response.Results.Deserialize<TItem>());

            pagesFetched++;
            cursor = response.Cursor;

            if (string.IsNullOrEmpty(cursor))
            {
                isComplete = true;
                break;
            }
        }

        return new CachedResult<TItem>
        {
            Items = allItems,
            TotalCount = totalCount > 0 ? totalCount : allItems.Count,
            IsComplete = isComplete,
            CachedAt = DateTimeOffset.UtcNow
        };
    }
}
