# OSDU C# Client

[![SCM Compliance](https://scm-compliance-api.radix.equinor.com/repos/equinor/ee3bb3b0-3485-4f5c-b37c-864b29c84914/badge)](https://developer.equinor.com/governance/scm-policy/)

This project is a C# client library for [OSDU](https://osduforum.org/) services, automatically generated from OpenAPI specifications using [Microsoft Kiota](https://github.com/microsoft/kiota).

It provides typed, async clients for various OSDU core services, allowing for easy integration with OSDU APIs in .NET applications.

## Generated code is not committed

The C# clients under `src/OsduCsharpClient/` are produced by running Kiota against the OpenAPI specs in `openapi_specs/`. This output is **not committed to the repository** for the following reasons:

- **Nobody can accidentally edit it.** If the generated code is not in the repository, it cannot be hand-edited. Any change must go through the spec and the generator — the only correct way to change it.
- **The spec is the source of truth.** Committing generated code creates a second source of truth that can silently drift from the spec.
- **Diffs stay meaningful.** A spec change generates hundreds of touched lines across dozens of files. Keeping generated code out of git means pull request diffs show only what actually changed.
- **Reproducible by design.** Given the same spec and the same Kiota version, generation is deterministic. Storing the result is redundant.

Consumers of the published NuGet package can still browse the generated client code through their IDE (Visual Studio, Rider, VS Code with C# Dev Kit) using decompilation and the included XML documentation. AI coding assistants also work against the installed package. Contributors working in this repository should run `python3 generate_all.py` once after cloning to have the generated code available locally.

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

## Quick Start

Each OSDU service has its own namespace under `OsduCsharpClient`. Clients are constructed with a Kiota `IRequestAdapter`, which handles HTTP and authentication.

Minimal example (Search):

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

For full examples (request adapter setup, Entitlements usage, Search usage, and accessing raw JSON), see [docs/usage.md](docs/usage.md).

## Available Services

| Namespace                                   | Service                    |
| ------------------------------------------- | -------------------------- |
| `OsduCsharpClient.CrsCatalog`               | CRS Catalog                |
| `OsduCsharpClient.CrsConversion`            | CRS Conversion             |
| `OsduCsharpClient.Dataset`                  | Dataset                    |
| `OsduCsharpClient.Entitlements`             | Entitlements               |
| `OsduCsharpClient.File`                     | File                       |
| `OsduCsharpClient.Indexer`                  | Indexer                    |
| `OsduCsharpClient.IngestionWorkflowService` | Ingestion Workflow Service |
| `OsduCsharpClient.Legal`                    | Legal                      |
| `OsduCsharpClient.Notification`             | Notification               |
| `OsduCsharpClient.Partition`                | Partition                  |
| `OsduCsharpClient.Policy`                   | Policy                     |
| `OsduCsharpClient.Register`                 | Register                   |
| `OsduCsharpClient.Schema`                   | Schema                     |
| `OsduCsharpClient.Search`                   | Search                     |
| `OsduCsharpClient.Storage`                  | Storage                    |
| `OsduCsharpClient.Unit`                     | Unit                       |
| `OsduCsharpClient.WellboreDdms`             | Wellbore DDMS              |

## Running Tests

Quick run:

```sh
# Run all integration tests
dotnet test OsduCsharpClient.slnx
```

For `.env` setup, optional variables, and detailed test commands, see [docs/environment-and-tests.md](docs/environment-and-tests.md).

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

## License

Ref. [License Information](LICENSE)
