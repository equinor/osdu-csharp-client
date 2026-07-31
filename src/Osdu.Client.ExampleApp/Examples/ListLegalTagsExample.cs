using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

public class ListLegalTagsExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "List Legal Tags";

    public override string ShortDescription => "Retrieves all valid legal tags from the Legal service.";

    public override string Category => "Legal";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Legal.GetLegaltagsAsync(valid: true, cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
