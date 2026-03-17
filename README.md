# OSDU C# Client

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

## .env Setup

Integration tests load configuration from a `.env` file in the repository root.

Create `.env` with the required values for your OSDU environment:

```dotenv
# Base OSDU host (no trailing slash)
SERVER=https://your-osdu-instance.com

# Required for authenticated test runs
DATA_PARTITION_ID=your-partition-id
AUTHORITY=https://login.microsoftonline.com/<tenant-id>
CLIENT_ID=<public-client-id>
SCOPES=api://<app-id-uri>/.default
```

Optional environment variables used by tests:

- `OSDU_MSAL_CACHE_PATH` — path to a persistent MSAL token cache file (default: `.msal_token_cache.bin` in the repo root)
- `SEARCH_KIND` — kind filter for search tests (default: `osdu:wks:work-product-component--WellLog:*`)
- `SEARCH_QUERY` — query string for search tests (default: `*`)
- `SEARCH_LIMIT` — result limit for search tests (default: `5`)
- `GROUP_TYPE` — group type filter for entitlements tests (default: `NONE`)

## Usage

Each OSDU service has its own namespace under `OsduCsharpClient`. Clients are constructed with a Kiota `IRequestAdapter`, which handles HTTP and authentication.

### Setting up the request adapter

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

### Example: Entitlements Service

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

### Example: Search Service

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

### Available Services

| Namespace | Service |
|---|---|
| `OsduCsharpClient.CrsCatalog` | CRS Catalog |
| `OsduCsharpClient.CrsConversion` | CRS Conversion |
| `OsduCsharpClient.Dataset` | Dataset |
| `OsduCsharpClient.Entitlements` | Entitlements |
| `OsduCsharpClient.File` | File |
| `OsduCsharpClient.Indexer` | Indexer |
| `OsduCsharpClient.IngestionWorkflowService` | Ingestion Workflow Service |
| `OsduCsharpClient.Legal` | Legal |
| `OsduCsharpClient.Notification` | Notification |
| `OsduCsharpClient.Partition` | Partition |
| `OsduCsharpClient.Policy` | Policy |
| `OsduCsharpClient.Register` | Register |
| `OsduCsharpClient.Schema` | Schema |
| `OsduCsharpClient.Search` | Search |
| `OsduCsharpClient.Storage` | Storage |
| `OsduCsharpClient.Unit` | Unit |
| `OsduCsharpClient.WellboreDdms` | Wellbore DDMS |

## Running Tests

Integration tests hit a real OSDU server. They require a `.env` file (see above). On first run a browser window will open for interactive MSAL login; the resulting token is cached in `.msal_token_cache.bin`.

```sh
# Run all integration tests
dotnet test OsduCsharpClient.slnx

# Run a single test by name
dotnet test OsduCsharpClient.slnx --filter "FullyQualifiedName~QueryRecords_ReturnsResults"

# Run tests and see printed output
dotnet test OsduCsharpClient.slnx --logger "console;verbosity=detailed"
```

Optional env vars for Wellbore DDMS tests (set in `.env` or shell):

- `WELLBORE_DDMS_WELLBORE_ID` — runs `GetWellbore_ById_ReturnsRecord`
- `WELLBORE_DDMS_WELL_ID` — runs `GetWell_ById_ReturnsRecord`

## Development

### Getting started

Clone the repo, then generate the clients and build:

```sh
git clone https://github.com/equinor/osdu-csharp-client.git
cd osdu-csharp-client
python3 generate_all.py
dotnet build OsduCsharpClient.slnx
```

Copy `.env` from the template and fill in your OSDU environment values before running tests:

```sh
cp .env.example .env   # if an example exists, otherwise create manually
dotnet test OsduCsharpClient.slnx
```

### Releasing a new version

1. Bump `<Version>` in `src/OsduCsharpClient/OsduCsharpClient.csproj`
2. Commit and push to `main`
3. Tag the commit and push the tag — this triggers the publish to GitHub Packages:

```sh
git tag v0.2.0
git push origin v0.2.0
```

### Updating OpenAPI Specs

To fetch the latest OpenAPI specifications from the OSDU wiki:

```sh
python3 download.py
```

This script parses the OSDU wiki for service definitions and downloads the corresponding JSON specs into `openapi_specs/`, trying Community Implementation, Azure, AWS, and GCP sources in order.

> **Warning:** The raw upstream specs are not always generator-friendly. This repository may intentionally apply local edits to files in `openapi_specs/` to improve generated client quality. Check git diff after running `download.py` before committing.

### Normalizing OpenAPI Response Media Types

Some OSDU endpoints declare structured JSON responses under `*/*` instead of `application/json`. The included script fixes these in place:

```sh
# Check what would be changed (dry-run)
python3 fix_openapi_json_response_media_types.py --check

# Apply fixes to all specs
python3 fix_openapi_json_response_media_types.py

# Target a specific file
python3 fix_openapi_json_response_media_types.py openapi_specs/Search.json
```

### Regenerating Clients

To regenerate all C# clients from the specs in `openapi_specs/`:

```sh
python3 generate_all.py
```

This iterates through the JSON files and runs `kiota generate` for each service into `src/OsduCsharpClient/<ServiceName>/`. It also handles minor spec patches (missing `info.version`, non-standard `< * >` wildcard properties) before invoking Kiota.

> **Warning:** Do not hand-edit files under `src/OsduCsharpClient/`. They are generated artifacts and will be overwritten the next time `generate_all.py` is run. Make changes in `openapi_specs/` and/or the generation scripts instead.

## Project Structure

```
openapi_specs/          Downloaded OpenAPI JSON specifications
src/OsduCsharpClient/   Generated C# clients (one subfolder per service)
tests/
  OsduCsharpClient.IntegrationTests/   xUnit integration tests
download.py             Downloads specs from the OSDU wiki
fix_openapi_json_response_media_types.py  Normalises */* response media types
generate_all.py         Regenerates all C# clients via Kiota
```

## License

Ref. [License Information](LICENSE)
