using System.Linq.Expressions;

namespace Osdu.Client.ExampleApp.Caching;

/// <summary>
/// Executes strongly-typed OSDU queries built from lambda expressions.
/// Can be used standalone (without caching) or plugged into <see cref="OsduCache{TItem}"/>.
/// </summary>
public interface IOsduQueryExecutor
{
    /// <summary>
    /// Executes a query using a strongly-typed predicate expression.
    /// </summary>
    Task<OsduQueryResult<TItem>> ExecuteAsync<TItem>(string kind, Expression<Func<TItem, bool>> predicate, OsduQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a raw Lucene query string.
    /// </summary>
    Task<OsduQueryResult<TItem>> ExecuteAsync<TItem>(string kind, string query, OsduQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a query fetching all records (no query filter).
    /// </summary>
    Task<OsduQueryResult<TItem>> ExecuteAllAsync<TItem>(string kind, OsduQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Builds a Lucene query string from a predicate without executing it.
    /// </summary>
    string BuildQuery<TItem>(Expression<Func<TItem, bool>> predicate);
}
