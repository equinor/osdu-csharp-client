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

## Accessing raw JSON alongside typed models

OSDU records share a common envelope (`id`, `kind`, `createTime`, etc.) but the `data` block varies by record kind. The Kiota-generated client gives you typed access to the common fields, but the `data` block is represented as `AdditionalData` with `UntypedNode` values because the OpenAPI spec defines it as a free-form object.

In many OSDU workflows you want both: the typed model for the envelope and the raw JSON for the kind-specific `data` block. Kiota supports this via the built-in `BodyInspectionHandler`, which is part of the default middleware pipeline.

### Setup

No special client configuration is needed. Create the client as usual:

```csharp
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Http.HttpClientLibrary.Middleware.Options;

var adapter = new HttpClientRequestAdapter(authProvider) { BaseUrl = baseUrl };
var client = new WellboreDdmsClient(adapter);
```

### Per-request body inspection

Pass a `BodyInspectionHandlerOption` on any request where you want the raw JSON:

```csharp
var bodyInspection = new BodyInspectionHandlerOption { InspectResponseBody = true };

// Kiota returns the typed model as usual
var wellbore = await client.Ddms.V3.Wellbores[wellboreId].GetAsync(h =>
{
    h.Headers.Add("data-partition-id", dataPartitionId);
    h.Options.Add(bodyInspection);
});

// Typed access to common OSDU record fields
Console.WriteLine(wellbore?.Kind);
Console.WriteLine(wellbore?.CreateTime?.DateTimeOffset);

// Raw JSON: the full record as the server returned it
bodyInspection.ResponseBody.Seek(0, SeekOrigin.Begin);
using var reader = new StreamReader(bodyInspection.ResponseBody, leaveOpen: true);
var json = reader.ReadToEnd();
Console.WriteLine(json);
```

### Working with the data block via System.Text.Json

Once you have the raw JSON, you can use `JsonDocument` to navigate the kind-specific `data` fields without unpacking `UntypedNode` values:

```csharp
var doc = JsonDocument.Parse(json);

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

Kiota is a typed client generator. It maps OpenAPI schemas to C# classes. The OSDU record `data` property is defined as an open-ended `object` with `additionalProperties: true` because its shape depends on the record kind (well, wellbore, well log, and so on). Without a fixed schema, Kiota falls back to `AdditionalData` with `UntypedNode` wrappers.

The `BodyInspectionHandlerOption` approach gives you both:

- Typed access to the common record envelope via the Kiota model
- Raw JSON for the kind-specific `data` block, using standard `System.Text.Json` APIs

The same `BodyInspectionHandlerOption` instance can be reused across multiple requests; the `ResponseBody` stream is replaced on each call.
