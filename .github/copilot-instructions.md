# Copilot Instructions

## Architecture Overview

This repo generates a C# client library for OSDU (Open Subsurface Data Universe) platform APIs using [Microsoft Kiota](https://github.com/microsoft/kiota).

**The pipeline is:**
1. `download.py` — fetches OpenAPI JSON specs from the OSDU community wiki into `openapi_specs/`
2. `fix_openapi_json_response_media_types.py` — normalizes `*/*` response media types to `application/json` for structured schemas
3. `generate_all.py` — runs `kiota generate` for each spec, producing C# clients under `src/OsduCsharpClient/<ServiceName>/`

**All code under `src/OsduCsharpClient/<ServiceName>/` is auto-generated** by Kiota from the OpenAPI specs. Do not hand-edit generated files; re-run `generate_all.py` instead.

Services covered: CrsCatalog, CrsConversion, Dataset, Entitlements, File, Indexer, IngestionWorkflowService, Legal, Notification, Partition, Policy, Register, Schema, Search, Storage, Unit.

## Build Commands

```sh
# Build the solution
dotnet build OsduCsharpClient.slnx

# Restore packages only
dotnet restore OsduCsharpClient.slnx
```

## Test Commands

Tests are integration tests that hit a real OSDU server (no mocking). They require a `.env` file in the repo root:

```sh
# Run all integration tests
dotnet test OsduCsharpClient.slnx

# Run a single test by name
dotnet test OsduCsharpClient.slnx --filter "FullyQualifiedName~QueryRecords_ReturnsResults"
```

Required `.env` variables:
```
SERVER=https://your-osdu-instance.example.com
DATA_PARTITION_ID=your-partition
AUTHORITY=https://login.microsoftonline.com/your-tenant-id
CLIENT_ID=your-app-client-id
SCOPES=https://your-osdu-scope/.default
```

Optional overrides: `SEARCH_KIND`, `SEARCH_QUERY`, `SEARCH_LIMIT`, `GROUP_TYPE`, `OSDU_MSAL_CACHE_PATH`.

Auth uses MSAL interactive login (browser pop-up on first run) with a persistent token cache at `.msal_token_cache.bin`.

## Code Generation Workflow

Prerequisites: `kiota` CLI installed (`dotnet tool install --global Microsoft.OpenApi.Kiota`), Python 3.

```sh
# 1. Download/refresh OpenAPI specs from OSDU wiki
python download.py

# 2. Fix wildcard media types in specs (idempotent)
python fix_openapi_json_response_media_types.py

# 3. Check what fix_openapi... would change without writing (dry-run)
python fix_openapi_json_response_media_types.py --check

# 4. Regenerate all C# clients from specs
python generate_all.py
```

## Key Conventions

- **Namespace pattern:** `OsduCsharpClient.<ServiceName>` — e.g., `OsduCsharpClient.Search`, `OsduCsharpClient.CrsCatalog`
- **Client class name:** `<ServiceName>Client` — e.g., `SearchClient`, `CrsCatalogClient`
- **Output path:** `src/OsduCsharpClient/<PascalCaseServiceName>/`
- **`kiota-lock.json`** in each service folder records the generation parameters (class name, namespace, Kiota version, spec hash). Kiota uses it to regenerate consistently.
- `generate_all.py` applies two in-memory patches before invoking Kiota:
  - Adds a missing `info.version` field if absent (some OSDU specs omit it)
  - Converts `"< * >"` wildcard property keys to `additionalProperties` (non-standard OpenAPI used in Partition spec)
- The solution targets **net10.0** with nullable reference types and implicit usings enabled.
- All Kiota packages are pinned at version `1.22.0` in the `.csproj`.
