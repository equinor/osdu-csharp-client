//using Microsoft.Extensions.Caching.Memory;
//using Osdu.Client.Schemas.MasterData;

//namespace Osdu.Client.ExampleApp.Caching;

///// <summary>
///// Extended cache for Wellbore data with domain-specific query methods.
///// Demonstrates how to derive from <see cref="OsduCache{TItem}"/> when extra functionality is needed.
///// </summary>
//public class WellboreCache(IMemoryCache cache, OsduCacheOptions options, IOsduClient osduClient)
//    : OsduCache<Wellbore_1_3_0>(cache, options.Wellbores, osduClient, "osdu:wellbores", "osdu:wks:master-data--Wellbore:*")
//{
//    /// <summary>
//    /// Gets wellbores filtered by a specific well ID.
//    /// </summary>
//    public Task<CachedResult<Wellbore_1_3_0>> GetByWellIdAsync(string wellId, CancellationToken ct = default) =>
//        GetByQueryAsync($"data.WellID:\"{wellId}\"", ct);

//    /// <summary>
//    /// Gets wellbores filtered by facility name.
//    /// </summary>
//    public Task<CachedResult<Wellbore_1_3_0>> GetByFacilityNameAsync(string facilityName, CancellationToken ct = default) =>
//        GetByQueryAsync($"data.FacilityName:\"{facilityName}\"", ct);
//}
