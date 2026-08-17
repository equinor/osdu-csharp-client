using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Osdu.Client.ExampleApp.Query;
using Osdu.Client.Schemas.MasterData;
using Osdu.Client.Schemas.ReferenceData;

namespace Osdu.Client.ExampleApp.Caching;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OSDU caching and query execution infrastructure.
    /// Use <paramref name="configureDescriptors"/> to add cache descriptors at startup.
    /// Additional descriptors can be added at runtime via <see cref="IOsduCacheProvider.Register"/>.
    /// </summary>
    public static IServiceCollection AddOsduCaching(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<List<OsduCacheDescriptor>>? configureDescriptors = null)
    {
        services.AddMemoryCache();

        var descriptors = new List<OsduCacheDescriptor>();
        configureDescriptors?.Invoke(descriptors);

        foreach (var descriptor in descriptors)
            services.AddSingleton(descriptor);

        // Query executor — standalone, usable without caching
        services.AddSingleton<IOsduQueryExecutor, OsduQueryExecutor>();

        // Cache provider — uses query executor internally
        services.AddSingleton<IOsduCacheProvider, OsduCacheProvider>();

        return services;
    }

    /// <summary>
    /// Registers only the OSDU query executor (without caching).
    /// Use when you need strongly-typed queries but don't want caching overhead.
    /// </summary>
    public static IServiceCollection AddOsduQueryExecutor(this IServiceCollection services)
    {
        services.AddSingleton<IOsduQueryExecutor, OsduQueryExecutor>();
        return services;
    }
}
