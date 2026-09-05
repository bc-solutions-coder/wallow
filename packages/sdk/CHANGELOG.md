# Changelog

## [1.0.0](https://github.com/bc-solutions-coder/wallow/compare/sdk-v0.2.0...sdk-v1.0.0) (2026-09-05)


### ⚠ BREAKING CHANGES

* **sdk:** the SDK-private code constants CSRF_INVALID_CODE, NETWORK_ERROR_CODE, and NETWORK_TIMEOUT_CODE are deleted (pinned deleted by src/index.test.ts); their wire values are now Bff.CsrfInvalid, Transport.NetworkError, and Transport.Timeout from api-errors, and the proxy's forward timeout answers 504 instead of 503. The bodiless 401s from the proxy and /bff/user now carry a problem body.
* **sdk:** throw ApiFailure from the SDK and retarget consumers
* **api:** error bodies lose the api, version, and instance members; titles are reason phrases; validation errors are a dictionary keyed by camelCase field path; ValidationProblemDetails becomes HttpValidationProblemDetails in the SDK.
* **api:** error-code catalog with kind-derived status and OpenAPI export
* **identity:** the consent feature no longer fetches scope descriptions by bare client_id; screens resolve client context only through the transaction-scoped authorize-context lookup.
* **identity:** the legacy `/v1/identity/clients/service-accounts` routes, `ServiceAccountMetadata`/`ServiceAccountStatus`, the `ServiceAccounts*` permissions and the `serviceaccounts.*` scopes are removed; the `identity.service_account_metadata` table is dropped.
* **sdk:** `CLIENT_IP_HEADER` is no longer exported from `./server` or `./server/passthrough`; `handle()`/`handleApi()` read the peer address from `request.ip` instead. Hosts must pass the runtime request through unchanged rather than stamping a header on a copy.
* **seeder:** seed.json no longer seeds bcordes-bff or sa-bcordes-bff; the client array renumbers, so positional Clients__<index>__* overrides shift (docker-compose.test.yml now targets Clients__1__* for bff-example-client). Production secret injection keeps only ClientSecrets__wallow-web-client.
* **identity:** POST /v1/identity/setup/complete no longer exists; the generated SetupCompleteSetup operation is gone from the SDK.
* **sdk:** setCsrfToken and getCsrfToken are no longer exported. Nothing needs them: the CSRF interceptor and logout() resolve the token from the BFF's double-submit cookie at request time.
* **sdk:** the @bc-solutions-coder/sdk browser entry no longer exports the WallowRouterContext type. Declare the router context shape in the app's __root.tsx, as all three reference apps do.
* **sdk:** the @bc-solutions-coder/sdk browser entry no longer exports getRoles, hasRole, isAdmin, isOperator, isGlobalAdmin, getOrgId, or getOrgName. Gate UI with hasRole/hasPermission/isAdmin from @bc-solutions-coder/auth over the typed current-user response.
* **sdk:** `login` and `getUser` are no longer exported from @bc-solutions-coder/sdk; use `loginRedirect()` and `getCurrentUser()`.
* **identity:** CreateAdminRequest requires organizationName. Roles are granted per organization, so an administrator created without one holds no permission anywhere.
* **identity:** OrganizationSettingsDto gains three members and the "user" role no longer carries OrganizationsCreate or OrganizationsUpdate. A fork that wants self-service organization creation grants it on a role of its own.
* **identity:** POST /v1/identity/organizations/{id}/members now requires a "role" field naming the role granted in that organization.

### Features

* **api-errors:** publish the dependency-free failure package ([1781636](https://github.com/bc-solutions-coder/wallow/commit/17816366f1343cb410ee849a34dc6dc2e564ef4f)), closes [#179](https://github.com/bc-solutions-coder/wallow/issues/179)
* **api:** error-code catalog with kind-derived status and OpenAPI export ([a918486](https://github.com/bc-solutions-coder/wallow/commit/a918486ab5fd774c02a36fffdb2d50fed0708b44)), closes [#177](https://github.com/bc-solutions-coder/wallow/issues/177)
* **api:** single problem writer and the unified problem contract ([b4bcc3f](https://github.com/bc-solutions-coder/wallow/commit/b4bcc3f028757b33e4d8e65c83ae897010d6dff7)), closes [#178](https://github.com/bc-solutions-coder/wallow/issues/178)
* **branding:** client branding sub-resource, editor and live preview ([770513d](https://github.com/bc-solutions-coder/wallow/commit/770513dbac22c2247000829d3701480abd239e2f)), closes [#141](https://github.com/bc-solutions-coder/wallow/issues/141)
* **env:** add @bc-solutions-coder/env and rewire the apps onto it ([188bfc9](https://github.com/bc-solutions-coder/wallow/commit/188bfc9e2c7024b1954db03660677f538a918c9e))
* **identity:** add per-organization enrollment policy ([3fda0a6](https://github.com/bc-solutions-coder/wallow/commit/3fda0a6bb612e7dedbb794ef25a7964ba9538941))
* **identity:** back-channel logout on the OP side ([96e61b2](https://github.com/bc-solutions-coder/wallow/commit/96e61b21582e97204fb62c18e98d5bd2f9902adf)), closes [#146](https://github.com/bc-solutions-coder/wallow/issues/146)
* **identity:** client-branded authorize-transaction screens ([a4fba6e](https://github.com/bc-solutions-coder/wallow/commit/a4fba6e437cf4459559cc4a034f4908efc113df3)), closes [#142](https://github.com/bc-solutions-coder/wallow/issues/142)
* **identity:** connected applications API with consent withdrawal ([6272e0a](https://github.com/bc-solutions-coder/wallow/commit/6272e0a25cbb4872d8a850e4f5dcb12d439559dd)), closes [#143](https://github.com/bc-solutions-coder/wallow/issues/143)
* **identity:** consent is a POST with a single-use token ([3127193](https://github.com/bc-solutions-coder/wallow/commit/3127193798b3c23fd599a5258eb765046f359669)), closes [#132](https://github.com/bc-solutions-coder/wallow/issues/132)
* **identity:** drop identity.user_roles and give bootstrap an organization ([3a0099a](https://github.com/bc-solutions-coder/wallow/commit/3a0099acf5f2e02e55ab57b81b8e1d8fdb8ecdd6))
* **identity:** drop the no-op POST /v1/identity/setup/complete ([398d6eb](https://github.com/bc-solutions-coder/wallow/commit/398d6eba2a0a699a750f000b7267ee72f432eb1e)), closes [#109](https://github.com/bc-solutions-coder/wallow/issues/109)
* **identity:** grant a client the scopes it may request ([7bbe3ad](https://github.com/bc-solutions-coder/wallow/commit/7bbe3ad21a146831dc265da9ed31500f06bffb46))
* **identity:** list suspended and denied memberships per organization ([c06797e](https://github.com/bc-solutions-coder/wallow/commit/c06797ee8ae849a3a7ddc82cf6778740bd82a3df))
* **identity:** notify relying parties of logout via OIDC front-channel ([9783132](https://github.com/bc-solutions-coder/wallow/commit/97831322abe05ffa078b4688aa499a49d96da36e))
* **identity:** org-scoped developer application registration ([f366c1b](https://github.com/bc-solutions-coder/wallow/commit/f366c1b55de7007896d0bd98734c0a64737b48b2)), closes [#135](https://github.com/bc-solutions-coder/wallow/issues/135)
* **identity:** organization hint and org-less first-party tokens ([d95f3ed](https://github.com/bc-solutions-coder/wallow/commit/d95f3eda718d1a1b1b9e11947848fdbdd30b8425)), closes [#134](https://github.com/bc-solutions-coder/wallow/issues/134)
* **identity:** per-client refresh-token lifetime with pinned refresh defaults ([5d0a71b](https://github.com/bc-solutions-coder/wallow/commit/5d0a71b1817b7b3ff92cabf5809207012b2eb40b)), closes [#144](https://github.com/bc-solutions-coder/wallow/issues/144)
* **identity:** platform suspension of clients and organizations ([77b4dd9](https://github.com/bc-solutions-coder/wallow/commit/77b4dd94d3f2d0b53a2b1080a5d0eec355fbcc29)), closes [#139](https://github.com/bc-solutions-coder/wallow/issues/139)
* **identity:** report the signed-in organization on userinfo ([4965ae1](https://github.com/bc-solutions-coder/wallow/commit/4965ae1ed2e902f043707e85a0f8444d5ced6613))
* **identity:** rotate client secrets with revoke and audit events ([079ccc9](https://github.com/bc-solutions-coder/wallow/commit/079ccc94bde54d019dbf72bc2ad764b56ed25adf)), closes [#137](https://github.com/bc-solutions-coder/wallow/issues/137)
* **identity:** service accounts on the org-scoped client surface ([d8e5073](https://github.com/bc-solutions-coder/wallow/commit/d8e5073f61642c328344ab7bd96d9d78483bdfcd)), closes [#136](https://github.com/bc-solutions-coder/wallow/issues/136)
* **identity:** suspend, reinstate and delete organization clients ([afe615d](https://github.com/bc-solutions-coder/wallow/commit/afe615d8b19d2fa0a6b44fa9fe7db55750945b8c)), closes [#138](https://github.com/bc-solutions-coder/wallow/issues/138)
* **logger:** add @bc-solutions-coder/logger and wire both apps ([b03d7ea](https://github.com/bc-solutions-coder/wallow/commit/b03d7ea013bb3f8248f64b5812fbc02c3449b0b1))
* **minimal-app:** external RP example and three-origin acceptance ([14382ed](https://github.com/bc-solutions-coder/wallow/commit/14382ed67b903570f2954ff50e9d7a977ca5dbeb)), closes [#151](https://github.com/bc-solutions-coder/wallow/issues/151)
* **sdk:** add createServiceClient on server/service subpath ([0bf9c64](https://github.com/bc-solutions-coder/wallow/commit/0bf9c64d8f7089c07f97adb8a96fcdbf20d4c865)), closes [#148](https://github.com/bc-solutions-coder/wallow/issues/148)
* **sdk:** add the COOKIE_SAMESITE cookie hardening knob ([bdb446f](https://github.com/bc-solutions-coder/wallow/commit/bdb446fa9776e55d773df80a37a6871118030703))
* **sdk:** BFF_APP_ID namespaces cookies and the valkey store ([c53f0c0](https://github.com/bc-solutions-coder/wallow/commit/c53f0c04687aa7ad16cda1b0728a003a0fdc7344)), closes [#159](https://github.com/bc-solutions-coder/wallow/issues/159)
* **sdk:** originate BFF proxy and passthrough failures as problems ([732b573](https://github.com/bc-solutions-coder/wallow/commit/732b573db2ee829b31c08edb441606a035002a84)), closes [#181](https://github.com/bc-solutions-coder/wallow/issues/181)
* **sdk:** receive back-channel logout at POST /bff/backchannel-logout ([157d596](https://github.com/bc-solutions-coder/wallow/commit/157d5962c76f6f8eb0cd259dbe1876fe6e16a0be)), closes [#147](https://github.com/bc-solutions-coder/wallow/issues/147)
* **sdk:** regenerate OpenAPI snapshot and typed client ([fff470d](https://github.com/bc-solutions-coder/wallow/commit/fff470d5dcd0e1b22c676fa9205a7f24ce386275))
* **sdk:** regenerate OpenAPI snapshot and typed client ([002c2c8](https://github.com/bc-solutions-coder/wallow/commit/002c2c8632e19396b2b1c51c71837d3bc37b101b))
* **sdk:** resolve the client address in the server presets ([fe71f1b](https://github.com/bc-solutions-coder/wallow/commit/fe71f1bf53015fd229cb62fcf489a22720effbd9))
* **sdk:** tear down dead session when refresh fails ([2c59c72](https://github.com/bc-solutions-coder/wallow/commit/2c59c7274613b7acc78cae7f8e05dde924a7f651)), closes [#145](https://github.com/bc-solutions-coder/wallow/issues/145)
* **sdk:** throw ApiFailure from the SDK and retarget consumers ([5b04bc3](https://github.com/bc-solutions-coder/wallow/commit/5b04bc3e63bcddb085ac348fd5e94a364f3db011)), closes [#180](https://github.com/bc-solutions-coder/wallow/issues/180)
* **seeder:** remove the bcordes client seeding ([ce16611](https://github.com/bc-solutions-coder/wallow/commit/ce166116ac33a96a012ef845f4d297eaf569133a)), closes [#111](https://github.com/bc-solutions-coder/wallow/issues/111)
* **utils:** add @bc-solutions-coder/utils and rewire the apps onto it ([bc7c5c7](https://github.com/bc-solutions-coder/wallow/commit/bc7c5c734302c7888def234445c5cbce846345a7))


### Bug Fixes

* **docs:** restore the frontend state boundary the CLAUDE.md split dropped ([99d090b](https://github.com/bc-solutions-coder/wallow/commit/99d090b419d1fc10a1e55b76e60a64c9f4653e74))
* **identity:** address back-channel logout review findings ([8fa6023](https://github.com/bc-solutions-coder/wallow/commit/8fa6023a4ef4829a44fd24dd69e8ca1fe4ea1986))
* **identity:** bootstrap admin joins the seeded organization ([c6a0f57](https://github.com/bc-solutions-coder/wallow/commit/c6a0f579d30a498addfe594089060b39dc54f59a))
* **identity:** keep the authorize request when a consent post is anonymous ([a178bb7](https://github.com/bc-solutions-coder/wallow/commit/a178bb78dc4787fe50025d4f5dc87027dcd87c2f)), closes [#132](https://github.com/bc-solutions-coder/wallow/issues/132)
* **identity:** validate enrollment requests on the constructor parameter ([fb0d5a7](https://github.com/bc-solutions-coder/wallow/commit/fb0d5a74a78c5cea5b66ed3b0381dd96df36e083))
* **lint:** register the wallow/* plugin in navigation, ui and forms ([d600e25](https://github.com/bc-solutions-coder/wallow/commit/d600e25f4566047adfe67a944fddfd71c2315a05))
* **sdk:** read a blank COOKIE_NAME as unset, and surface the BFF's remaining knobs ([3520ff1](https://github.com/bc-solutions-coder/wallow/commit/3520ff155049328e3b4505abfce842759854424e))
* **storage:** replace broken async presigned-upload scan with a completion endpoint ([ddca318](https://github.com/bc-solutions-coder/wallow/commit/ddca318d3ba63dc9cd5dd41ad904d4fb7a41f63e))


### Code Refactoring

* **identity:** read and write organization members through memberships ([d1eaa3d](https://github.com/bc-solutions-coder/wallow/commit/d1eaa3d57a8527808338f7b58655f26d19b3a44e))
* **sdk:** delete imperative login() and getUser() browser helpers ([42035fc](https://github.com/bc-solutions-coder/wallow/commit/42035fc231768c3546fc9811803e0db8a6133fd0))
* **sdk:** delete the browser claim-bag readers; one typed user model ([722a598](https://github.com/bc-solutions-coder/wallow/commit/722a598ce5cf753909a13b63ae8b9de87443651c))
* **sdk:** delete the module-scope CSRF token store; add a csrf opt-out ([6603cf7](https://github.com/bc-solutions-coder/wallow/commit/6603cf7b46fff66b7ea2d636f7dbe57544b6fe74))
* **sdk:** delete the unadopted WallowRouterContext interface ([107aba2](https://github.com/bc-solutions-coder/wallow/commit/107aba2dea9ec3ab33d997a338ce7cdc0524ceaf))

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
