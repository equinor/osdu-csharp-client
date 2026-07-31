using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

public class LegalTagPropertiesExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "Legal Tag Properties";

    public override string ShortDescription => "Retrieves the allowed property values for legal tags.";

    public override string Category => "Legal";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Legal.GetLegaltagsPropertiesAsync(cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
