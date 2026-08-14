using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Osdu.Client.ExampleApp.Caching;

/// <summary>
/// A registry that creates and resolves <see cref="OsduCache{TItem}"/> instances
/// by the generic type parameter. No string keys needed — the type itself is the key.
/// </summary>
public class OsduCacheRegistry
{
    private readonly IMemoryCache _memoryCache;
    private readonly IOsduClient _osduClient;
    private readonly Dictionary<Type, OsduCacheDescriptor> _descriptors;
    private readonly ConcurrentDictionary<Type, object> _caches = [];

    public OsduCacheRegistry(IMemoryCache memoryCache, IOsduClient osduClient, IEnumerable<OsduCacheDescriptor> descriptors)
    {
        _memoryCache = memoryCache;
        _osduClient = osduClient;
        _descriptors = descriptors.ToDictionary(d => d.ItemType);
    }

    /// <summary>
    /// Resolves a cache by its item type. Creates the instance on first access.
    /// </summary>
    /// <typeparam name="TItem">The strongly-typed model registered via <see cref="OsduCacheDescriptor"/>.</typeparam>
    public OsduCache<TItem> GetCache<TItem>()
    {
        return (OsduCache<TItem>)_caches.GetOrAdd(typeof(TItem), type =>
        {
            if (!_descriptors.TryGetValue(type, out var descriptor))
                throw new InvalidOperationException($"No cache descriptor registered for type '{type.Name}'. Register it via AddOsduCaching().");

            return new OsduCache<TItem>(_memoryCache, descriptor.Options, _osduClient, descriptor.KeyPrefix, descriptor.Kind);
        });
    }

    /// <summary>
    /// Gets all cached items for the given type in a single call.
    /// </summary>
    public async Task<List<TItem>> GetAllAsync<TItem>(CancellationToken ct = default)
    {
        var result = await GetCache<TItem>().GetAllAsync(ct);
        return result.Items;
    }

    /// <summary>
    /// Gets cached items matching a query for the given type in a single call.
    /// </summary>
    public async Task<List<TItem>> GetByQueryAsync<TItem>(string query, CancellationToken ct = default)
    {
        var result = await GetCache<TItem>().GetByQueryAsync(query, ct);
        return result.Items;
    }

    /// <summary>
    /// Returns all registered item types.
    /// </summary>
    public IReadOnlyCollection<Type> RegisteredTypes => _descriptors.Keys.ToList().AsReadOnly();
}
