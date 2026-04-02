# Changelog

## [3.0.2](https://github.com/bc-solutions-coder/wallow/compare/v3.0.1...v3.0.2) (2026-04-02)


### Bug Fixes

* **identity:** disable OpenIddict transport security for container-to-container HTTP ([d8f0240](https://github.com/bc-solutions-coder/wallow/commit/d8f024027d9e45f3e18704b1262537b33e516828))
* **web:** add X-Forwarded-Proto to OIDC backchannel for container-to-container calls ([82e57a9](https://github.com/bc-solutions-coder/wallow/commit/82e57a90813fa09ecdc9e57902763646efb77822))

## [3.0.1](https://github.com/bc-solutions-coder/wallow/compare/v3.0.0...v3.0.1) (2026-04-02)


### Bug Fixes

* **auth:** fix /auth redirect downloading file instead of navigating ([4fc8598](https://github.com/bc-solutions-coder/wallow/commit/4fc859809dca719b76d418ff20944ac36fb97f8f))
* **web:** add data-enhance-nav=false to Get Started login links ([1f306ee](https://github.com/bc-solutions-coder/wallow/commit/1f306ee80244d84d32b88520c481592867fbdb74))

## [3.0.0](https://github.com/bc-solutions-coder/wallow/compare/v2.0.0...v3.0.0) (2026-04-01)


### ⚠ BREAKING CHANGES

* remove api/ route prefix from controllers, let UsePathBase handle it
* integrate .NET Aspire for dev orchestration, service defaults, and migration extraction
* remove messaging module to tighten initial release scope

### Features

* integrate .NET Aspire for dev orchestration, service defaults, and migration extraction ([56e3327](https://github.com/bc-solutions-coder/wallow/commit/56e3327bc677593ffca9790c3dbbac56162df4b9))
* remove messaging module to tighten initial release scope ([40e2206](https://github.com/bc-solutions-coder/wallow/commit/40e2206ab22dfd9a04d2ff5898236a41bb5b8dac))


### Bug Fixes

* **ci:** add missing -migrations image to publish workflow ([0db1ef7](https://github.com/bc-solutions-coder/wallow/commit/0db1ef7e6559101034d2536db7d00c261f07e598))
* **ci:** only push :latest docker tag on release, not every CI run ([7647459](https://github.com/bc-solutions-coder/wallow/commit/76474592013311e4231e931b4170f7be7b3af9bb))
* **docker:** add OTEL wiring, OIDC metadata, and VERSION to production stack ([ced47bd](https://github.com/bc-solutions-coder/wallow/commit/ced47bd6985ff539b6b611d2d4107fb902fae22e))
* **docs:** update Dockerfile theme stage for docfx/ template path ([0d282df](https://github.com/bc-solutions-coder/wallow/commit/0d282df3d522686f7ad068486cdf0e5c13c45c11))
* **docs:** update template and filter paths after docfx/ directory move ([86bab3c](https://github.com/bc-solutions-coder/wallow/commit/86bab3cc5aade6fea95e777f969feb6d99340c11))
* update E2E tests, docker compose, and middleware for api route prefix removal ([00fb8e7](https://github.com/bc-solutions-coder/wallow/commit/00fb8e75e928ac7bb9016b1a7d3bc27f7d0bbd36))
* **web:** allow HTTP metadata endpoint for container-to-container OIDC discovery ([bdc85fc](https://github.com/bc-solutions-coder/wallow/commit/bdc85fc1ac4a14a40bcee1d31d4385ea5c0995a3))


### Performance Improvements

* **ci:** add incremental build cache and graph build for faster CI ([edd4440](https://github.com/bc-solutions-coder/wallow/commit/edd444073a6540cb26ddd2f0ad811d556fa37117))
* **ci:** add NuGet cache and graph build to CodeQL workflow ([57b91f8](https://github.com/bc-solutions-coder/wallow/commit/57b91f8482947d133e3dd1954b7dc278c03ebc54))
* **ci:** remove CodeQL PR trigger, runs on main push and weekly ([8292bf5](https://github.com/bc-solutions-coder/wallow/commit/8292bf5dcf50a6b3f0282724403355c8dff95cbb))
* **ci:** run CodeQL on PRs and weekly schedule, not on main push ([7e73b3f](https://github.com/bc-solutions-coder/wallow/commit/7e73b3f7dadf121ff050027913a91e0deab61b2d))
* **ci:** skip CI for release-please commits ([2f38b66](https://github.com/bc-solutions-coder/wallow/commit/2f38b6604b6a38bd24d8f4de5763e174a1e7619a))
* **ci:** skip CI on release-please PR branches ([49a50a1](https://github.com/bc-solutions-coder/wallow/commit/49a50a10850a9e59e6a4f3bb0c74f8b5ca95d5b8))
* **ci:** skip CodeQL on release-please PR branches ([c1be299](https://github.com/bc-solutions-coder/wallow/commit/c1be299b3bb916d89a4f081097d3ee900f981759))
* **ci:** split CI and deploy, eliminate redundant workflow runs ([094ed0c](https://github.com/bc-solutions-coder/wallow/commit/094ed0ce514284f883431cd8b9057b2bd98f166c))


### Reverts

* **ci:** restore :latest tag push from CI pipeline ([dd652a5](https://github.com/bc-solutions-coder/wallow/commit/dd652a51d4aabb1569b5bf00bd88a6d6d345ef1d))


### Code Refactoring

* remove api/ route prefix from controllers, let UsePathBase handle it ([fcb224e](https://github.com/bc-solutions-coder/wallow/commit/fcb224e0c8bcb5135a3f19b7e1156e9fd3bbc55f))

## [2.0.0](https://github.com/bc-solutions-coder/wallow/compare/v1.4.1...v2.0.0) (2026-04-01)


### ⚠ BREAKING CHANGES

* billing, metering, invoices, payments, and subscription management APIs and event contracts have been removed from the platform

### Features

* remove billing module to tighten initial release scope ([09d176c](https://github.com/bc-solutions-coder/wallow/commit/09d176cc7ce9c7bf4f044699f17ae633ada4be6b))


### Bug Fixes

* **identity:** return 401 instead of redirecting to /Account/Login ([2eb5f95](https://github.com/bc-solutions-coder/wallow/commit/2eb5f95f6c1807e641cd9c9d8b8544c33590fd5f))

## [1.4.1](https://github.com/bc-solutions-coder/wallow/compare/v1.4.0...v1.4.1) (2026-03-31)


### Bug Fixes

* **docker:** add PathBase env vars and route prefix convention for path-based routing ([34e4c6a](https://github.com/bc-solutions-coder/wallow/commit/34e4c6a182e65ef0e0ab69f05da34f8aa1491675))

## [1.4.0](https://github.com/bc-solutions-coder/wallow/compare/v1.3.0...v1.4.0) (2026-03-31)


### Features

* **e2e:** add comprehensive auth E2E tests ([fb53ac4](https://github.com/bc-solutions-coder/wallow/commit/fb53ac44cf1f45e878860dda8bacc2ab8aff6c27))
* **identity:** add PathBase support, OAuth consent page, and BFF documentation ([4419a4a](https://github.com/bc-solutions-coder/wallow/commit/4419a4afaeba8d2ce0740bb41c09427e23a92004))


### Bug Fixes

* **auth:** split ApiBaseUrl into public and internal URLs ([995055e](https://github.com/bc-solutions-coder/wallow/commit/995055ebaa93d6dcc753e7de9a2de62268571870))
* **web:** move data-testid into InputFile AdditionalAttributes dictionary ([fec641e](https://github.com/bc-solutions-coder/wallow/commit/fec641eeb5c8cd63d1d307fe058d4761608e80d9))

## [1.3.0](https://github.com/bc-solutions-coder/wallow/compare/v1.2.3...v1.3.0) (2026-03-30)


### Features

* **auth:** replace custom tailwind MSBuild targets with Tailwind.Hosting packages ([1fccef8](https://github.com/bc-solutions-coder/wallow/commit/1fccef8da7d10faa851efe8204fbb110d58ecc2e))
* replace Dockerfiles with SDK-native container publishing ([05d39a1](https://github.com/bc-solutions-coder/wallow/commit/05d39a115abd18b378456173913f74f49ba6c8cc))
* **web:** replace custom tailwind MSBuild targets with Tailwind.Hosting packages ([075bd32](https://github.com/bc-solutions-coder/wallow/commit/075bd32ec91436d04d04ec3264497af7a94a0355))


### Bug Fixes

* **auth:** raise auth rate limit and forward client IP for correct partitioning ([163ce07](https://github.com/bc-solutions-coder/wallow/commit/163ce075c1aa49180cc54ac513670a870d3fe65c))
* **ci:** add --no-build to arm64 API publish to prevent MSB3030 ([75b4a22](https://github.com/bc-solutions-coder/wallow/commit/75b4a22d6b76e828cbf116c6c909f136423d56f4))
* **ci:** use framework-dependent migration bundles on Linux to avoid cross-compile failure ([22607dc](https://github.com/bc-solutions-coder/wallow/commit/22607dc521c272555cba97cfe0167dbe0e3f0d81))
* clean inkscape metadata from SVG to fix GitHub rendering ([403ffad](https://github.com/bc-solutions-coder/wallow/commit/403ffade752a048d636bae1489a31815225112de))
* migration bundle cross-compilation on macOS ARM64 ([79755e8](https://github.com/bc-solutions-coder/wallow/commit/79755e8bc9a49f2cc6e6dcceea8924e556def96b))
* remove double-hyphens from XML comment in csproj ([d422f63](https://github.com/bc-solutions-coder/wallow/commit/d422f63da3f810c92de237b39f0a56b336927f1d))
* **web:** resolve OIDC correlation failure and broken sign-out link ([9083fe4](https://github.com/bc-solutions-coder/wallow/commit/9083fe4b00135b93e44ce0c18f8c883769040bdf))

## [1.2.3](https://github.com/bc-solutions-coder/wallow/compare/v1.2.2...v1.2.3) (2026-03-30)


### Bug Fixes

* **auth,web:** add forwarded headers middleware for reverse proxy support ([a199bca](https://github.com/bc-solutions-coder/wallow/commit/a199bca38f9a49cb622f8f7c6df173d0953002d7))
* **auth,web:** persist DataProtection keys to Valkey ([4408198](https://github.com/bc-solutions-coder/wallow/commit/44081981e9192c9abaf11e0ed37135c5745b135c))
* **ci:** build infra images (garage, postgres-replica) as multi-arch ([8025a49](https://github.com/bc-solutions-coder/wallow/commit/8025a4970c92ec0412563721392e95749abc54cc))
* wire Valkey connection to Auth and Web containers ([bdc78db](https://github.com/bc-solutions-coder/wallow/commit/bdc78db306752d29482a64324d76b123d774ad08))

## [1.2.2](https://github.com/bc-solutions-coder/wallow/compare/v1.2.1...v1.2.2) (2026-03-30)


### Bug Fixes

* add forwarded headers middleware for reverse proxy HTTPS support ([2fc1f9d](https://github.com/bc-solutions-coder/wallow/commit/2fc1f9dc051905fd6754b722e7e9e5ac39d70c9e))
* auto-download Tailwind binary and compile CSS during publish ([a640999](https://github.com/bc-solutions-coder/wallow/commit/a640999dde7b1a6a9169332fa73751d5558ff0b5))
* bust GitHub image cache for readme logo ([cc18c0f](https://github.com/bc-solutions-coder/wallow/commit/cc18c0ffd9ddf64d93424e3b6d49a2bfef19939b))
* **ci:** add no-build to arm64 publish to prevent stale bin directory errors ([02bbe40](https://github.com/bc-solutions-coder/wallow/commit/02bbe4064a29ca568911ed826a4f90daa7aee16e))
* **ci:** include Tailwind-compiled CSS in build cache ([5b850c3](https://github.com/bc-solutions-coder/wallow/commit/5b850c32c3285cc4ffc1dc49162a38dfce81c064))
* **ci:** restore arm64 runtime packages before cross-compiling migration bundles ([0b0769c](https://github.com/bc-solutions-coder/wallow/commit/0b0769cba2e92d5301d6299405a92c71515e58d8))
* remove invalid XML comment containing double hyphens in csproj ([4f61dc9](https://github.com/bc-solutions-coder/wallow/commit/4f61dc9ba7f94e79139b6def38c2591c6865aee7))
* set tmpfs uid/gid to match container app user (1654) ([7a4e406](https://github.com/bc-solutions-coder/wallow/commit/7a4e406a342c63c55d2fe772373aa0be145c578c))
* update release-please last-release-sha to v1.2.1 after history filter ([1c2323d](https://github.com/bc-solutions-coder/wallow/commit/1c2323d6b3b522796a49ed8bc23511374c7e08d7))
* use --no-build for native-arch migration bundles, --target-runtime for cross-compilation ([7819fd4](https://github.com/bc-solutions-coder/wallow/commit/7819fd4e8385038975882d35f85baabf26fed33f))
* use mode=1777 for tmpfs instead of hardcoded uid ([ea0ffec](https://github.com/bc-solutions-coder/wallow/commit/ea0ffecc60e08cb1dcb81087696ddb6af37439c3))

## [1.2.1](https://github.com/bc-solutions-coder/wallow/compare/v1.2.0...v1.2.1) (2026-03-30)


### Bug Fixes

* set DOTNET_BUNDLE_EXTRACT_BASE_DIR for migration bundles in read-only containers ([369a798](https://github.com/bc-solutions-coder/wallow/commit/369a798908aaf56e169c2110648d05b261b5cec5))

## [1.2.0](https://github.com/bc-solutions-coder/wallow/compare/v1.1.0...v1.2.0) (2026-03-29)


### Features

* self-contained API container with migrations, cert gen, and multi-arch support ([016c986](https://github.com/bc-solutions-coder/wallow/commit/016c98691295f1055a0d00cfc11890a30550b1aa))

## [1.1.0](https://github.com/bc-solutions-coder/wallow/compare/v1.0.3...v1.1.0) (2026-03-29)


### Features

* **identity:** implement auth security hardening ([539276e](https://github.com/bc-solutions-coder/wallow/commit/539276e93b6c77c35ff3d445ee75a95def067c4a))
* migrate app containers to dotnet publish /t:PublishContainer ([a82a0c3](https://github.com/bc-solutions-coder/wallow/commit/a82a0c347f981ccaaf81cc95382db53f29caebb6))
* unified docker production stack with custom images ([5142f9c](https://github.com/bc-solutions-coder/wallow/commit/5142f9c12517104e523df7cac3cfd916f19d1a22))


### Bug Fixes

* **ci:** correct garage service name in docker compose build ([e4db5fb](https://github.com/bc-solutions-coder/wallow/commit/e4db5fb4b8e5328176c0a13f71945f7aa633a420))
* **ci:** restore dotnet-ef tool before publishing container images ([ac0c764](https://github.com/bc-solutions-coder/wallow/commit/ac0c764aeeea5f9515880733ba2b25afe7da4112))
* **ci:** update docker image build to use dotnet publish /t:PublishContainer ([e509f61](https://github.com/bc-solutions-coder/wallow/commit/e509f619c2c843f9797b5b57500c84c342d63f71))
* **identity:** resolve scoped service DI violation in SessionActivityMiddleware ([f2db335](https://github.com/bc-solutions-coder/wallow/commit/f2db3359a994561c622194746b33edae51b8990d))
* reduce flakiness in AuditInterceptorTests by sharing Postgres container ([76ab6c6](https://github.com/bc-solutions-coder/wallow/commit/76ab6c6684eab6393f857884363a4678e9cc9f65))
* resolve 167 codebase audit findings across all modules ([#18](https://github.com/bc-solutions-coder/wallow/issues/18)) ([d9d3c98](https://github.com/bc-solutions-coder/wallow/commit/d9d3c98a01ca291dbc6a8267baf81d374101bb58))
* restore garage docker files accidentally deleted in 5142f9c1 ([633219c](https://github.com/bc-solutions-coder/wallow/commit/633219c5409408b53a9820b67af5d78d6aad867e))

## [1.0.3](https://github.com/bc-solutions-coder/wallow/compare/v1.0.2...v1.0.3) (2026-03-29)


### Bug Fixes

* add last-release-sha to release-please config after history rewrite ([72967a6](https://github.com/bc-solutions-coder/wallow/commit/72967a608b8c0b598881234ee614c0a40c606166))

## [1.0.2](https://github.com/bc-solutions-coder/wallow/compare/v1.0.1...v1.0.2) (2026-03-29)


### Bug Fixes

* align codeql config with merged resolution ([88b1a84](https://github.com/bc-solutions-coder/wallow/commit/88b1a849b57db26085d272fa9bfc620b93553b20))
* **deploy:** make dockhand stack self-contained and fix container failures ([c76e738](https://github.com/bc-solutions-coder/wallow/commit/c76e7386172a3afa9ccfe446e12e959440f3251e))
* **kernel:** add missing xml doc on ForbiddenAccessException ([ae3540c](https://github.com/bc-solutions-coder/wallow/commit/ae3540ced13fdf94140607afe66611881ffb8035))

## [1.0.1](https://github.com/bc-solutions-coder/wallow/compare/v1.0.0...v1.0.1) (2026-03-29)


### Bug Fixes

* **auth:** prevent open-redirect via ReturnUrl in Blazor pages ([#13](https://github.com/bc-solutions-coder/wallow/issues/13)) ([2515600](https://github.com/bc-solutions-coder/wallow/commit/25156003867c2df17e0d1599630c4d399c2bd671))

## [1.0.0](https://github.com/bc-solutions-coder/wallow/compare/v0.2.0...v1.0.0) (2026-03-29)


### ⚠ BREAKING CHANGES

* /api/v1/showcases endpoints removed, showcases.read and showcases.manage OAuth2 scopes no longer exist
* rename project from Foundry to Wallow
* retire Configuration module, migrate custom fields to Billing, complete decentralized settings
* implement security hardening from sweep 2 across all modules
* implement security hardening across all modules

### Features

* .NET 10 modernization - HybridCache, OTel upgrades, tenant rate limiting, FrozenDictionary ([02961bd](https://github.com/bc-solutions-coder/wallow/commit/02961bd2bebfc1456d72a47b47156cfd9c550b11))
* add architecture test auto-discovery and security scanning CI ([96a5a46](https://github.com/bc-solutions-coder/wallow/commit/96a5a4628325f5863eaff3ecdaee9458f7e850cc))
* add database migration init container with EF Core bundles ([d288be4](https://github.com/bc-solutions-coder/wallow/commit/d288be4da4a0ebfe55feef11ebec4cc395f694b5))
* add discord webhook notifications via claude code hooks ([4ef023c](https://github.com/bc-solutions-coder/wallow/commit/4ef023cb551b42c086bbf4a7b558b99294b26a0d))
* add EF Core compiled queries across all modules and benchmarks ([3177a16](https://github.com/bc-solutions-coder/wallow/commit/3177a164e478c13d8cf6f1a465ea0e781aa8aca3))
* add EF Core migrations for Identity, Storage, and Communications ([2611c4b](https://github.com/bc-solutions-coder/wallow/commit/2611c4bcc7da03cb6e192c0faa09aab0b420ff20))
* add notifications, inquiries, and billing enhancements ([7214e61](https://github.com/bc-solutions-coder/wallow/commit/7214e61917215ebc91015e4c95152207cfa81f77))
* add Polly resilience integration across all HTTP clients ([ca0c981](https://github.com/bc-solutions-coder/wallow/commit/ca0c98182c5b724b579865824e4f824a0655db2c))
* add TenantAwareDbContext base class and migrate all module DbContexts ([d375cf4](https://github.com/bc-solutions-coder/wallow/commit/d375cf4aae6be4faf3d67a7b976c9a3081f2ca67))
* add Wolverine tenant context propagation via ITenantContextSetter and stamping/restoring middlewares ([12d2ad4](https://github.com/bc-solutions-coder/wallow/commit/12d2ad48e56cf7135bbbbba40e8ba847b4c02771))
* **api:** add config-driven module toggles ([c1da632](https://github.com/bc-solutions-coder/wallow/commit/c1da6323b0c26a164f54e16a1dce885ad3c6acb3))
* **api:** add CSRF protection with antiforgery middleware and explicit controller postures ([cc96322](https://github.com/bc-solutions-coder/wallow/commit/cc96322fde36a8f5d4b06406503f1a70b1b1d83d))
* **api:** add Microsoft.FeatureManagement.AspNetCore package ([f7d9aa4](https://github.com/bc-solutions-coder/wallow/commit/f7d9aa43552c65507e2f6905e4947d5807ab1044))
* **api:** integrate Microsoft.FeatureManagement for module gating ([a0709fc](https://github.com/bc-solutions-coder/wallow/commit/a0709fcbb07bbb3f3f6b8e46ea8ca193e1ce7be8))
* **billing:** implement settings pattern as reference implementation ([e7b6fa1](https://github.com/bc-solutions-coder/wallow/commit/e7b6fa157f19b62203552549261d03e33fe34194))
* **billing:** implement settings pattern with keyed DI, integration tests, and shared infrastructure fixes ([d19772a](https://github.com/bc-solutions-coder/wallow/commit/d19772af1df5cd5b47219fd699b658fd02760bdf))
* CI/CD and deployment hardening - pin images, add healthcheck, fix trivy, prod compose ([0cd5c93](https://github.com/bc-solutions-coder/wallow/commit/0cd5c93e402f19c4c5c29d81e3ab05b538f61354))
* **communications:** add email provider abstraction with IEmailProvider interface ([e9feb5f](https://github.com/bc-solutions-coder/wallow/commit/e9feb5feb8cd147fb0256da6aa234be2de8a72cd))
* **communications:** add email retry background job ([1d90bbc](https://github.com/bc-solutions-coder/wallow/commit/1d90bbc433f67a4397a7b77c04b0bd599881ee6e))
* **communications:** add enhanced system inbox with archive and expiry ([f9bdd76](https://github.com/bc-solutions-coder/wallow/commit/f9bdd76a3c4141e21aff94cabc15d10135171ad3))
* **communications:** add messaging infrastructure, API, and SMS domain ([23cd745](https://github.com/bc-solutions-coder/wallow/commit/23cd74521bda1e51fb7e333a0ed1455264e16096))
* **communications:** add messaging integration events, handlers, and integration tests ([06808d2](https://github.com/bc-solutions-coder/wallow/commit/06808d200c815eaba611365e962e714d892cd760))
* **communications:** add push notifications with FCM/APNs/WebPush and unified notification settings ([8201074](https://github.com/bc-solutions-coder/wallow/commit/820107414207f5dc8718f12bc9230bde1f738cb3))
* **communications:** add SMS channel with Twilio provider ([d52155f](https://github.com/bc-solutions-coder/wallow/commit/d52155f7f6325b76dd5493ed7b3fb9d68f439d3e))
* **communications:** add test coverage for SMS, notifications, and test builders ([852d731](https://github.com/bc-solutions-coder/wallow/commit/852d7313a4fd777191e3dd07ea579b55169fc6b5))
* **communications:** add unified channel preference model with data migration ([c6e41e3](https://github.com/bc-solutions-coder/wallow/commit/c6e41e39d5269fd230aa51544dbc4e0b50e7746d))
* **communications:** add user-to-user messaging application layer ([d7e8193](https://github.com/bc-solutions-coder/wallow/commit/d7e819330a293f46372e7c926e38222cee3affac))
* **communications:** add user-to-user messaging domain model ([a49cb81](https://github.com/bc-solutions-coder/wallow/commit/a49cb81506243f2eea333db986b375bfa808732b))
* **communications:** split into notifications, messaging, and announcements modules ([4f968d0](https://github.com/bc-solutions-coder/wallow/commit/4f968d01626485c5cae22d239d4cf84221bb18ff))
* complete phase 1 critical security and runtime crash fixes ([27c0f7c](https://github.com/bc-solutions-coder/wallow/commit/27c0f7c06702922c8d041cc1de36d350b8abf8e5))
* critical fixes for v0.3.0 release (phase 1 audit remediation) ([0c96d6b](https://github.com/bc-solutions-coder/wallow/commit/0c96d6b3b4ba58e62f51bc816e7175dce52a5035))
* **docs:** add DocFX docs site with branding-driven theme ([f83b2eb](https://github.com/bc-solutions-coder/wallow/commit/f83b2eb5623e6d6c2f4cdd45f295d04f963c221a))
* Foundry modular monolith platform v0.1.0 ([72230ea](https://github.com/bc-solutions-coder/wallow/commit/72230ea57c029e935c4dad1d92b99546c922db43))
* **identity:** add 27 missing scopes to ApiScopes, ApiScopeSeeder, and update tests ([7458246](https://github.com/bc-solutions-coder/wallow/commit/7458246e9201b04cff65f6156c1d68478d41d36a))
* **identity:** add ClientsController, remove Keycloak, update tests and config ([a3ee604](https://github.com/bc-solutions-coder/wallow/commit/a3ee604db9d8357e57b7ebdce56aade7666c6f28))
* **identity:** add developer self-service DCR proxy with app-* prefix, scope whitelist, and rate limiting ([a11c083](https://github.com/bc-solutions-coder/wallow/commit/a11c083f5728bd32e1ae43114028e194f41eb452))
* **identity:** add explicit admin permission listing and service account permissions ([4bb3b38](https://github.com/bc-solutions-coder/wallow/commit/4bb3b3853b95d45e4fae9f8872d546270b8a2b20))
* **identity:** add IDeveloperAppService and KeycloakDeveloperAppService for DCR proxy ([142cce8](https://github.com/bc-solutions-coder/wallow/commit/142cce820125b91dcc0d61d7efb3e19c97fa8e1a))
* **identity:** add OpenIddict and ASP.NET Core Identity foundation entities and migration ([2d488d3](https://github.com/bc-solutions-coder/wallow/commit/2d488d37e7975bcd2da1a5a70f5f0fdb2bc3801e))
* **identity:** add OpenIddict auth controllers and update auth pipeline ([e58218c](https://github.com/bc-solutions-coder/wallow/commit/e58218c1fcb743857ab991b7ca0e665e3e7d5c26))
* **identity:** add OpenIddict auth, MFA, passwordless login, and Blazor apps ([96e1c46](https://github.com/bc-solutions-coder/wallow/commit/96e1c46ddb475527f82b234dbc3b047f1eb68098))
* **identity:** add PostgreSQL persistence for API keys with Valkey read-through cache ([9339631](https://github.com/bc-solutions-coder/wallow/commit/93396317551d2357dc63ed596a6d7068127c230f))
* **identity:** add TenantId.Platform sentinel unit tests ([779c7ea](https://github.com/bc-solutions-coder/wallow/commit/779c7eabab024a43c90a7522e02a277a11021825))
* **identity:** align FoundryUser with spec and add Organization domain tests ([9a77ca3](https://github.com/bc-solutions-coder/wallow/commit/9a77ca3893815c88635a49aed9541535eb8286b8))
* **identity:** allow HTTP for OpenIddict in development environment ([37f0a60](https://github.com/bc-solutions-coder/wallow/commit/37f0a60dbc1941fe5cc91f16ebcfc157464dcbe5))
* **identity:** harden API key security — extract ScopePermissionMapper, fix revocation race condition, add max key limit ([3e6abf2](https://github.com/bc-solutions-coder/wallow/commit/3e6abf228b5de410556f29f7e6c7c764f82ac49a))
* **identity:** implement dynamic client registration (DCR) ([4651eb6](https://github.com/bc-solutions-coder/wallow/commit/4651eb67de2e3cb3b6f13abe2dcf2bf9feacd63d))
* **identity:** implement MFA overhaul with partial-auth sessions ([68a5bd6](https://github.com/bc-solutions-coder/wallow/commit/68a5bd6d21efe9ac6679d1ce617e172032915614))
* **identity:** replace Keycloak service account, developer app, and SSO services with OpenIddict implementations ([5c7c931](https://github.com/bc-solutions-coder/wallow/commit/5c7c9313209a7b3b6b8e06a18da04c777f01d6bf))
* **identity:** replace Keycloak user and organization services with ASP.NET Core Identity implementations ([6f0f232](https://github.com/bc-solutions-coder/wallow/commit/6f0f232485826cda71202c361ea0c00b05219d7f))
* **identity:** repoint SCIM services to Identity, simplify middleware, remove Keycloak remnants ([7468908](https://github.com/bc-solutions-coder/wallow/commit/74689081d5cc7962352b9ebf89f6e9607a27852a))
* implement security hardening across all modules ([75b41b5](https://github.com/bc-solutions-coder/wallow/commit/75b41b5904234d278702be15374523898cd3180f))
* implement security hardening from sweep 2 across all modules ([d33f87c](https://github.com/bc-solutions-coder/wallow/commit/d33f87c205076f3ff9c9c5df5a7618b0ca4665f7))
* **inquiries:** add domain layer with Inquiry aggregate root ([4aa05fa](https://github.com/bc-solutions-coder/wallow/commit/4aa05fa437ad81ce072ba7cb69d6829d61c42d4f))
* **inquiries:** add inquiry comments feature ([ec8b494](https://github.com/bc-solutions-coder/wallow/commit/ec8b494443c10e2ac94087acdf85b199cc612748))
* **inquiries:** add scopes, permissions, and overhaul inquiry submission ([798a9bf](https://github.com/bc-solutions-coder/wallow/commit/798a9bf411e99db0c571e9a8282e793478f0f44e))
* **inquiries:** add user inquiry view and read scope ([53d4366](https://github.com/bc-solutions-coder/wallow/commit/53d43662e49cdf5332f75944a7be5e4d5d93a3ef))
* **inquiries:** create Inquiries module project structure ([b73ede7](https://github.com/bc-solutions-coder/wallow/commit/b73ede73fd11c9892e434437c1681a427f1a1b25))
* **inquiries:** implement complete Inquiries module with application, infrastructure, and API layers ([f107682](https://github.com/bc-solutions-coder/wallow/commit/f10768271db29034110c2163a73644db00674fec))
* integrate Microsoft.FeatureManagement for module-level feature gating ([0467ccd](https://github.com/bc-solutions-coder/wallow/commit/0467ccd8e0ff16e9c388f76a937649298aef75fd))
* **kernel:** enhance SettingKeyValidator with namespace validation and custom key limits ([e3fb865](https://github.com/bc-solutions-coder/wallow/commit/e3fb8651bf383ab29b87956f6bebcdb4c8af6f02))
* **notifications:** add SignalR handlers for inquiry submitted and status changed events ([bfe8769](https://github.com/bc-solutions-coder/wallow/commit/bfe87699046b591c4728aaff966cca33a34a8467))
* **notifications:** clean up event-driven architecture anti-patterns ([1794e8b](https://github.com/bc-solutions-coder/wallow/commit/1794e8b9f7da2fba516cbbd14345ff7a10a2ec5e))
* **observability:** comprehensive observability improvements to achieve 10/10 score ([51272ce](https://github.com/bc-solutions-coder/wallow/commit/51272ce6193fd9b4f6c4645930aec3ffe17a34d6))
* phase 2 security hardening (SCIM sanitization, IDOR fix, DebugInfo removal, HTML sanitization, HasPermission attributes) ([fc978b0](https://github.com/bc-solutions-coder/wallow/commit/fc978b05960cae65459d11fee1ce0d6f44535c0e))
* phase 4 code quality and DRY improvements (ResultExtensions consolidation, ICurrentUserService, TimeProvider injection, domain exceptions, AuditableEntity cleanup) ([612a45f](https://github.com/bc-solutions-coder/wallow/commit/612a45f1172de4e06d7815ad85d441d1e16968f4))
* phase 5 infrastructure and DevOps improvements (DB resilience, HttpClient resilience, Wolverine idempotency, CI/CD hardening, Docker pinning, TenantId security) ([a4537ae](https://github.com/bc-solutions-coder/wallow/commit/a4537aec75af5e07915ff64671394dc9b6126ed1))
* phase 6 consistency and code quality improvements ([80ec19a](https://github.com/bc-solutions-coder/wallow/commit/80ec19aeb9ed70b7234fc0eaad32a4d85d7180eb))
* **realtime:** add SSE infrastructure with audience-scoped filtering ([4a1430f](https://github.com/bc-solutions-coder/wallow/commit/4a1430fb139b50b156dba3506c741786eb110a24))
* remove Showcases module ([6940f20](https://github.com/bc-solutions-coder/wallow/commit/6940f207faf86f95ba88567727849820be0c1877))
* repo structure updates, CI hardening, and dependency bumps ([6585b11](https://github.com/bc-solutions-coder/wallow/commit/6585b1137615c95895c7c809947f833fd7cece9e))
* retire Configuration module, migrate custom fields to Billing, complete decentralized settings ([fd6a13c](https://github.com/bc-solutions-coder/wallow/commit/fd6a13cdede4b0281d7f1fbae9b907fcfe0303a1))
* roll out settings pattern to identity, storage, and communications modules ([9b4d38e](https://github.com/bc-solutions-coder/wallow/commit/9b4d38e25666e54af37a9e53f9dd7d50d7877639))
* **security:** add JWT auth, tenant isolation, and permission guards for P0/P1 vulnerabilities ([63c4a77](https://github.com/bc-solutions-coder/wallow/commit/63c4a77078e27143ad127fb4d20c29efe19ae43e))
* **security:** remediate P0/P1 findings from security sweep 2 ([552e1b5](https://github.com/bc-solutions-coder/wallow/commit/552e1b596bb3fef4aa979948b966ec655f83970b))
* **shared-infra:** add settings entities, repositories, cached service, and DI wiring ([176b744](https://github.com/bc-solutions-coder/wallow/commit/176b744615452e5317e9a7b81fecdbee045625fa))
* **shared-kernel:** add setting definitions and interfaces for decentralized settings ([dd54d94](https://github.com/bc-solutions-coder/wallow/commit/dd54d943777f0b736e9dbf50e3ff845d92ff1918))
* **showcases:** implement showcases module with CRUD API, domain, infrastructure, and tests ([d097826](https://github.com/bc-solutions-coder/wallow/commit/d097826c6427fecbb9f7e83867d2d3c760dcf572))
* split Shared.Infrastructure into Core, Workflows, BackgroundJobs, and Plugins sub-projects ([25d4ee2](https://github.com/bc-solutions-coder/wallow/commit/25d4ee210ef10b67e71a4fd06b64bb0ad8e0f757))
* **storage:** add virus/malware scanning for file uploads via ClamAV ([fdb8f86](https://github.com/bc-solutions-coder/wallow/commit/fdb8f8655cfb01c1af878ea565d29e4dbd407163))
* **storage:** begin optional ClamAV with design and config refactor ([66ead36](https://github.com/bc-solutions-coder/wallow/commit/66ead361195f1f037f5bff7189e9e69554cd5a86))
* trunk-based workflow with release-please semantic releases ([9f76b1a](https://github.com/bc-solutions-coder/wallow/commit/9f76b1ab41543af8d32743ecde7d37468d4a90b2))
* verify resilience OTel integration and add logging callback tests ([35c6b2c](https://github.com/bc-solutions-coder/wallow/commit/35c6b2c10d23a3a0369c6ace418649068626e270))


### Bug Fixes

* add missing project references for shared test coverage in Rider ([6c9363e](https://github.com/bc-solutions-coder/wallow/commit/6c9363edcf138b68b18184dc27d9332d6de9591e))
* add proper health checks to Auth/Web apps and fix docker healthcheck URLs ([3c07d0c](https://github.com/bc-solutions-coder/wallow/commit/3c07d0c245d6f235157871e08bd03a6075519a89))
* address remaining Qodana warning categories ([c5d420d](https://github.com/bc-solutions-coder/wallow/commit/c5d420d9b8db49e78f6df44da0c6b1764b0b9196))
* align 25 namespace declarations with directory structure (Qodana CheckNamespace) ([8053076](https://github.com/bc-solutions-coder/wallow/commit/8053076653e7ece0d0e90a8dc55e1f86b5088900))
* **ci:** exclude benchmarks project from test discovery ([97739e9](https://github.com/bc-solutions-coder/wallow/commit/97739e9c48a3247d255e0463148aa3f23c7a7c25))
* **ci:** fix release-please auto-merge template parsing error ([699fedf](https://github.com/bc-solutions-coder/wallow/commit/699fedf098887264822f1820817f8e2f218ad465))
* **ci:** set codeql build-mode to manual ([7a8d7a6](https://github.com/bc-solutions-coder/wallow/commit/7a8d7a6de6de22636528f506d1e1e9d005e67514))
* **communications:** resolve CS8604 null-dereference warning in SmtpEmailProvider ([97e561f](https://github.com/bc-solutions-coder/wallow/commit/97e561f50418936e8108b442239eae131d6004cc))
* **deps:** bump resilience packages to satisfy Elsa 3.6.0 transitive dependency ([4d3c583](https://github.com/bc-solutions-coder/wallow/commit/4d3c583bd566388eaf72d263430a73252f188b4f))
* **deps:** resolve Humanizer version conflict and upgrade WireMock.Net to 2.0.0 ([4d3c583](https://github.com/bc-solutions-coder/wallow/commit/4d3c583bd566388eaf72d263430a73252f188b4f))
* **e2e:** improve Blazor login reliability with circuit-aware waits ([fd578d4](https://github.com/bc-solutions-coder/wallow/commit/fd578d4736b9d4e9977ebeac6650b85ce2359884))
* exclude only docs/plans instead of entire docs directory in .dockerignore ([#9](https://github.com/bc-solutions-coder/wallow/issues/9)) ([6963891](https://github.com/bc-solutions-coder/wallow/commit/696389104aef9b4166112ad9646712b23d314400))
* **identity:** update WireMock path patterns to require leading slash ([cfcce9b](https://github.com/bc-solutions-coder/wallow/commit/cfcce9b0e91e5ea565e11adce6c7dee1ddfd7d51))
* **identity:** validate API key scopes against user's current permissions ([1972d62](https://github.com/bc-solutions-coder/wallow/commit/1972d6248da06ddb13f1e6acad118191cd5f24e3))
* make RabbitMQ health check conditional and fix grafana-lgtm health check ([b3724fd](https://github.com/bc-solutions-coder/wallow/commit/b3724fd61abc0da65f8c1ab1414fc023337c75cb))
* phase 3 broken workflows and functional bugs ([39f5766](https://github.com/bc-solutions-coder/wallow/commit/39f57664e0821a99495865ae1ccdd2bec398c350))
* prevent recursive bin/Debug nesting in build output ([97cd5b0](https://github.com/bc-solutions-coder/wallow/commit/97cd5b024a8839945eb249aa2e9f586a70613350))
* remove 24 redundant ToString() calls flagged by Qodana UseNameOfInsteadOfToString ([3619849](https://github.com/bc-solutions-coder/wallow/commit/3619849c35840eb50b269b797541afdaf3bd7a26))
* remove redundant namespace qualifiers across codebase ([0428b44](https://github.com/bc-solutions-coder/wallow/commit/0428b44aa4429c079374bfdf708832a76be95665))
* remove unnecessary using directive in StorageDbContextFactoryTests ([9f4362d](https://github.com/bc-solutions-coder/wallow/commit/9f4362d6d46bf17967e46b7bd8ca69f24e25fc11))
* remove unnecessary using directive in StorageExtensionsTests ([fd8ce3b](https://github.com/bc-solutions-coder/wallow/commit/fd8ce3bd001f1c90d67023cc5853282c129a8bd3))
* remove unnecessary using directives (IDE0005) across test projects ([f2c7dfb](https://github.com/bc-solutions-coder/wallow/commit/f2c7dfb7cbb89f5d9b7423b710dc85d8905c0ebc))
* resolve codeql build errors from analyzer rules ([a5aa8a1](https://github.com/bc-solutions-coder/wallow/commit/a5aa8a15860bc82544c3cf85983c441b990efdb1))
* resolve qodana phase 2 bug risk warnings across all modules ([90b4925](https://github.com/bc-solutions-coder/wallow/commit/90b492589d35e2795e104f303e3fb4e851d0ecbc))
* resolve qodana phase 3 code hygiene warnings across all modules ([a3e8ca0](https://github.com/bc-solutions-coder/wallow/commit/a3e8ca070978478b17bd7ec4fdacf6d0c8e1254f))
* **showcases:** remove [AllowAnonymous] from read endpoints ([509a605](https://github.com/bc-solutions-coder/wallow/commit/509a6051371e781f06c54ab82ba47e835d10bf4e))
* suppress 58 unused positional record property warnings (Qodana NotAccessedPositionalProperty.Global) ([4812fda](https://github.com/bc-solutions-coder/wallow/commit/4812fdac2a4665ee2e7fac0dacb580fddc6fc39f))
* suppress 59 unused auto-property accessor warnings (Qodana UnusedAutoPropertyAccessor.Global) ([58a6a58](https://github.com/bc-solutions-coder/wallow/commit/58a6a58d003e137096eb968d05ec9bee7698eec4))
* suppress and resolve remaining small Qodana warning categories (~45 issues) ([069f7df](https://github.com/bc-solutions-coder/wallow/commit/069f7df37c6cc7078a4a1b5bd38d588981b1277f))
* suppress unused EF Core constructors and remove dead code (Qodana UnusedMember.Local) ([6cce9a5](https://github.com/bc-solutions-coder/wallow/commit/6cce9a583bcc1cbf646feb2513c700bf86fb4ef4))


### Performance Improvements

* phase 3 performance optimization (NoTracking, pagination, Redis consolidation, cache fix, SMTP reuse, FlushUsageJob optimization) ([c1cbac9](https://github.com/bc-solutions-coder/wallow/commit/c1cbac949554784aa4254d23a4f9a2eecfe554e5))


### Code Refactoring

* rename project from Foundry to Wallow ([ac21d2a](https://github.com/bc-solutions-coder/wallow/commit/ac21d2abee2ed1c19407d11319fcf46881ac0deb))

## [0.2.0](https://github.com/bc-solutions-coder/Wallow/compare/v0.1.0...v0.2.0) (2026-03-01)


### Features

* trunk-based workflow with release-please semantic releases ([9f76b1a](https://github.com/bc-solutions-coder/Wallow/commit/9f76b1ab41543af8d32743ecde7d37468d4a90b2))


### Bug Fixes

* **ci:** fix release-please auto-merge template parsing error ([699fedf](https://github.com/bc-solutions-coder/Wallow/commit/699fedf098887264822f1820817f8e2f218ad465))
