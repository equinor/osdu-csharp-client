# Development

## Generated Code Is Not Committed

The C# clients under `src/OsduCsharpClient/Generated/` are produced by running Kiota against the OpenAPI specs in `openapi_specs/`. This output is **not committed to the repository** for the following reasons:

- **Nobody can accidentally edit it.** If the generated code is not in the repository, it cannot be hand-edited. Any change must go through the spec and the generator — the only correct way to change it.
- **The spec is the source of truth.** Committing generated code creates a second source of truth that can silently drift from the spec.
- **Diffs stay meaningful.** A spec change generates hundreds of touched lines across dozens of files. Keeping generated code out of git means pull request diffs show only what actually changed.
- **Reproducible by design.** Given the same spec and the same Kiota version, generation is deterministic. Storing the result is redundant.

Consumers of the published NuGet package can still browse the generated client code through their IDE (Visual Studio, Rider, VS Code with C# Dev Kit) using decompilation and the included XML documentation. AI coding assistants also work against the installed package. Contributors working in this repository should run `python3 generate_all.py` once after cloning to have the generated code available locally.

## Getting Started

Clone the repo, then generate the clients and build:

```sh
git clone https://github.com/equinor/osdu-csharp-client.git
cd osdu-csharp-client
python3 generate_all.py
dotnet build OsduCsharpClient.slnx
```

Provide configuration (e.g. `appsettings.local.json` or `Osdu__*` environment variables) before running tests — see [docs/environment-and-tests.md](environment-and-tests.md).

```sh
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

This iterates through all JSON and YAML specs in `openapi_specs/` and runs `kiota generate` for each service into `src/OsduCsharpClient/Generated/<ServiceName>/`. It also handles minor spec patches before invoking Kiota:

- missing `info.version`
- non-standard `< * >` wildcard properties
- YAML timestamp normalization
- untyping the free-form OSDU `data` field on `Record` (Storage, Dataset, Wellbore DDMS) and `RecordMergePatchRequest` (Storage `PATCH /records/{id}`) so Kiota emits an `UntypedNode` instead of an empty `*_data` class ([#38](https://github.com/equinor/osdu-csharp-client/issues/38))

These patches are applied in memory only — the files in `openapi_specs/` are not modified.

> Warning: Do not hand-edit files under `src/OsduCsharpClient/Generated/`. They are generated artifacts and will be overwritten the next time `generate_all.py` is run. Make changes in `openapi_specs/` and/or the generation scripts instead.

## Adding a New Service

1. **Add the OpenAPI spec** to `openapi_specs/` (`.json`, `.yaml`, or `.yml`).

2. **Regenerate** — `generate_all.py` auto-discovers all specs, so no script changes are needed:
   ```sh
   python3 generate_all.py
   ```
   This creates `src/OsduCsharpClient/Generated/<PascalCaseName>/` with a `<PascalCaseName>Client` class.

3. **Register the endpoint** in `src/OsduCsharpClient/Facade/ServiceRegistry.cs`:
   ```csharp
   new("my_service", "/api/my-service/v1"),
   ```
   The attribute name (snake_case) must match the property name you will add to `OsduClient`.

4. **Expose the typed property** in `src/OsduCsharpClient/Facade/OsduClient.cs`:
   - Add a `using` for the generated namespace (e.g. `using Equinor.OsduCsharpClient.MyService;`)
   - Add a backing field: `private MyServiceClient? _myService;`
   - Add a public property: `public MyServiceClient MyService => _myService ??= Build(ref _myService, "my_service");`

5. **Update the README** services table in `README.md`.

6. **Update the unit tests** — the service count assertions in
   `tests/OsduCsharpClient.Tests/ServiceRegistryTests.cs` and
   `tests/OsduCsharpClient.Tests/OsduClientTests.cs` will fail until updated.

## Project Structure

```txt
openapi_specs/                              OpenAPI specs (.json / .yaml / .yml)
src/
    OsduCsharpClient/
        Generated/                          Generated C# clients — gitignored, re-run generate_all.py
            <ServiceName>/                  One subfolder per service (e.g. Search/, Storage/)
        Facade/
            Auth/                           ITokenProvider + MSAL implementations
            DataPartitionHandler.cs         DelegatingHandler for data-partition-id injection
            LoggingHandler.cs               DelegatingHandler for HTTP request/response logging
            OsduClient.cs                   High-level facade with typed per-service properties
            OsduConfig.cs                   Configuration record (FromConfiguration binder)
            OsduException.cs                Typed exception for auth/config/API errors
            ServiceRegistry.cs              Static service → endpoint mapping
tests/
    OsduCsharpClient.IntegrationTests/      xUnit integration tests (require live OSDU server)
    OsduCsharpClient.Tests/                 xUnit unit tests (no network required)
download.py                                 Downloads specs from the OSDU wiki
fix_openapi_json_response_media_types.py    Normalizes */* response media types
generate_all.py                             Regenerates all C# clients via Kiota
```
