using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

public class SearchSchemasExample(IOsduClient osduClient) : ExampleBase
{
    [ExampleParameter(DisplayName = "Authority", Order = 0, Description = "Filter by schema authority (e.g. 'osdu').")]
    public string Authority { get; set; } = "osdu";

    [ExampleParameter(DisplayName = "Entity Type", Order = 1, Description = "Filter by entity type (e.g. 'Wellbore').")]
    public string EntityType { get; set; } = "Wellbore";

    [ExampleParameter(DisplayName = "Limit", Order = 2, Description = "Maximum number of schemas to return.")]
    public int Limit { get; set; } = 10;

    public override string Text => "Search Schemas";

    public override string ShortDescription => "Searches the Schema registry with optional filters for authority, entity type, and more.";

    public override string Category => "Schema";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Schema.GetSchemaAsync(
            authority: Authority,
            entityType: EntityType,
            latestVersion: true,
            limit: Limit,
            cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
