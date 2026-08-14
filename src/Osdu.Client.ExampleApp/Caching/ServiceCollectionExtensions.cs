using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Osdu.Client.Schemas.MasterData;
using Osdu.Client.Schemas.ReferenceData;

namespace Osdu.Client.ExampleApp.Caching;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OSDU caching infrastructure.
    /// Use <paramref name="configureDescriptors"/> to add cache descriptors at startup.
    /// Additional descriptors can be added at runtime via <see cref="IOsduCacheProvider.Register"/>.
    /// </summary>
    public static IServiceCollection AddOsduCaching(this IServiceCollection services, IConfiguration configuration, Action<List<OsduCacheDescriptor>>? configureDescriptors = null)
    {
        services.AddMemoryCache();

        var descriptors = new List<OsduCacheDescriptor>();
        configureDescriptors?.Invoke(descriptors);

        foreach (var descriptor in descriptors)
            services.AddSingleton(descriptor);

        services.AddSingleton<IOsduCacheProvider, OsduCacheProvider>();

        return services;
    }
}
