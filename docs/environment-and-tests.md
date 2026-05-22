# Environment and Tests

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

Integration tests hit a real OSDU server. On first run a browser window will open for interactive MSAL login; the resulting token is cached in `.msal_token_cache.bin`.

```sh
# Run all integration tests
dotnet test OsduCsharpClient.slnx

# Run a single test by name
dotnet test OsduCsharpClient.slnx --filter "FullyQualifiedName~QueryRecords_ReturnsResults"

# Run tests and see printed output
dotnet test OsduCsharpClient.slnx --logger "console;verbosity=detailed"
```
