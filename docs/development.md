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

Every file in `openapi_specs/` is a copy of a spec its service publishes, and [`spec_sources.yaml`](../spec_sources.yaml) records where each one came from and the sha256 of both sides at the last check.

### Why upstream git, and not a deployed service

Each OSDU service already runs `cimpl-check-openapi-spec` in its own pipeline, which fetches the spec from the deployed service and fails that pipeline when the committed `docs/api/community/v{N}/openapi.yaml` no longer matches. The published file is therefore already known to match a running service. Comparing against it inherits that guarantee and needs no OSDU environment, no deployment, and no credentials — the URLs serve anonymously.

### Refreshing

```bash
python verify_spec_sources.py            # offline: manifest vs local files
python verify_spec_sources.py --fetch    # also compare against upstream
python verify_spec_sources.py --refresh  # overwrite the specs from upstream, then re-record
python verify_spec_sources.py --update   # re-record only
```

`--refresh` is the one you usually want. `--update` alone only re-records hashes, which turns a real divergence into a recorded one. Regenerate and run the tests after refreshing — an upstream change can add, remove or reshape operations on the generated client. Update one service at a time when the diff is large.

### Never hand-edit a vendored spec

An undeclared manual edit makes the drift check red forever, and the noise trains everyone to ignore it. Corrections belong in `generate_all.py`'s patch step, which is applied in memory so the file on disk stays a faithful copy of what the service publishes — see [Regenerating Clients](#regenerating-clients).

### The gate

| Workflow | When | Blocking |
|---|---|---|
| `run-tests.yml` → *Check spec provenance* | every PR | yes |
| `spec-drift.yml` → *check* | PRs touching `openapi_specs/`, `spec_sources.yaml`, `verify_spec_sources.py` | yes |
| `spec-drift.yml` → *report* | Mondays 06:00 UTC, or manually | no |

The offline check runs on every PR and needs no network. The upstream comparison is scoped to PRs that touch the specs on purpose: upstream services merge spec changes on their own cadence, and a gate that ran on every PR would turn this repo red for a change no author here could fix.

The scheduled half refreshes every spec and opens a PR when anything moved. Note that pull requests opened with the default `GITHUB_TOKEN` do not trigger further workflow runs, so that PR arrives without a CI run — close and reopen it, or push an empty commit, to get one.

### Every spec matches a published file

`spec_sources.yaml` carries no `differs` entry. It is worth keeping it that way:
`differs` exists so a drift check can tell a known divergence from a new one, but
a spec that cannot be matched to a published file has no provenance to check
against.

Three services publish one spec per API version rather than a combined one, so
this repo tracks the latest of each — CRS Catalog v3, CRS Conversion v4, Unit v2
and v3. Operations from older versions are not in the client; the services may
still serve them, but upstream no longer describes them. `convertBinGrid` is the
one with no successor, having existed only at CRS Conversion v3.

Two services were dropped rather than carried. Seismic DDMS's upstream spec
`$ref`s sibling files that only exist in the upstream repository and leaves
literal tabs on blank lines, so the published file is neither valid YAML nor
usable standalone. Geospatial's spec is the GCZ Transformer's administrative
API -- all 27 paths sit under `/admin` -- and upstream has not configured it:
the title is springdoc's `OpenAPI definition` placeholder, the version is `v0`,
and `servers` is an absolute dev-sandbox URL, so its route could only be
inferred from upstream's CI config. Neither is in osdu-python-client either.

### Endpoints must match the spec's own `servers`

The generated clients append each spec path verbatim to the base URL, so
`ServiceSpec.DefaultEndpoint` has to be exactly what the spec was written
against. Where a spec declares `servers: /api/file` and paths like
`/v2/files/uploadURL`, the version belongs to the path — registering
`/api/file/v2` produces `/api/file/v2/v2/files/uploadURL` and makes every
operation on that service unreachable. That was the state of `file`, `workflow`,
`unit` and both CRS services until it was corrected.

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

1. **Add the OpenAPI spec** to `openapi_specs/` (`.json`, `.yaml`, or `.yml`), then record where it came from in [`spec_sources.yaml`](../spec_sources.yaml) and run `python verify_spec_sources.py --update`. A spec with no recorded provenance fails the *Check spec provenance* step on every PR.

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
generate_all.py                             Regenerates all C# clients via Kiota
```
