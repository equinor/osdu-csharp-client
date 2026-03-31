# Development

## Getting Started

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

## Releasing a New Version

Releases are automated using [Release Please](https://github.com/googleapis/release-please).

How it works:

1. On merge to `main`, Release Please checks new commits since the last release using the [Conventional Commits](https://www.conventionalcommits.org/) format.
2. When releasable changes are found, Release Please creates or updates a release pull request that bumps the version in [`OsduCsharpClient.csproj`](../src/OsduCsharpClient/OsduCsharpClient.csproj) and updates [`CHANGELOG.md`](../CHANGELOG.md).
3. When the release pull request is merged, the release workflow creates a GitHub release and publishes the NuGet package.

## Updating OpenAPI Specs

To fetch the latest OpenAPI specifications from the OSDU wiki:

```sh
python3 download.py
```

This script parses the OSDU wiki for service definitions and downloads the corresponding JSON specs into `openapi_specs/`, trying Community Implementation, Azure, AWS, and GCP sources in order.

> Warning: The raw upstream specs are not always generator-friendly. This repository may intentionally apply local edits to files in `openapi_specs/` to improve generated client quality. Check `git diff` after running `download.py` before committing.

## Normalizing OpenAPI Response Media Types

Some OSDU endpoints declare structured JSON responses under `*/*` instead of `application/json`. The included script fixes these in place:

```sh
# Check what would be changed (dry-run)
python3 fix_openapi_json_response_media_types.py --check

# Apply fixes to all specs
python3 fix_openapi_json_response_media_types.py

# Target a specific file
python3 fix_openapi_json_response_media_types.py openapi_specs/Search.json
```

## Regenerating Clients

To regenerate all C# clients from the specs in `openapi_specs/`:

```sh
python3 generate_all.py
```

This iterates through the JSON files and runs `kiota generate` for each service into `src/OsduCsharpClient/<ServiceName>/`. It also handles minor spec patches (missing `info.version`, non-standard `< * >` wildcard properties) before invoking Kiota.

> Warning: Do not hand-edit files under `src/OsduCsharpClient/`. They are generated artifacts and will be overwritten the next time `generate_all.py` is run. Make changes in `openapi_specs/` and/or the generation scripts instead.

## Project Structure

```txt
openapi_specs/                              Downloaded OpenAPI JSON specifications
src/
    OsduCsharpClient/                       Generated C# clients (one subfolder per service)
tests/
    OsduCsharpClient.IntegrationTests/      xUnit integration tests
download.py                                 Downloads specs from the OSDU wiki
fix_openapi_json_response_media_types.py    Normalizes */* response media types
generate_all.py                             Regenerates all C# clients via Kiota
```
