# Environment and Tests

## Configuration Setup

Integration tests use standard .NET configuration (`Microsoft.Extensions.Configuration`).
Settings are read, in increasing order of precedence, from:

1. `appsettings.json` (committed template, empty values)
2. `appsettings.local.json` (gitignored — put your real values here)
3. user secrets (`dotnet user-secrets`, id `osdu-csharp-client-integration-tests`)
4. environment variables (`Osdu__*`)

Provide the required values for your OSDU environment in `appsettings.local.json`:

```json
{
  "Osdu": {
    "Server": "https://your-osdu-instance.com",
    "DataPartitionId": "your-partition-id",
    "Authority": "https://login.microsoftonline.com/<tenant-id>",
    "ClientId": "<public-client-id>",
    "Scopes": "api://<app-id-uri>/.default"
  }
}
```

…or via environment variables (double-underscore = section nesting):

```sh
export Osdu__Server=https://your-osdu-instance.com
export Osdu__DataPartitionId=your-partition-id
export Osdu__Authority=https://login.microsoftonline.com/<tenant-id>
export Osdu__ClientId=<public-client-id>
export Osdu__Scopes=api://<app-id-uri>/.default
```

…or via user secrets:

```sh
cd tests/OsduCsharpClient.IntegrationTests
dotnet user-secrets set "Osdu:Server" "https://your-osdu-instance.com"
# …etc
```

Optional environment variables used by tests:

- `OSDU_MSAL_CACHE_PATH` — path to a persistent MSAL token cache file (default: `~/.osdu/msal_cache.bin`)
- `SEARCH_KIND` - kind filter for search tests (default: `osdu:wks:work-product-component--WellLog:*`)
- `SEARCH_QUERY` - query string for search tests (default: `*`)
- `SEARCH_LIMIT` - result limit for search tests (default: `5`)
- `GROUP_TYPE` - group type filter for entitlements tests (default: `NONE`)
- `WELLBORE_DDMS_WELLBORE_ID` - runs `GetWellbore_ById_ReturnsRecord` (also the parent wellbore for `PostWellLog_WithJsonData_CreatesAndRoundTripsRecord`)
- `WELLBORE_DDMS_WELL_ID` - runs `GetWell_ById_ReturnsRecord`
- `WELLBORE_DDMS_WELLLOG_ID` - runs `GetWellLog_ById_ExposesDataAsJson`
- `WELLBORE_DDMS_LEGAL_TAG`, `WELLBORE_DDMS_ACL_OWNER`, `WELLBORE_DDMS_ACL_VIEWER` - together with `WELLBORE_DDMS_WELLBORE_ID`, run `PostWellLog_WithJsonData_CreatesAndRoundTripsRecord` (ingests a WellLog, verifies it, then deletes it)

## Running Tests

Integration tests hit a real OSDU server. On first run a browser window will open for interactive MSAL login; the resulting token is cached in `.msal_token_cache.bin`. Tests are skipped automatically when their required settings are absent.

```sh
# Run all integration tests
dotnet test OsduCsharpClient.slnx

# Run a single test by name
dotnet test OsduCsharpClient.slnx --filter "FullyQualifiedName~QueryRecords_ReturnsResults"

# Run tests and see printed output
dotnet test OsduCsharpClient.slnx --logger "console;verbosity=detailed"

# Run only the WellLog tests with full SDK request/response logging
dotnet test tests/OsduCsharpClient.IntegrationTests/OsduCsharpClient.IntegrationTests.csproj \
  --filter "FullyQualifiedName~WellLog" \
  --logger "console;verbosity=detailed"
```

## Logging

`OsduFixture` builds the `OsduClient` with a logger factory that routes SDK
logs to the running test's xUnit output. With `--logger "console;verbosity=detailed"`,
HTTP request/response lines (and truncated bodies, sensitive headers redacted)
appear under the test that produced them. See the log categories in
[docs/usage.md](usage.md#logging).

Body logging is enabled by default in the test fixture so that a failing test
shows the full request/response. Set `OSDU_TEST_LOG_BODIES=false` to silence
the `Equinor.OsduCsharpClient.Body` category for a quieter run; the summary
`→ METHOD URL` / `← STATUS URL` lines still print.
