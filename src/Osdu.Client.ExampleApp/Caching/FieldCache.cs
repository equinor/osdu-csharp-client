using Microsoft.Extensions.Caching.Memory;
using Osdu.Client.Schemas.MasterData;

namespace Osdu.Client.ExampleApp.Caching;

public class FieldCache(IMemoryCache cache, OsduCacheOptions options, IOsduClient osduClient)
    : BaseCache<Field_1_0_0>(cache, options.Fields, osduClient)
{
    protected override string KeyPrefix => "osdu:fields";
    protected override string Kind => "osdu:wks:master-data--Field:*";
}
