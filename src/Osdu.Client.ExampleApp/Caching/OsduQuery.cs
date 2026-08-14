using System.Linq.Expressions;

namespace Osdu.Client.ExampleApp.Caching;

/// <summary>
/// Fluent builder for constructing and executing strongly-typed OSDU queries.
/// The generic type parameter is specified once at the start via
/// <see cref="IOsduQueryExecutor.Query{TItem}"/>.
/// </summary>
public class OsduQuery<TItem>
{
    private readonly IOsduQueryExecutor _executor;
    private readonly string _kind;
    private string _query = "*";
    private readonly OsduQueryOptions _options = new();
    private readonly List<string> _returnedFields = [];
    private readonly List<string> _excludedFields = [];

    internal OsduQuery(IOsduQueryExecutor executor, string kind)
    {
        _executor = executor;
        _kind = kind;
    }

    /// <summary>
    /// Adds a strongly-typed predicate filter.
    /// </summary>
    public OsduQuery<TItem> Where(Expression<Func<TItem, bool>> predicate)
    {
        var built = _executor.BuildQuery(predicate);
        _query = _query == "*" ? built : $"({_query} AND {built})";
        return this;
    }

    /// <summary>
    /// Adds a raw Lucene query filter.
    /// </summary>
    public OsduQuery<TItem> Where(string rawQuery)
    {
        _query = _query == "*" ? rawQuery : $"({_query} AND {rawQuery})";
        return this;
    }

    /// <summary>
    /// Specifies which fields to return using strongly-typed expressions.
    /// </summary>
    public OsduQuery<TItem> Select(params Expression<Func<TItem, object?>>[] selectors)
    {
        _returnedFields.AddRange(OsduFieldSelector.ResolveMany(selectors));
        return this;
    }

    /// <summary>
    /// Specifies which fields to return using raw field names.
    /// </summary>
    public OsduQuery<TItem> Select(params string[] fields)
    {
        _returnedFields.AddRange(fields);
        return this;
    }

    /// <summary>
    /// Specifies which fields to exclude from results using strongly-typed expressions.
    /// </summary>
    public OsduQuery<TItem> Exclude(params Expression<Func<TItem, object?>>[] selectors)
    {
        _excludedFields.AddRange(OsduFieldSelector.ResolveMany(selectors));
        return this;
    }

    /// <summary>
    /// Specifies which fields to exclude from results using raw field names.
    /// </summary>
    public OsduQuery<TItem> Exclude(params string[] fields)
    {
        _excludedFields.AddRange(fields);
        return this;
    }

    /// <summary>
    /// Sets the page size for paginated fetching.
    /// </summary>
    public OsduQuery<TItem> PageSize(int pageSize)
    {
        _options.PageSize = pageSize;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of pages to fetch.
    /// </summary>
    public OsduQuery<TItem> MaxPages(int maxPages)
    {
        _options.MaxPages = maxPages;
        return this;
    }

    /// <summary>
    /// Fetches all available records regardless of page limits.
    /// </summary>
    public OsduQuery<TItem> FetchAll()
    {
        _options.FetchAll = true;
        return this;
    }

    /// <summary>
    /// Executes the query and returns results.
    /// </summary>
    public Task<OsduQueryResult<TItem>> ExecuteAsync(CancellationToken ct = default)
    {
        _options.ReturnedFields = _returnedFields;
        _options.ExcludedFields = _excludedFields;
        return _executor.ExecuteAsync<TItem>(_kind, _query, _options, ct);
    }

    /// <summary>
    /// Executes the query and returns only the items list.
    /// </summary>
    public async Task<List<TItem>> ToListAsync(CancellationToken ct = default)
    {
        var result = await ExecuteAsync(ct);
        return result.Items;
    }

    /// <summary>
    /// Executes the query and returns the first item, or default if none.
    /// </summary>
    public async Task<TItem?> FirstOrDefaultAsync(CancellationToken ct = default)
    {
        _options.PageSize = 1;
        _options.MaxPages = 1;
        var result = await ExecuteAsync(ct);
        return result.Items.Count > 0 ? result.Items[0] : default;
    }
}
