# Usage

## Using OsduClient (Recommended)

`OsduClient` wraps all service clients with auth, `data-partition-id` injection, and connection management. Construct it once and use it for the lifetime of your application.

```csharp
using Equinor.OsduCsharpClient.Facade;
using Microsoft.Extensions.Configuration;

using var osdu = new OsduClient(OsduConfig.FromConfiguration(builder.Configuration));
```

`OsduConfig.FromConfiguration(IConfiguration)` binds the `Osdu` section from any standard
.NET configuration source — `appsettings.json`, environment variables, user secrets, command line:

```jsonc
// appsettings.json
{
  "Osdu": {
    "Server": "https://your-osdu-instance.com",
    "DataPartitionId": "your-partition-id",
    "Authority": "https://login.microsoftonline.com/<tenant-id>",
    "ClientId": "<client-id>",
    "Scopes": "api://<app-id-uri>/.default"
  }
}
```

Any value can be overridden by an environment variable using the standard double-underscore
convention, e.g. `Osdu__Server` or `Osdu__Scopes`. Pass a custom section name via
`OsduConfig.FromConfiguration(config, "MySection")`. You can also construct `OsduConfig` directly:

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

### Logging

`OsduClient` uses `Microsoft.Extensions.Logging` and produces no output by default. Pass your application's `ILoggerFactory` to enable logging:

```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

using var osdu = new OsduClient(config, loggerFactory: loggerFactory);
```

Two log categories are used:

| Category | What it logs | Default level to enable |
|---|---|---|
| `Equinor.OsduCsharpClient` | `→ METHOD URL`, `← STATUS URL (elapsed ms)`, auth flow events | `Debug` |
| `Equinor.OsduCsharpClient.Body` | Request/response bodies (truncated to 2 KB, `Authorization`/`Cookie` headers redacted) | `Debug` (opt-in) |

To enable body logging only:

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Equinor.OsduCsharpClient": "Debug",
      "Equinor.OsduCsharpClient.Body": "Debug"
    }
  }
}
```

Auth providers also accept an optional `ILoggerFactory` to log silent vs interactive/device/credential flow transitions.

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

## Working with free-form `data`

OSDU records share a common envelope (`id`, `kind`, `acl`, `legal`, `createTime`, …) but the `data` block varies by record kind. A WellLog, a Wellbore and a Trajectory all travel inside the same `Record` envelope, so no single closed C# type can describe `data` — the spec declares it as a free-form object.

### Storage, Dataset & Wellbore DDMS: `Record.Data` + the JSON bridge

The services with a generic `Record` schema — **Storage**, **Dataset** and **Wellbore DDMS** — expose `data` directly as a Kiota `UntypedNode` (`Record.Data`). The facade adds a JSON bridge in `Equinor.OsduCsharpClient.Facade` — `ToUntypedNode()` / `ToJsonNode()`, plus generic POCO overloads — so you can author and read `data` with ordinary `System.Text.Json` values instead of hand-building `UntypedNode` trees.

The example below ingests a WellLog through Wellbore DDMS; the same `Record` / JSON-bridge pattern applies to `osdu.Storage.Records.PutAsync(...)` and the Dataset registry endpoints.

**Ingesting a WellLog:**

```csharp
using System.Text.Json.Nodes;
using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.WellboreDdms.Models;

var data = JsonNode.Parse("""
{
  "Name": "GR Log",
  "WellboreID": "partition:master-data--Wellbore:abc:",
  "TopMeasuredDepth": 12345.6,
  "BottomMeasuredDepth": 13856.2,
  "Curves": [ { "Mnemonic": "GR", "NumberOfColumns": 1 } ]
}
""");

var record = new Record
{
    Kind  = "osdu:wks:work-product-component--WellLog:1.2.0",
    Acl   = new StorageAcl { Owners = ["..."], Viewers = ["..."] },
    Legal = new Legal { /* legaltags, otherRelevantDataCountries */ },
    Data  = data.ToUntypedNode(),
};

var response = await osdu.WellboreDdms.Ddms.V3.Welllogs.PostAsync([record]);
```

**Reading `data` back:**

```csharp
var result = await osdu.WellboreDdms.Ddms.V3.Welllogs[welllogId].GetAsync();

JsonNode? data = result?.Data.ToJsonNode();
Console.WriteLine((string?)data?["Name"]);
```

**Bridging your own POCOs:**

```csharp
record.Data = myWellLog.ToUntypedNode();                // POCO  -> UntypedNode
MyWellLog? wl = result?.Data.Deserialize<MyWellLog>();   // UntypedNode -> POCO
```

**JSON Merge Patch (Storage `PATCH /records/{id}`):**

The merge-patch request body uses the same pattern — `RecordMergePatchRequest.Data` is also an `UntypedNode`. Per [RFC 7396](https://datatracker.ietf.org/doc/html/rfc7396), include a JSON `null` to delete a nested key:

```csharp
var patch = new RecordMergePatchRequest
{
    Data = JsonNode.Parse("""
    {
      "Name": "Updated Well Name",
      "RetiredField": null
    }
    """).ToUntypedNode(),
};

await osdu.Storage.Records[recordId].PatchAsync(patch);
```

### Other services: raw JSON via NativeResponseHandler

Some endpoints return free-form payloads that are not exposed as a typed `Record` — for example Search query hits, which arrive as untyped maps in `AdditionalData`. For those, use Kiota's `NativeResponseHandler` to intercept the `HttpResponseMessage` directly and read the raw JSON.

#### Getting raw JSON with NativeResponseHandler

```csharp
using Microsoft.Kiota.Abstractions;
using Equinor.OsduCsharpClient.Search.Models;
using System.Net.Http;

var nativeResponseHandler = new NativeResponseHandler();

await osdu.Search.Query.PostAsync(
    new QueryRequest { Kind = new() { QueryRequestKindString = "*:*:*:*" }, Query = "*" },
    config =>
    {
        config.Options.Add(new ResponseHandlerOption { ResponseHandler = nativeResponseHandler });
    });

var httpResponse = (HttpResponseMessage)nativeResponseHandler.Value!;
var json = await httpResponse.Content.ReadAsStringAsync();
Console.WriteLine(json);
```

> **Note:** When a `NativeResponseHandler` is used, Kiota hands off the response to the handler instead of deserializing it into a typed model. The method returns the default value (`null` for reference types). Use `json` with `System.Text.Json` to access any fields you need.

#### Working with the data block via System.Text.Json

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

Kiota is a typed client generator: it maps OpenAPI schemas to closed C# classes. The OSDU record `data` property is an open-ended `object` with `additionalProperties: true` because its shape depends on the record `kind`. Kiota cannot generate a meaningful type for it.

For the services with a generic `Record` schema (Storage, Dataset, Wellbore DDMS) the spec is patched during generation so `data` is emitted as a Kiota `UntypedNode` (`Record.Data`) — see `generate_all.py`. That makes `data` round-trip arbitrary JSON, and the facade's JSON bridge turns it into ergonomic `System.Text.Json` access for both reading and writing. For endpoints that return free-form payloads outside that `Record` type, `NativeResponseHandler` + `System.Text.Json` remains the way to consume free-form fields.

## Wellbore DDMS: Parquet bulk data

Well-log bulk data is served as Parquet by Wellbore DDMS — the `/ddms/v3/{entity}/{id}/data` endpoints negotiate `application/json` **or** `application/x-parquet`, and Parquet is the primary, performant format. The generated client is JSON-only (Kiota models one content type per direction — [microsoft/kiota#3377](https://github.com/microsoft/kiota/issues/3377)), so the facade ships a hand-written bulk client at `client.WellboreDdmsBulk` that talks real `application/x-parquet` over the same authenticated transport:

```csharp
// Read bulk data as Parquet (raw stream)
await using var parquet = await client.WellboreDdmsBulk.ReadParquetAsync(
    recordId,
    new WellboreBulkReadOptions { Curves = ["MD", "GR"], Limit = 10_000 },
    cancellationToken: ct);

// Write bulk data as Parquet, creating a new record version
var version = await client.WellboreDdmsBulk.WriteParquetAsync(recordId, parquetStream, cancellationToken: ct);
```

For large datasets (> ~10M values or > 3000 columns the server requires chunking), write via a session — one call orchestrates open → upload chunks → commit, abandoning the session automatically if any step fails:

```csharp
var commit = await client.WellboreDdmsBulk.WriteParquetSessionAsync(
    recordId, [chunk1, chunk2, chunk3], SessionUpdateMode.Update, cancellationToken: ct);

// Or drive the steps yourself
var session = await client.WellboreDdmsBulk.OpenSessionAsync(recordId, SessionUpdateMode.Update, cancellationToken: ct);
await client.WellboreDdmsBulk.WriteSessionChunkParquetAsync(recordId, session.Id!.Value, chunk1, cancellationToken: ct);
var result = await client.WellboreDdmsBulk.CommitSessionAsync(recordId, session.Id!.Value, cancellationToken: ct);
// ... or AbandonSessionAsync(recordId, session.Id!.Value)
```

All methods take an `entity` parameter (`WellLogs` default, `WellboreTrajectories`, `PpfgDataset`, `WellPressureTestRawMeasurement`). Payloads are raw Parquet `Stream`s — the package takes no dependency on a Parquet library; encode/decode with e.g. [Parquet.Net](https://www.nuget.org/packages/Parquet.Net) or [Apache.Arrow](https://www.nuget.org/packages/Apache.Arrow). Remapping to `application/octet-stream` is not an option: WBDDMS validates the exact media type and rejects it.

### Escape hatch: GetRequestAdapter

For other requests the generated clients cannot express (alternate content types, hand-built URLs), `OsduClient.GetRequestAdapter(serviceAttr)` returns the authenticated Kiota adapter for any registered service. Build a `RequestInformation` and send it directly — bearer-token auth, `data-partition-id` injection, and logging are all applied:

```csharp
var adapter = client.GetRequestAdapter("wellbore_ddms");
var requestInfo = new RequestInformation(
    Method.GET, "{+baseurl}/ddms/v3/welllogs/{record_id}/data",
    new Dictionary<string, object> { { "record_id", recordId } });
requestInfo.Headers.TryAdd("Accept", "application/x-parquet");
var stream = await adapter.SendPrimitiveAsync<Stream>(requestInfo, cancellationToken: ct);
```
