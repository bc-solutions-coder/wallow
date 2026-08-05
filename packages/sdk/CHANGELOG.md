# Changelog

## [1.0.0](https://github.com/bc-solutions-coder/wallow/compare/sdk-v0.2.0...sdk-v1.0.0) (2026-08-05)


### ⚠ BREAKING CHANGES

* **identity:** CreateAdminRequest requires organizationName. Roles are granted per organization, so an administrator created without one holds no permission anywhere.
* **identity:** OrganizationSettingsDto gains three members and the "user" role no longer carries OrganizationsCreate or OrganizationsUpdate. A fork that wants self-service organization creation grants it on a role of its own.
* **identity:** POST /v1/identity/organizations/{id}/members now requires a "role" field naming the role granted in that organization.
* packages/web-shell no longer exists. Its createQueryClient() moved to @bc-solutions-coder/query and its current-user helpers moved to @bc-solutions-coder/auth; consumers must switch imports to the new packages.
* @bc-solutions-coder/sdk drops its h3 dependency and collapses its hand-written surface. The server entry now exports web-standard Request to Response handlers instead of h3 EventHandlers, and consumers must construct a per-request client with createWallowSdk() rather than importing a module-global singleton. The operation and query surfaces are generated, so hand-written wrappers and inline query keys are gone; all query keys come from the generated factory.

### Features

* add packages/query and packages/auth, delete packages/web-shell ([521fd1b](https://github.com/bc-solutions-coder/wallow/commit/521fd1bc09d843c1a7cc5b37bd3d0bf69cd1dea6))
* **env:** add @bc-solutions-coder/env and rewire the apps onto it ([188bfc9](https://github.com/bc-solutions-coder/wallow/commit/188bfc9e2c7024b1954db03660677f538a918c9e))
* **identity:** add per-organization enrollment policy ([3fda0a6](https://github.com/bc-solutions-coder/wallow/commit/3fda0a6bb612e7dedbb794ef25a7964ba9538941))
* **identity:** drop identity.user_roles and give bootstrap an organization ([3a0099a](https://github.com/bc-solutions-coder/wallow/commit/3a0099acf5f2e02e55ab57b81b8e1d8fdb8ecdd6))
* **identity:** grant a client the scopes it may request ([7bbe3ad](https://github.com/bc-solutions-coder/wallow/commit/7bbe3ad21a146831dc265da9ed31500f06bffb46))
* **identity:** list suspended and denied memberships per organization ([c06797e](https://github.com/bc-solutions-coder/wallow/commit/c06797ee8ae849a3a7ddc82cf6778740bd82a3df))
* **identity:** report the signed-in organization on userinfo ([4965ae1](https://github.com/bc-solutions-coder/wallow/commit/4965ae1ed2e902f043707e85a0f8444d5ced6613))
* **logger:** add @bc-solutions-coder/logger and wire both apps ([b03d7ea](https://github.com/bc-solutions-coder/wallow/commit/b03d7ea013bb3f8248f64b5812fbc02c3449b0b1))
* migrate apps to tanstack start and streamline sdk ([cc8311e](https://github.com/bc-solutions-coder/wallow/commit/cc8311e38f36d186dfa8b79a5933d3d615b5ed75))
* **sdk:** expose rfc 7807 field errors on WallowError ([3a15ad8](https://github.com/bc-solutions-coder/wallow/commit/3a15ad8b971e811921b9b6eac628347c333f4f31))
* **sdk:** regenerate OpenAPI snapshot and typed client ([002c2c8](https://github.com/bc-solutions-coder/wallow/commit/002c2c8632e19396b2b1c51c71837d3bc37b101b))
* **utils:** add @bc-solutions-coder/utils and rewire the apps onto it ([bc7c5c7](https://github.com/bc-solutions-coder/wallow/commit/bc7c5c734302c7888def234445c5cbce846345a7))


### Bug Fixes

* **docs:** make docs/toc.yml the single site-root toc and guard it ([f0af8d4](https://github.com/bc-solutions-coder/wallow/commit/f0af8d4d678fb33de3ba5072cf94039221cc83cd))
* **docs:** restore the frontend state boundary the CLAUDE.md split dropped ([99d090b](https://github.com/bc-solutions-coder/wallow/commit/99d090b419d1fc10a1e55b76e60a64c9f4653e74))
* **identity:** validate enrollment requests on the constructor parameter ([fb0d5a7](https://github.com/bc-solutions-coder/wallow/commit/fb0d5a74a78c5cea5b66ed3b0381dd96df36e083))
* **inquiries:** type the 201 response on inquiry comment creation ([8eeea2d](https://github.com/bc-solutions-coder/wallow/commit/8eeea2df7d83de76d732f455577428de4c31652c))
* **lint:** clear the diagnostics the inherited config surfaced ([7af64f7](https://github.com/bc-solutions-coder/wallow/commit/7af64f7f2af6c0cf37dd814f9781420a8158e684))
* **lint:** register the wallow/* plugin in navigation, ui and forms ([d600e25](https://github.com/bc-solutions-coder/wallow/commit/d600e25f4566047adfe67a944fddfd71c2315a05))
* **sdk:** fail at boot on a too-short COOKIE_PASSWORD ([3438efc](https://github.com/bc-solutions-coder/wallow/commit/3438efc7a9828bdf941e4c467410a086200dfecb))
* **sdk:** fall back to the double-submit cookie in the CSRF interceptor ([4d3aec4](https://github.com/bc-solutions-coder/wallow/commit/4d3aec498ed1d8679e80709038458286977af427))
* **sdk:** forward the real client IP through the BFF api proxy ([01ce6fb](https://github.com/bc-solutions-coder/wallow/commit/01ce6fb08027dca097be6758ce2b669549617242))
* **sdk:** idempotent logout, cookie-password rotation, refresh coalescing, and split-horizon URL pinning ([acb1794](https://github.com/bc-solutions-coder/wallow/commit/acb1794169d7ca2bab5effbabe83fe29e342031d))
* **sdk:** preserve the issuer path when pinning browser-facing OIDC endpoints ([3a3c192](https://github.com/bc-solutions-coder/wallow/commit/3a3c192cd2a62a7be2adf03601a1926cb341a88e))
* **sdk:** read a blank COOKIE_NAME as unset, and surface the BFF's remaining knobs ([3520ff1](https://github.com/bc-solutions-coder/wallow/commit/3520ff155049328e3b4505abfce842759854424e))


### Code Refactoring

* **identity:** read and write organization members through memberships ([d1eaa3d](https://github.com/bc-solutions-coder/wallow/commit/d1eaa3d57a8527808338f7b58655f26d19b3a44e))

## [0.2.0](https://github.com/bc-solutions-coder/wallow/compare/sdk-v0.1.0...sdk-v0.2.0) (2026-07-26)


### Features

* **sdk:** absorb csrf, ssr context, and facade helpers ([a1f64f6](https://github.com/bc-solutions-coder/wallow/commit/a1f64f608ed94d1e967bc309c86dc983319ab2b8))
* **sdk:** add apps query module ([e020264](https://github.com/bc-solutions-coder/wallow/commit/e02026443adc91a1c0bec2196c7a74c8fb9a9541))
* **sdk:** add auth facade and oidc helpers for tanstack auth app ([5d761ea](https://github.com/bc-solutions-coder/wallow/commit/5d761eabab40a0e4e6f1ddc99049ae3f6e4c600a))
* **sdk:** add auth query module ([182881f](https://github.com/bc-solutions-coder/wallow/commit/182881f3788d9c72330033a7ec2e1f44f494ddf0))
* **sdk:** add central query-key factory ([0ebad6f](https://github.com/bc-solutions-coder/wallow/commit/0ebad6fe484072c24f04b9a0631ca031aa71d4ee))
* **sdk:** add getCurrentUser auth-state facade method ([ddb02f3](https://github.com/bc-solutions-coder/wallow/commit/ddb02f37da7ffc1fcf57d345e9905d68cefb3c65))
* **sdk:** add inquiries query module ([e844369](https://github.com/bc-solutions-coder/wallow/commit/e84436918b108df8310f822f99a04d1e9ca113bd))
* **sdk:** add lazy query-layer bootstrap seam ([565627b](https://github.com/bc-solutions-coder/wallow/commit/565627b4c39041d78cc099fa56fd3e060fb9c3e4))
* **sdk:** add mfa query module ([fabc046](https://github.com/bc-solutions-coder/wallow/commit/fabc046a219afd92a94bb7fde3ee8cb5a5669fc9))
* **sdk:** add organizations query module ([e1891f8](https://github.com/bc-solutions-coder/wallow/commit/e1891f80b031715e4be352cae83ec71c08636155))
* **sdk:** add settings query module ([c755ab5](https://github.com/bc-solutions-coder/wallow/commit/c755ab566f2e7af5da8a4716814d3f55abc75d29))
* **sdk:** add user query module ([13d8d91](https://github.com/bc-solutions-coder/wallow/commit/13d8d91019f03b895e9716acfe5ea83410f3c9a6))
* **sdk:** expose query layer via ./query subpath ([ae76871](https://github.com/bc-solutions-coder/wallow/commit/ae7687136f4d332b13231277af4177170d7b1cc8))
* **sdk:** restructure sdk into pnpm monorepo with redis adapter ([b48cb7c](https://github.com/bc-solutions-coder/wallow/commit/b48cb7c6f5c3e3a746dc7b9f16183e98a76d9b19))
* **sdk:** share mfa api-wrapper slice across apps ([5436863](https://github.com/bc-solutions-coder/wallow/commit/5436863a975f6b3c10979774dd0fdd87125772a1))
* **web:** restore route-tree codegen, centralize styling, regenerate openapi client ([9ea6928](https://github.com/bc-solutions-coder/wallow/commit/9ea69283a1d4885f7af1484358f82fd798c842c1))


### Bug Fixes

* restore example app to pnpm workspace after apps/ relocation ([33d2cdf](https://github.com/bc-solutions-coder/wallow/commit/33d2cdf1f12ea4c920deefb055058b285fd49307))
* **sdk:** add SSR-safe baseUrl and headers options to getUser ([6b6dd54](https://github.com/bc-solutions-coder/wallow/commit/6b6dd54472e4621ae53476023d438ad03c2d9543))
* **sdk:** bump @hey-api/openapi-ts to 0.99 to clear npm security alerts ([1b1ee70](https://github.com/bc-solutions-coder/wallow/commit/1b1ee7051ed65266dd1230ef04e012927abfc323))
* **sdk:** preserve auth error code through error mapping ([d4e27e0](https://github.com/bc-solutions-coder/wallow/commit/d4e27e0921a8eaf055990e3b80482752d061c882))
* **sdk:** scope client-facing auth queries by client_id ([ce5350d](https://github.com/bc-solutions-coder/wallow/commit/ce5350d59796d0a648013dccd90e9a5c3c2d14f0))
* **wallow-auth:** resolve the org-domain interstitial before submitting register ([0320b4d](https://github.com/bc-solutions-coder/wallow/commit/0320b4da0b9e3faa004f234f6d2c38e4c23e2a45))
* **web:** route SSR self-fetch through an internal origin override ([43285af](https://github.com/bc-solutions-coder/wallow/commit/43285aff3b8b9dcea5639ec7d1b330c0654dd2a0))
