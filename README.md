# OSDU C# Client

[![SCM Compliance](https://scm-compliance-api.radix.equinor.com/repos/equinor/ee3bb3b0-3485-4f5c-b37c-864b29c84914/badge)](https://developer.equinor.com/governance/scm-policy/)

This project is a C# client library for [OSDU](https://osduforum.org/) services, automatically generated from OpenAPI specifications using [Microsoft Kiota](https://github.com/microsoft/kiota).

It provides typed, async clients for various OSDU core services, allowing for easy integration with OSDU APIs in .NET applications.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

For regenerating clients locally:

- [Kiota CLI](https://learn.microsoft.com/en-us/openapi/kiota/install)

  ```sh
  dotnet tool install --global Microsoft.OpenApi.Kiota
  ```

- Python 3.10+

## Installation

The package is published to [GitHub Packages](https://github.com/equinor/osdu-csharp-client/packages).

Add the Equinor NuGet feed (once per machine), then install the package:

```sh
dotnet nuget add source "https://nuget.pkg.github.com/equinor/index.json" \
  --name equinor-github \
  --username <your-github-username> \
  --password <your-github-personal-access-token>

dotnet add package Equinor.OsduCsharpClient
```

> The personal access token needs the `read:packages` scope. Generate one at [github.com/settings/tokens](https://github.com/settings/tokens).

### Authentication packages

The core `Equinor.OsduCsharpClient` package is authentication-agnostic: it depends only
on your `ITokenProvider` implementation and pulls in **no** identity library of its own.
This keeps the SDK from pinning an MSAL version that could clash with your application's,
and removes it as a supply-chain surface you don't control.

You supply auth in one of two ways:

- **Bring your own** — implement `ITokenProvider` (a single `GetTokenAsync` method), or use
  the built-in `StaticTokenProvider` when you already hold a token.
- **Use the optional MSAL package** — install `Equinor.OsduCsharpClient.Msal` for ready-made
  MSAL providers (`MsalInteractiveTokenProvider`, `MsalDeviceFlowTokenProvider`,
  `MsalClientCredentialsTokenProvider`) with OS-encrypted token caching. MSAL is then a
  dependency you own and can update independently of this SDK.

  ```sh
  dotnet add package Equinor.OsduCsharpClient.Msal
  ```

## Quick Start

The `OsduClient` facade handles token acquisition (via the `ITokenProvider` you pass) and
`data-partition-id` injection automatically. This example uses the optional MSAL package:

```csharp
using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.Facade.Auth; // MsalInteractiveTokenProvider (Msal package)
using Equinor.OsduCsharpClient.Search.Models;
using Microsoft.Extensions.Configuration;

var config = OsduConfig.FromConfiguration(builder.Configuration);
using var osdu = new OsduClient(config, new MsalInteractiveTokenProvider(config));

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

`OsduConfig.FromConfiguration(IConfiguration)` binds the `Osdu` section (`Server`, `DataPartitionId`, `Authority`, `ClientId`, `Scopes`) from any standard .NET configuration source — `appsettings.json`, environment variables (`Osdu__Server`), user secrets, etc. See [docs/environment-and-tests.md](docs/environment-and-tests.md) for setup.

For low-level usage (constructing service clients directly with a raw adapter), see [docs/usage.md](docs/usage.md).

Wellbore DDMS bulk data (well-log curves) can be read and written as Parquet via `osdu.WellboreDdmsBulk` — including chunked session writes for large datasets. See [docs/usage.md](docs/usage.md#wellbore-ddms-parquet-bulk-data).

## Typed schema models (optional companion)

This client keeps each record's free-form `data` block as an `UntypedNode`, matching the canonical OSDU `Map<String, Object>` model — a single client cannot hard-code every OSDU kind. When you want intellisense and compile-time types for a specific OSDU `kind` and version, pair the client with the companion [`equinor/osdu-csharp-schemas`](https://github.com/equinor/osdu-csharp-schemas) (`Equinor.Osdu.Schemas`) library. It provides typed POCOs for `work-product-component`, `master-data`, and `dataset` entity types that bridge into a record envelope through the client's `ToUntypedNode()` / `Deserialize<T>()` extensions — no changes to the client required.

```csharp
using V15 = Osdu.Schemas.WorkProductComponent.WellLog.V1_5_0;
using Equinor.OsduCsharpClient.Facade; // ToUntypedNode() / Deserialize<T>()

// Read: envelope from the client, data as a typed schema POCO.
var record = await osdu.WellboreDdms.Ddms.V3.Welllogs[id].GetAsync();
V15.Data data = record.Data.Deserialize<V15.Data>(); // UntypedNode → POCO
Console.WriteLine(data.Name);                         // typed property, not data["name"]

// Write: author the data as a POCO, bridge back to the envelope.
record.Data = data.ToUntypedNode();                   // POCO → UntypedNode
```

Runnable, end-to-end examples combining both libraries live in the separate [`equinor/osdu-csharp-samples`](https://github.com/equinor/osdu-csharp-samples) repository — covering search, get, versioning, reference navigation, bulk-data read/write, and typed WellLog ingestion.

## Available Services

| Namespace                                   | Service                    |
| ------------------------------------------- | -------------------------- |
| `OsduCsharpClient.CrsCatalog`               | CRS Catalog                |
| `OsduCsharpClient.CrsConversion`            | CRS Conversion             |
| `OsduCsharpClient.Dataset`                  | Dataset                    |
| `OsduCsharpClient.Entitlements`             | Entitlements               |
| `OsduCsharpClient.File`                     | File                       |
| `OsduCsharpClient.Indexer`                  | Indexer                    |
| `OsduCsharpClient.Legal`                    | Legal                      |
| `OsduCsharpClient.Notification`             | Notification               |
| `OsduCsharpClient.Partition`                | Partition                  |
| `OsduCsharpClient.Policy`                   | Policy                     |
| `OsduCsharpClient.Register`                 | Register                   |
| `OsduCsharpClient.SchemaService`             | Schema                     |
| `OsduCsharpClient.Search`                   | Search                     |
| `OsduCsharpClient.Storage`                  | Storage                    |
| `OsduCsharpClient.UnitV2`                    | Unit v2                    |
| `OsduCsharpClient.UnitV3`                    | Unit v3                    |
| `OsduCsharpClient.WellboreDdms`             | Wellbore DDMS              |
| `OsduCsharpClient.Workflow`                 | Workflow                   |

## Running Tests

Quick run:

```sh
# Run all integration tests
dotnet test OsduCsharpClient.slnx
```

For configuration setup, optional variables, and detailed test commands, see [docs/environment-and-tests.md](docs/environment-and-tests.md).

## Development

Quick flow:

```sh
git clone https://github.com/equinor/osdu-csharp-client.git
cd osdu-csharp-client
python3 generate_all.py
dotnet build OsduCsharpClient.slnx
```

For release flow, OpenAPI update steps, response media type normalization, client regeneration, and project structure details, see [docs/development.md](docs/development.md).

## Documentation

- Environment and tests: [docs/environment-and-tests.md](docs/environment-and-tests.md)
- Usage examples (including raw JSON access): [docs/usage.md](docs/usage.md)
- Development and release workflow: [docs/development.md](docs/development.md)

## Related projects

- [`equinor/osdu-csharp-schemas`](https://github.com/equinor/osdu-csharp-schemas) — typed C# domain models (`Equinor.Osdu.Schemas`) for OSDU record `data` blocks, an opt-in companion to this client.
- [`equinor/osdu-csharp-samples`](https://github.com/equinor/osdu-csharp-samples) — runnable examples showing this client and the schema models used together.

## License

Ref. [License Information](LICENSE)
