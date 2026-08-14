using Microsoft.Extensions.Caching.Memory;
using Osdu.Client.Schemas.ReferenceData;

namespace Osdu.Client.ExampleApp.Caching;

public class UnitCache(IMemoryCache cache, OsduCacheOptions options, IOsduClient osduClient)
    : BaseCache<UnitOfMeasure_1_0_0>(cache, options.Units, osduClient)
{
    protected override string KeyPrefix => "osdu:units";
    protected override string Kind => "osdu:wks:reference-data--UnitOfMeasure:*";
}
