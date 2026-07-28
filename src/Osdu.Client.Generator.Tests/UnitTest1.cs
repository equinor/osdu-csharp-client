using Osdu.Client.Apis.CrsConversion;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.Generator.Tests;

public class UnitTest1
{
    [Fact]
    public async Task Test1()
    {
        ISearchApiClient searchClient = new SearchApiClient(null);
        //await searchClient.QueryRecordsAsync("dataPartitionId", new QueryRequest());

        ICrsConversionApiClient crsConversionClient = new CrsConversionApiClient(null);
        
        //Class1 class1 = new Class1();
    }
}
