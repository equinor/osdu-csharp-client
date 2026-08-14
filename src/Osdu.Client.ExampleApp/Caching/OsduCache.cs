using Microsoft.Extensions.Caching.Memory;
using Osdu.Client.Apis.Search;
using Osdu.Client.Extensions;

namespace Osdu.Client.ExampleApp.Caching;

/// <summary>
/// A generic, reusable OSDU reference data cache. Provides paginated fetching
/// with configurable page size, max pages, cache-all, and expiration.
/// Results are deserialized into strongly-typed <typeparamref name="TItem"/> instances.
/// Can be used directly or extended with domain-specific query methods.
/// </summary>
public class OsduCache<TItem>
{
    private readonly IMemoryCache _cache;
    private readonly IOsduClient _osduClient;
    private readonly string _keyPrefix;
    private readonly string _kind;

    protected CacheOptions Options { get; }

    public OsduCache(IMemoryCache cache, CacheOptions options, IOsduClient osduClient, string keyPrefix, string kind)
    {
        _cache = cache;
        Options = options;
        _osduClient = osduClient;
        _keyPrefix = keyPrefix;
        _kind = kind;
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
    public void Invalidate(string key) => _cache.Remove($"{_keyPrefix}:{key}");

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
            _cache.Remove($"{_keyPrefix}:{key}");
    }

    /// <summary>
    /// Gets a cached value or creates it by fetching paginated results from OSDU.
    /// </summary>
    protected async Task<CachedResult<TItem>> GetOrCreateAsync(string cacheKey, string query, CancellationToken ct)
    {
        var fullKey = $"{_keyPrefix}:{cacheKey}";

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
        var fullKey = $"{_keyPrefix}:{cacheKey}";

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
                Kind = _kind,
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
