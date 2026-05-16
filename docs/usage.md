# Usage

## Using OsduClient (Recommended)

`OsduClient` wraps all service clients with auth, `data-partition-id` injection, and connection management. Construct it once and use it for the lifetime of your application.

```csharp
using Equinor.OsduCsharpClient.Facade;

using var osdu = new OsduClient(OsduConfig.FromEnvironment());
```

`OsduConfig.FromEnvironment()` reads from environment variables or a `.env` file. You can also construct `OsduConfig` directly:

```csharp
var config = new OsduConfig
{
    Server          = "https://your-osdu-instance.com",
    DataPartitionId = "your-partition-id",
    Authority       = "https://login.microsoftonline.com/<tenant-id>",
    ClientId        = "<client-id>",
    Scopes          = "api://<app-id-uri>/.default",
};
using var osdu = new OsduClient(config);
```

### Example: Search service

```csharp
using Equinor.OsduCsharpClient.Search.Models;

var result = await osdu.Search.Query.PostAsync(
    new QueryRequest
    {
        Kind = new QueryRequest.QueryRequest_kind
        {
            QueryRequestKindString = "osdu:wks:work-product-component--WellLog:*"
        },
        Query = "*",
        Limit = 10,
        ReturnedFields = ["id", "kind", "createTime"],
    });

if (result?.Results is not null)
{
    foreach (var record in result.Results)
        Console.WriteLine(record.AdditionalData["id"]);
}
```

### Example: Entitlements service

```csharp
var result = await osdu.Entitlements.Groups.All.GetAsync(config =>
{
    config.QueryParameters.Type = "NONE";
});

if (result?.Groups is not null)
{
    foreach (var group in result.Groups)
        Console.WriteLine($"{group.Name} - {group.Email}");
}
```

### Auth providers

By default `OsduClient` uses `MsalInteractiveTokenProvider` (browser popup on first run, then silent from cache). Choose the mode that fits your environment:

| Provider | When to use | Config needed |
|---|---|---|
| `MsalInteractiveTokenProvider` | Local dev, opens browser | `Authority`, `ClientId`, `Scopes` |
| `MsalDeviceFlowTokenProvider` | Headless / SSH sessions | `Authority`, `ClientId`, `Scopes` |
| `MsalClientCredentialsTokenProvider` | CI / service-to-service | + `clientSecret` |
| `StaticTokenProvider` | Testing / externally managed token | pre-acquired token string |

```csharp
using Equinor.OsduCsharpClient.Facade.Auth;

// Interactive (default — opens browser on first run)
using var osdu = new OsduClient(config);

// Device code flow (prints a URL + code to the console; no browser required on this machine)
using var osdu = new OsduClient(config, new MsalDeviceFlowTokenProvider(config));

// Client credentials (CI / service-to-service, no user interaction)
using var osdu = new OsduClient(config, new MsalClientCredentialsTokenProvider(config, clientSecret: "..."));

// Pre-acquired token
using var osdu = new OsduClient(config, new StaticTokenProvider("your-bearer-token"));
```

All three MSAL providers persist the token cache to `~/.osdu/msal_cache.bin` by default (override with `OSDU_MSAL_CACHE_PATH` env var), so silent renewal is used on subsequent runs.

---

## Low-level: Raw Service Clients

Each service client can also be constructed directly with a Kiota `IRequestAdapter` when you need full control over auth and HTTP configuration.

### Set Up the Request Adapter

```csharp
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

var authProvider = new BaseBearerTokenAuthenticationProvider(new MyTokenProvider());
var adapter = new HttpClientRequestAdapter(authProvider)
{
    BaseUrl = "https://your-osdu-instance.com/api/entitlements/v2"
};
```

### Example: Entitlements service

```csharp
using Equinor.OsduCsharpClient.Entitlements;

var client = new EntitlementsClient(adapter);

var result = await client.Groups.All.GetAsync(config =>
{
    config.QueryParameters.Type = "NONE";
    config.Headers.Add("data-partition-id", "your-partition-id");
});
```

### Example: Search service

```csharp
using Equinor.OsduCsharpClient.Search;
using Equinor.OsduCsharpClient.Search.Models;

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
```

---

## Accessing raw JSON

OSDU records share a common envelope (`id`, `kind`, `createTime`, etc.) but the `data` block varies by record kind. The Kiota-generated client gives you typed access to the common fields, but the `data` block is represented as `AdditionalData` with `UntypedNode` values because the OpenAPI spec defines it as a free-form object.

When you need the raw JSON — for example to access the kind-specific `data` block — use Kiota's `NativeResponseHandler` to intercept the `HttpResponseMessage` directly.

### Getting raw JSON with NativeResponseHandler

```csharp
using Microsoft.Kiota.Abstractions;
using System.Net.Http;

var nativeResponseHandler = new NativeResponseHandler();

await osdu.WellboreDdms.Ddms.V3.Wellbores[wellboreId].GetAsync(config =>
{
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
