# Usage

Each OSDU service has its own namespace under `OsduCsharpClient`. Clients are constructed with a Kiota `IRequestAdapter`, which handles HTTP and authentication.

## Set Up the Request Adapter

```csharp
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

// Implement IAccessTokenProvider to supply your bearer token
var authProvider = new BaseBearerTokenAuthenticationProvider(new MyTokenProvider());
var adapter = new HttpClientRequestAdapter(authProvider)
{
    BaseUrl = "https://your-osdu-instance.com/api/entitlements/v2"
};
```

## Example: Entitlements service

```csharp
using OsduCsharpClient.Entitlements;

var client = new EntitlementsClient(adapter);

var result = await client.Groups.All.GetAsync(config =>
{
    config.QueryParameters.Type = "NONE";
    config.Headers.Add("data-partition-id", "your-partition-id");
});

if (result?.Groups is not null)
{
    foreach (var group in result.Groups)
        Console.WriteLine($"{group.Name} - {group.Email}");
}
```

## Example: Search service

```csharp
using OsduCsharpClient.Search;
using OsduCsharpClient.Search.Models;

var client = new SearchClient(adapter);

var result = await client.Query.PostAsync(
    new QueryRequest
    {
        Kind = new QueryRequest.QueryRequest_kind
        {
            QueryRequestKindString = "osdu:wks:work-product-component--WellLog:*"
        },
        Query = "*",
        Limit = 10,
        ReturnedFields = ["id", "kind", "createTime"],
    },
    config => config.Headers.Add("data-partition-id", "your-partition-id"));

if (result?.Results is not null)
{
    foreach (var record in result.Results)
        Console.WriteLine(record.AdditionalData["id"]);
}
```

## Accessing raw JSON

OSDU records share a common envelope (`id`, `kind`, `createTime`, etc.) but the `data` block varies by record kind. The Kiota-generated client gives you typed access to the common fields, but the `data` block is represented as `AdditionalData` with `UntypedNode` values because the OpenAPI spec defines it as a free-form object.

When you need the raw JSON — for example to access the kind-specific `data` block — use Kiota's `NativeResponseHandler` to intercept the `HttpResponseMessage` directly.

### Getting raw JSON with NativeResponseHandler

```csharp
using Microsoft.Kiota.Abstractions;
using System.Net.Http;

var nativeResponseHandler = new NativeResponseHandler();

await client.Ddms.V3.Wellbores[wellboreId].GetAsync(config =>
{
    config.Headers.Add("data-partition-id", dataPartitionId);
    config.Options.Add(new ResponseHandlerOption { ResponseHandler = nativeResponseHandler });
});

var httpResponse = (HttpResponseMessage)nativeResponseHandler.Value!;
var json = await httpResponse.Content.ReadAsStringAsync();
Console.WriteLine(json);
```

> **Note:** When a `NativeResponseHandler` is used, Kiota hands off the response to the handler instead of deserializing it into a typed model. The method returns the default value (`null` for reference types). Use `json` with `System.Text.Json` to access any fields you need.

### Working with the data block via System.Text.Json

```csharp
using var doc = JsonDocument.Parse(json);

// Pretty-print the entire record
Console.WriteLine(JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));

// Extract a specific field from data
if (doc.RootElement.TryGetProperty("data", out var data)
    && data.TryGetProperty("WellboreID", out var val))
{
    Console.WriteLine($"WellboreID: {val.GetString()}");
}
```

### Why This Is Needed

Kiota is a typed client generator. It maps OpenAPI schemas to C# classes. The OSDU record `data` property is defined as an open-ended `object` with `additionalProperties: true` because its shape depends on the record kind (well, wellbore, well log, and so on). Without a fixed schema, Kiota falls back to `AdditionalData` with `UntypedNode` wrappers. Reading the raw JSON via `NativeResponseHandler` and `System.Text.Json` is the recommended way to consume these free-form fields.
