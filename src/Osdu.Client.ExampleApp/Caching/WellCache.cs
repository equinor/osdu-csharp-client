using Microsoft.Extensions.Caching.Memory;
using Osdu.Client.Schemas.MasterData;

namespace Osdu.Client.ExampleApp.Caching;

public class WellCache(IMemoryCache cache, OsduCacheOptions options, IOsduClient osduClient)
    : BaseCache<Well_1_0_0>(cache, options.Wells, osduClient)
{
    protected override string KeyPrefix => "osdu:wells";
    protected override string Kind => "osdu:wks:master-data--Well:*";
}
