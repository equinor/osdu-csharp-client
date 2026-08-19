using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.Facade.Auth;
using Equinor.OsduCsharpClient.Search;
using Equinor.OsduCsharpClient.WellboreDdms;
using Xunit;

namespace OsduCsharpClient.Tests;

/// <summary>
/// Service clients and their request adapters are built lazily on first property access.
/// A singleton <see cref="OsduClient"/> (the normal DI registration) is hit by many
/// requests at once during cold start, so that first access must be safe to race.
///
/// Before the lock was introduced these tests failed the great majority of runs: racing
/// threads each built their own client over their own <c>HttpClient</c>, leaving orphaned
/// handlers behind, and the unsynchronised <c>List&lt;HttpClient&gt;</c> could be corrupted
/// badly enough that <see cref="OsduClient.Dispose"/> threw a <see cref="NullReferenceException"/>.
/// </summary>
public class OsduClientConcurrencyTests
{
    private const int Threads = 16;
    private const int Trials = 50;

    private static OsduConfig MakeConfig() => new()
    {
        Server = "https://osdu.example.com",
        DataPartitionId = "test-partition",
        Authority = "https://login.microsoftonline.com/tenant",
        ClientId = "client-id",
        Scopes = "https://example.com/.default",
    };

    private static OsduClient NewClient() => new(MakeConfig(), new StaticTokenProvider("tok"));

    /// <summary>
    /// Runs <paramref name="access"/> on <see cref="Threads"/> threads released simultaneously,
    /// and returns what each one observed. Exceptions are captured rather than thrown so a
    /// failing run reports every thread's outcome at once.
    /// </summary>
    private static (object?[] Results, Exception?[] Failures) Race(Func<int, object?> access)
    {
        var results = new object?[Threads];
        var failures = new Exception?[Threads];
        using var gate = new Barrier(Threads);
        var threads = new Thread[Threads];

        for (var i = 0; i < Threads; i++)
        {
            var index = i;
            threads[i] = new Thread(() =>
            {
                gate.SignalAndWait();
                try { results[index] = access(index); }
                catch (Exception ex) { failures[index] = ex; }
            });
            threads[i].Start();
        }

        foreach (var thread in threads) thread.Join();
        return (results, failures);
    }

    [Fact]
    public void ServiceProperty_ReturnsSameInstance_WhenFirstAccessIsRaced()
    {
        for (var trial = 0; trial < Trials; trial++)
        {
            using var client = NewClient();

            var (results, failures) = Race(_ => client.Search);

            Assert.All(failures, Assert.Null);
            Assert.Single(results.Distinct());
        }
    }

    [Fact]
    public void DistinctServiceProperties_InitialiseSafely_WhenRaced()
    {
        for (var trial = 0; trial < Trials; trial++)
        {
            var client = NewClient();

            // Spread the threads across different services so the adapter dictionary and the
            // HttpClient list take concurrent writes for several distinct keys at once.
            var (results, failures) = Race(index => (index % 4) switch
            {
                0 => client.Search,
                1 => client.Storage,
                2 => client.WellboreDdms,
                _ => client.Entitlements,
            });

            Assert.All(failures, Assert.Null);

            // Every thread that asked for a given service must have been handed the same
            // instance, even though four services were being built at the same time.
            foreach (var perService in results.GroupBy(client => client!.GetType()))
                Assert.Single(perService.Distinct());

            // Disposal walks the HttpClient list, which is what a torn write corrupts.
            var dispose = Record.Exception(client.Dispose);
            Assert.Null(dispose);
        }
    }

    [Fact]
    public void GetRequestAdapter_ReturnsSameAdapter_WhenFirstAccessIsRaced()
    {
        for (var trial = 0; trial < Trials; trial++)
        {
            using var client = NewClient();

            var (results, failures) = Race(_ => client.GetRequestAdapter("search"));

            Assert.All(failures, Assert.Null);
            Assert.Single(results.Distinct());
        }
    }

    [Fact]
    public void WellboreDdmsAndBulk_AreDistinctClients_OverOneSharedAdapter()
    {
        using var client = NewClient();

        // Both are served from the "wellbore_ddms" adapter, so the client cache has to be
        // keyed by client type rather than by service name.
        Assert.IsType<WellboreDdmsClient>(client.WellboreDdms);
        Assert.IsType<WellboreDdmsBulkClient>(client.WellboreDdmsBulk);
        Assert.Same(client.WellboreDdms, client.WellboreDdms);
        Assert.Same(client.WellboreDdmsBulk, client.WellboreDdmsBulk);
    }

    [Fact]
    public void ServiceProperty_ReturnsSameInstance_AcrossSequentialAccesses()
    {
        using var client = NewClient();

        Assert.Same(client.Search, client.Search);
        Assert.IsType<SearchClient>(client.Search);
    }

    [Fact]
    public void ServiceProperty_Throws_AfterDispose()
    {
        var client = NewClient();
        _ = client.Search;
        client.Dispose();

        Assert.Throws<ObjectDisposedException>(() => client.Search);
        Assert.Throws<ObjectDisposedException>(() => client.GetRequestAdapter("search"));
    }
}
