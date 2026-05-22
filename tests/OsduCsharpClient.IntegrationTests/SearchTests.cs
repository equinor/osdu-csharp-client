using Equinor.OsduCsharpClient.Search.Models;
using Xunit;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// Integration tests for the Search service.
/// </summary>
[Collection("Osdu")]
public class SearchTests(OsduFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task QueryRecords_ReturnsResults()
    {
        var kind = Environment.GetEnvironmentVariable("SEARCH_KIND")
            ?? "osdu:wks:work-product-component--WellLog:*";
        var query = Environment.GetEnvironmentVariable("SEARCH_QUERY") ?? "*";
        var limit = int.TryParse(Environment.GetEnvironmentVariable("SEARCH_LIMIT"), out var l) ? l : 5;

        // The facade OsduClient injects auth and the data-partition-id header.
        var result = await fixture.Client.Search.Query.PostAsync(
            new QueryRequest
            {
                Kind = new QueryRequest.QueryRequest_kind { QueryRequestKindString = kind },
                Query = query,
                Limit = limit,
                ReturnedFields = ["id", "kind", "createTime"],
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Results);
        foreach (var record in result.Results)
            output.WriteLine(string.Join(", ", record.AdditionalData.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    [Fact]
    public async Task QueryWellboresForGivenField_ReturnsResults()
    {
        var search = fixture.Client.Search;

        var fieldResult = await search.Query.PostAsync(
            new QueryRequest
            {
                Kind = new QueryRequest.QueryRequest_kind { QueryRequestKindString = "osdu:wks:master-data--Field:*" },
                Query = "data.FieldName:\"AASTA HANSTEEN\"",
                Limit = 1,
                ReturnedFields = ["id"],
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(fieldResult);
        Assert.NotNull(fieldResult.Results);
        Assert.NotEmpty(fieldResult.Results);

        var fieldId = fieldResult.Results[0].AdditionalData["id"]?.ToString();
        Assert.NotNull(fieldId);
        output.WriteLine($"Field ID: {fieldId}");

        var wellboreResult = await search.Query.PostAsync(
            new QueryRequest
            {
                Kind = new QueryRequest.QueryRequest_kind { QueryRequestKindString = "osdu:wks:master-data--Wellbore:*" },
                Query = $"nested(data.GeoContexts, (FieldID:\"{fieldId}\"))",
                Limit = 100,
                ReturnedFields = ["id", "kind", "createTime"],
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(wellboreResult);
        Assert.NotNull(wellboreResult.Results);
        foreach (var record in wellboreResult.Results)
            output.WriteLine(string.Join(", ", record.AdditionalData.Select(kv => $"{kv.Key}={kv.Value}")));
    }
}
