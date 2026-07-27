using Osdu.Client.Apis.Search;
using Osdu.Client.Data.Schemas;
using Osdu.Client.Apis.CrsConversion;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.Generator.Tests;

public class UnitTest1
{
    [Fact]
    public async Task Test1()
    {
        ISearchApiClient searchClient = new SearchApiClient(null);
        ICrsConversionApiClient crsConversionApiClient = new CrsConversionApiClient(null);

        GeoJsonGeometryCollection geo;
        //geo.Geometries

        GeoJsonGeometryCollectionGeometries geo1;

        //Class1 class1 = new Class1();
    }
}
