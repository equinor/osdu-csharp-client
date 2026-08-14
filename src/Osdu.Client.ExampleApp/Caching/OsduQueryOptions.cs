namespace Osdu.Client.ExampleApp.Caching;

/// <summary>
/// Options for controlling query execution behavior.
/// </summary>
public class OsduQueryOptions
{
    /// <summary>
    /// Number of records per page. Defaults to 100.
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of pages to fetch. Set to 0 for unlimited.
    /// </summary>
    public int MaxPages { get; set; } = 10;

    /// <summary>
    /// When true, fetches all available records regardless of MaxPages.
    /// </summary>
    public bool FetchAll { get; set; }
}
