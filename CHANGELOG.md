# Changelog

## [2.1.0](https://github.com/equinor/osdu-csharp-client/compare/v2.0.1...v2.1.0) (2026-09-04)


### Features

* **msal:** let the caller choose which cached account to use ([#112](https://github.com/equinor/osdu-csharp-client/issues/112)) ([2b22ae5](https://github.com/equinor/osdu-csharp-client/commit/2b22ae5744aa8fc086f95bce09cbc6ee8e9c5fb8))

## [2.0.1](https://github.com/equinor/osdu-csharp-client/compare/v2.0.0...v2.0.1) (2026-09-01)


### Dependencies

* bump the github-actions group with 3 updates ([#109](https://github.com/equinor/osdu-csharp-client/issues/109)) ([020a8be](https://github.com/equinor/osdu-csharp-client/commit/020a8bec2ea67ece47312950936c7075ace26992))
* Bump the nuget group with 2 updates ([#110](https://github.com/equinor/osdu-csharp-client/issues/110)) ([e967dfb](https://github.com/equinor/osdu-csharp-client/commit/e967dfb4d379a85ba095643b78a43f94a193a034))

## [2.0.0](https://github.com/equinor/osdu-csharp-client/compare/v1.1.9...v2.0.0) (2026-08-27)


### ⚠ BREAKING CHANGES

* extract MSAL auth into optional Equinor.OsduCsharpClient.Msal package ([#105](https://github.com/equinor/osdu-csharp-client/issues/105))

### Features

* extract MSAL auth into optional Equinor.OsduCsharpClient.Msal package ([#105](https://github.com/equinor/osdu-csharp-client/issues/105)) ([6bddb51](https://github.com/equinor/osdu-csharp-client/commit/6bddb51dfd5efc136e3f3f60536b30db09616cbd))

## [1.1.9](https://github.com/equinor/osdu-csharp-client/compare/v1.1.8...v1.1.9) (2026-08-26)


### Bug Fixes

* untype string-typed JSON responses so record reads return data ([#103](https://github.com/equinor/osdu-csharp-client/issues/103)) ([e07b0a4](https://github.com/equinor/osdu-csharp-client/commit/e07b0a4a3181e72667ccf8f891cdb78e345dad60))

## [1.1.8](https://github.com/equinor/osdu-csharp-client/compare/v1.1.7...v1.1.8) (2026-08-25)


### Bug Fixes

* send a content type on bodiless requests so Storage query endpoints work ([#101](https://github.com/equinor/osdu-csharp-client/issues/101)) ([28b751c](https://github.com/equinor/osdu-csharp-client/commit/28b751ce100a7b183ca6004513290c26c20b33c6))

## [1.1.7](https://github.com/equinor/osdu-csharp-client/compare/v1.1.6...v1.1.7) (2026-08-25)


### Bug Fixes

* encrypt the persisted MSAL token cache and purge plaintext ones ([#98](https://github.com/equinor/osdu-csharp-client/issues/98)) ([4954de4](https://github.com/equinor/osdu-csharp-client/commit/4954de4156c3ab9bd87d47898c2bf80c731858db))

## [1.1.6](https://github.com/equinor/osdu-csharp-client/compare/v1.1.5...v1.1.6) (2026-08-24)


### Dependencies

* Bump the nuget group with 10 updates ([#94](https://github.com/equinor/osdu-csharp-client/issues/94)) ([ad1eb27](https://github.com/equinor/osdu-csharp-client/commit/ad1eb2700364f6e0068e1507ee0ebade37b2473d))

## [1.1.5](https://github.com/equinor/osdu-csharp-client/compare/v1.1.4...v1.1.5) (2026-08-24)


### Bug Fixes

* retire pooled connections by age so DNS changes are picked up ([#90](https://github.com/equinor/osdu-csharp-client/issues/90)) ([8c7bd8c](https://github.com/equinor/osdu-csharp-client/commit/8c7bd8ce869630185593b0d6478d05de9817c8bc))


### Dependencies

* bump the github-actions group with 3 updates ([#93](https://github.com/equinor/osdu-csharp-client/issues/93)) ([5dffff6](https://github.com/equinor/osdu-csharp-client/commit/5dffff625658a981a8d6e875391457a900707ddd))

## [1.1.4](https://github.com/equinor/osdu-csharp-client/compare/v1.1.3...v1.1.4) (2026-08-20)


### Bug Fixes

* make OsduClient service-client initialisation thread-safe ([#87](https://github.com/equinor/osdu-csharp-client/issues/87)) ([6bf2814](https://github.com/equinor/osdu-csharp-client/commit/6bf2814554ff21afc7820e50cc2e3bb9e933623e))

## [1.1.3](https://github.com/equinor/osdu-csharp-client/compare/v1.1.2...v1.1.3) (2026-08-18)


### Dependencies

* bump the github-actions group with 3 updates ([#81](https://github.com/equinor/osdu-csharp-client/issues/81)) ([5fb0736](https://github.com/equinor/osdu-csharp-client/commit/5fb07367157452edcb6793c6863296867d12e82d))

## [1.1.2](https://github.com/equinor/osdu-csharp-client/compare/v1.1.1...v1.1.2) (2026-08-13)


### Dependencies

* bump the github-actions group across 1 directory with 6 updates ([#74](https://github.com/equinor/osdu-csharp-client/issues/74)) ([ceb5bc5](https://github.com/equinor/osdu-csharp-client/commit/ceb5bc5e1a4a18d446593b8693a91a1c955e70a2))
* bump the github-actions group with 6 updates ([#78](https://github.com/equinor/osdu-csharp-client/issues/78)) ([1fe5add](https://github.com/equinor/osdu-csharp-client/commit/1fe5adda2f1e0a89b6ee1e9a784025ebe755ef6f))
* Bump the nuget group with 9 updates ([#80](https://github.com/equinor/osdu-csharp-client/issues/80)) ([f6e77a2](https://github.com/equinor/osdu-csharp-client/commit/f6e77a2fabfdef34f6db539c65197138e0fd9352))

## [1.1.1](https://github.com/equinor/osdu-csharp-client/compare/v1.1.0...v1.1.1) (2026-06-23)


### Dependencies

* bump `Microsoft.Identity.Client` from 4.84.1 to 4.84.2 ([#58](https://github.com/equinor/osdu-csharp-client/issues/58)) ([2cf22f3](https://github.com/equinor/osdu-csharp-client/commit/2cf22f39170ab9f651b32327aa5068145254cbde))
* bump the github-actions group with 4 updates ([#57](https://github.com/equinor/osdu-csharp-client/issues/57)) ([d752ddb](https://github.com/equinor/osdu-csharp-client/commit/d752ddb2cd52ee0d29127c2e10dbd65e87021157))
* Bump the nuget group with 6 updates ([#71](https://github.com/equinor/osdu-csharp-client/issues/71)) ([329c021](https://github.com/equinor/osdu-csharp-client/commit/329c021406ed67570689f4c9c664ca98df1294c4))

## [1.1.0](https://github.com/equinor/osdu-csharp-client/compare/v1.0.0...v1.1.0) (2026-06-11)


### Features

* add Wellbore DDMS Parquet bulk data support ([#54](https://github.com/equinor/osdu-csharp-client/issues/54)) ([b6e5fd0](https://github.com/equinor/osdu-csharp-client/commit/b6e5fd01327efca570c566335b623e731b2088a5))

## [1.0.0](https://github.com/equinor/osdu-csharp-client/compare/v0.5.2...v1.0.0) (2026-06-10)


### ⚠ BREAKING CHANGES

* bind OsduConfig from IConfiguration instead of environment variables ([#49](https://github.com/equinor/osdu-csharp-client/issues/49))

### Features

* bind OsduConfig from IConfiguration instead of environment variables ([#49](https://github.com/equinor/osdu-csharp-client/issues/49)) ([bc2115c](https://github.com/equinor/osdu-csharp-client/commit/bc2115c686910bf3306aad2f06139f2fad38a915))


### Dependencies

* bump `equinor/ops-actions` from 9.38.0 to 9.38.1 and `actions/setup-dotnet` from 5.2.0 to 5.3.0 ([#50](https://github.com/equinor/osdu-csharp-client/issues/50)) ([e679484](https://github.com/equinor/osdu-csharp-client/commit/e6794844817b97608d338242ec13096a9c63f8d0))
* bump `Microsoft.NET.Test.Sdk` from 18.5.1 to 18.6.0 ([#51](https://github.com/equinor/osdu-csharp-client/issues/51)) ([4a282dd](https://github.com/equinor/osdu-csharp-client/commit/4a282ddafda47d93707af3b75245a81cf9442bad))

## [0.5.2](https://github.com/equinor/osdu-csharp-client/compare/v0.5.1...v0.5.2) (2026-06-04)


### Dependencies

* bump all Microsoft.Kiota packages from 1.22.1 to 2.0.0 ([#47](https://github.com/equinor/osdu-csharp-client/issues/47)) ([dd16403](https://github.com/equinor/osdu-csharp-client/commit/dd16403eb4db4fc38f5e3bcd5c64dc419b4ac4a2))

## [0.5.1](https://github.com/equinor/osdu-csharp-client/compare/v0.5.0...v0.5.1) (2026-06-03)


### Bug Fixes

* install Python dependencies before generating clients in release workflow ([#44](https://github.com/equinor/osdu-csharp-client/issues/44)) ([5bd7112](https://github.com/equinor/osdu-csharp-client/commit/5bd7112f2481f876b5522306dcf4155eac4866ef))

## [0.5.0](https://github.com/equinor/osdu-csharp-client/compare/v0.4.2...v0.5.0) (2026-06-02)


### Features

* add OsduClient facade, MSAL auth providers, and YAML spec support ([#31](https://github.com/equinor/osdu-csharp-client/issues/31)) ([731ba81](https://github.com/equinor/osdu-csharp-client/commit/731ba81170913d8ca30836c7384b70b7e8033b7c))
* expose OSDU Record.data as free-form JSON for Storage, Dataset and Wellbore DDMS ([#39](https://github.com/equinor/osdu-csharp-client/issues/39)) ([e58907c](https://github.com/equinor/osdu-csharp-client/commit/e58907c9f61a15ff813409024b7650fd4c146c47))


### Dependencies

* bump `DotNetEnv` from 3.1.1 to 3.2.0 ([#29](https://github.com/equinor/osdu-csharp-client/issues/29)) ([8f3463f](https://github.com/equinor/osdu-csharp-client/commit/8f3463f01e64b7a5bb051b0d3cb967e04f8b3d56))
* bump `equinor/ops-actions` from 9.37.2 to 9.37.3 ([#28](https://github.com/equinor/osdu-csharp-client/issues/28)) ([84965be](https://github.com/equinor/osdu-csharp-client/commit/84965be5396de51af36b50fc66543f7e348b7480))
* bump `equinor/ops-actions` from 9.37.3 to 9.38.0 ([#41](https://github.com/equinor/osdu-csharp-client/issues/41)) ([7dc1435](https://github.com/equinor/osdu-csharp-client/commit/7dc1435ab1c67e4389672ce07f9fa063cb2ff8e8))
* bump `Microsoft.Identity.Client` from 4.83.3 to 4.84.0 ([#32](https://github.com/equinor/osdu-csharp-client/issues/32)) ([e26054d](https://github.com/equinor/osdu-csharp-client/commit/e26054d807433bf0500020b798c55b7bc8af75b7))
* bump `Microsoft.Identity.Client` from 4.83.3 to 4.84.0 ([#42](https://github.com/equinor/osdu-csharp-client/issues/42)) ([dd15633](https://github.com/equinor/osdu-csharp-client/commit/dd15633e1243bd228a37acc0b4e1bbf622ffe3fa))
* bump `Microsoft.Identity.Client` from 4.84.0 to 4.84.1 ([#43](https://github.com/equinor/osdu-csharp-client/issues/43)) ([956495e](https://github.com/equinor/osdu-csharp-client/commit/956495ee731171615bba7d089266ae2f48dcc868))
* bump `Microsoft.NET.Test.Sdk` from 18.4.0 to 18.5.1 ([#30](https://github.com/equinor/osdu-csharp-client/issues/30)) ([3885b99](https://github.com/equinor/osdu-csharp-client/commit/3885b99cdbb2ef025d6ad30d86b7e25ad538aa12))
* bump the github-actions group with 3 updates ([#26](https://github.com/equinor/osdu-csharp-client/issues/26)) ([fa6f1e7](https://github.com/equinor/osdu-csharp-client/commit/fa6f1e73fdfdf53f8bd09fd8e155ba695e1c9fbb))

## [0.4.2](https://github.com/equinor/osdu-csharp-client/compare/v0.4.1...v0.4.2) (2026-04-27)


### Dependencies

* bump the github-actions group with 3 updates ([#24](https://github.com/equinor/osdu-csharp-client/issues/24)) ([ff65c43](https://github.com/equinor/osdu-csharp-client/commit/ff65c4338de15fa5a775b33302f374ae1802cf2c))

## [0.4.1](https://github.com/equinor/osdu-csharp-client/compare/v0.4.0...v0.4.1) (2026-04-20)


### Dependencies

* bump `equinor/ops-actions/.github/workflows/commitlint.yml` from 9.35.1 to 9.36.0 ([bd19fe7](https://github.com/equinor/osdu-csharp-client/commit/bd19fe72bacb28ff91518064d7f913077b277071))
* bump `equinor/ops-actions/.github/workflows/release-please-manifest.yml` from 9.35.1 to 9.36.0 ([bd19fe7](https://github.com/equinor/osdu-csharp-client/commit/bd19fe72bacb28ff91518064d7f913077b277071))
* bump `equinor/ops-actions/.github/workflows/zizmor-codeql.yml` from 9.35.1 to 9.36.0 ([bd19fe7](https://github.com/equinor/osdu-csharp-client/commit/bd19fe72bacb28ff91518064d7f913077b277071))
* bump `Microsoft.Identity.Client` from 4.83.1 to 4.83.3 ([#20](https://github.com/equinor/osdu-csharp-client/issues/20)) ([de6326a](https://github.com/equinor/osdu-csharp-client/commit/de6326a58f2ab2c233a2f87fb7bf5c3a38ba1a32))
* bump `Microsoft.NET.Test.Sdk` from 18.3.0 to 18.4.0 ([#23](https://github.com/equinor/osdu-csharp-client/issues/23)) ([7d78fd5](https://github.com/equinor/osdu-csharp-client/commit/7d78fd5ca18cc1fa2d2b32ce8879e405fb56f481))
* bump the github-actions group with 3 updates ([#22](https://github.com/equinor/osdu-csharp-client/issues/22)) ([bd19fe7](https://github.com/equinor/osdu-csharp-client/commit/bd19fe72bacb28ff91518064d7f913077b277071))

## [0.4.0](https://github.com/equinor/osdu-csharp-client/compare/v0.3.0...v0.4.0) (2026-03-31)


### Features

* update search specifications to latest version ([#12](https://github.com/equinor/osdu-csharp-client/issues/12)) ([3fea81b](https://github.com/equinor/osdu-csharp-client/commit/3fea81bee6e16bfbf49a6e1556ca731040b4dd72))


### Dependencies

* update equinor/ops-actions from 9.35.0 to 9.35.1 ([#13](https://github.com/equinor/osdu-csharp-client/issues/13)) ([b0bbd5a](https://github.com/equinor/osdu-csharp-client/commit/b0bbd5ac90749d4972d89160b78d2be9722e861b))
* update Microsoft.Kiota.Abstractions from 1.22.0 to 1.22.1 and Microsoft.Kiota.Serialization.Text from 1.22.0 to 1.22.1 ([#19](https://github.com/equinor/osdu-csharp-client/issues/19)) ([5904dd4](https://github.com/equinor/osdu-csharp-client/commit/5904dd4493c1f022c5466cfb2c8ec6083f0fa272))
* update Microsoft.Kiota.Http.HttpClientLibrary from 1.22.0 to 1.22.1 ([#15](https://github.com/equinor/osdu-csharp-client/issues/15)) ([ad5599a](https://github.com/equinor/osdu-csharp-client/commit/ad5599a5c28f6b537999d364dfdce381081df2c7))
* update Microsoft.Kiota.Serialization.Form from 1.22.0 to 1.22.1 ([#16](https://github.com/equinor/osdu-csharp-client/issues/16)) ([66f2726](https://github.com/equinor/osdu-csharp-client/commit/66f272690b97d98a12d13a4b95836341ba767c12))
* update Microsoft.Kiota.Serialization.Json from 1.22.0 to 1.22.1 ([#17](https://github.com/equinor/osdu-csharp-client/issues/17)) ([e6c7113](https://github.com/equinor/osdu-csharp-client/commit/e6c7113239f1d944a432c7f4dfd54a26bbb33910))
* update Microsoft.Kiota.Serialization.Multipart from 1.22.0 to 1.22.1 ([#18](https://github.com/equinor/osdu-csharp-client/issues/18)) ([b92b95a](https://github.com/equinor/osdu-csharp-client/commit/b92b95a5649691bd94dedac9f10e02cb0da930a1))
* updates `requests` from 2.32.5 to 2.33.0 ([#10](https://github.com/equinor/osdu-csharp-client/issues/10)) ([68aa7bf](https://github.com/equinor/osdu-csharp-client/commit/68aa7bf790e1c0983443f969986953877eb117aa))

## [0.3.0](https://github.com/equinor/osdu-csharp-client/compare/v0.2.0...v0.3.0) (2026-03-24)


### Features

* explicit dotnet version ([#7](https://github.com/equinor/osdu-csharp-client/issues/7)) ([110120f](https://github.com/equinor/osdu-csharp-client/commit/110120fc20ab75ec915b51a90a367346c13850bc))

## 0.2.0 (2026-03-17)


### Bug Fixes

* xUnit1051 warnings: use TestContext.Current.CancellationToken

## 0.1.2 (2026-03-14)


### Bug Fixes

* Fix Wellbore DDMS integer fields to int64

## 0.1.1 (2026-03-14)
