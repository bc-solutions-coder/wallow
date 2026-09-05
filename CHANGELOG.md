# Changelog

## [5.0.0](https://github.com/bc-solutions-coder/wallow/compare/v4.0.0...v5.0.0) (2026-09-05)


### ⚠ BREAKING CHANGES

* **wallow-web:** wallow-web's read and mutation sites no longer accept per-site error fallback strings; `errorText` is no longer used by the app.
* **forms:** resolve submit failures through api-errors and the registry
* **ui,query:** failure surfaces — toast, banner, provider, client callback
* **sdk:** the SDK-private code constants CSRF_INVALID_CODE, NETWORK_ERROR_CODE, and NETWORK_TIMEOUT_CODE are deleted (pinned deleted by src/index.test.ts); their wire values are now Bff.CsrfInvalid, Transport.NetworkError, and Transport.Timeout from api-errors, and the proxy's forward timeout answers 504 instead of 503. The bodiless 401s from the proxy and /bff/user now carry a problem body.
* **sdk:** throw ApiFailure from the SDK and retarget consumers
* **api:** error bodies lose the api, version, and instance members; titles are reason phrases; validation errors are a dictionary keyed by camelCase field path; ValidationProblemDetails becomes HttpValidationProblemDetails in the SDK.
* **api:** error-code catalog with kind-derived status and OpenAPI export
* **identity:** the consent feature no longer fetches scope descriptions by bare client_id; screens resolve client context only through the transaction-scoped authorize-context lookup.
* **identity:** transaction-scoped authorize-context, drop anonymous client reads
* **identity:** the legacy `/v1/identity/clients/service-accounts` routes, `ServiceAccountMetadata`/`ServiceAccountStatus`, the `ServiceAccounts*` permissions and the `serviceaccounts.*` scopes are removed; the `identity.service_account_metadata` table is dropped.
* **sdk:** `CLIENT_IP_HEADER` is no longer exported from `./server` or `./server/passthrough`; `handle()`/`handleApi()` read the peer address from `request.ip` instead. Hosts must pass the runtime request through unchanged rather than stamping a header on a copy.
* **seeder:** seed.json no longer seeds bcordes-bff or sa-bcordes-bff; the client array renumbers, so positional Clients__<index>__* overrides shift (docker-compose.test.yml now targets Clients__1__* for bff-example-client). Production secret injection keeps only ClientSecrets__wallow-web-client.
* **docker:** .env.production no longer defines ADMIN_EMAIL / ADMIN_PASSWORD / ADMIN_FIRST_NAME / ADMIN_LAST_NAME, and the production stack no longer bootstraps an admin from configuration.
* **seeder:** the seed admin block requires organizationName; an admin config without it is treated as not configured and bootstrap is skipped.
* **identity:** POST /v1/identity/setup/complete no longer exists; the generated SetupCompleteSetup operation is gone from the SDK.
* **seeder:** deployments must rename the seeder's secret env keys from Clients__<index>__Secret to ClientSecrets__<clientId>; .env.production values are unchanged.
* **sdk:** setCsrfToken and getCsrfToken are no longer exported. Nothing needs them: the CSRF interceptor and logout() resolve the token from the BFF's double-submit cookie at request time.
* **sdk:** the @bc-solutions-coder/sdk browser entry no longer exports the WallowRouterContext type. Declare the router context shape in the app's __root.tsx, as all three reference apps do.
* **sdk:** the @bc-solutions-coder/sdk browser entry no longer exports getRoles, hasRole, isAdmin, isOperator, isGlobalAdmin, getOrgId, or getOrgName. Gate UI with hasRole/hasPermission/isAdmin from @bc-solutions-coder/auth over the typed current-user response.
* **sdk:** `login` and `getUser` are no longer exported from @bc-solutions-coder/sdk; use `loginRedirect()` and `getCurrentUser()`.
* contributors need pnpm 11.24.0. corepack installs it from the packageManager field, but a fork still on pnpm 10 will fail to install on the allowBuilds key, which pnpm 10 does not understand.
* **identity:** CreateAdminRequest requires organizationName. Roles are granted per organization, so an administrator created without one holds no permission anywhere.
* **announcements:** AnnouncementTarget.Plan no longer exists. Any announcement row stored with target "Plan" will fail to deserialize; re-seed instead.
* **identity:** IOrganizationService.AddMemberAsync/RemoveMemberAsync and IUserManagementService.AssignRoleAsync/RemoveRoleAsync take a Guid actorId before the CancellationToken.
* **identity:** OrganizationSettingsDto gains three members and the "user" role no longer carries OrganizationsCreate or OrganizationsUpdate. A fork that wants self-service organization creation grants it on a role of its own.
* **identity:** POST /v1/identity/invitations now returns 422 Identity.AlreadyAMember when the address already belongs to the organization, and re-inviting returns the existing token rather than a new one.
* **identity:** IInvitationService.CreateInvitationAsync no longer takes a tenantId; it invites into the caller's own organization.
* **identity:** POST /v1/identity/invitations/{token}/accept now returns 422 when the caller is not the invited verified user or the invitation has lapsed.
* **identity:** access tokens are now validated against the token entry on every request, so revoked tokens are refused immediately.
* **identity:** refuse token issuance for a membership that is not active
* **identity:** WallowUser has no TenantId, and the person-scoped identity integration events carry a nullable one.
* **identity:** the auth cookie carries neither org_id nor role claims.
* **identity:** drop OrgMemberRole in favour of the shared role catalog
* **identity:** keep global role claims out of the auth cookie
* **identity:** refreshed tokens carry only the roles the token's own organization grants.
* **identity:** authorize refuses a client that carries no tenant binding, and role claims are scoped to the client's organization.
* **identity:** grant the scopes a caller is entitled to instead of refusing the request
* **identity:** write role assignments to memberships, not the global role store
* **identity:** gate cross-organization access on per-org permissions
* **identity:** POST /v1/identity/organizations/{id}/members now requires a "role" field naming the role granted in that organization.

### Features

* **api-errors:** publish the dependency-free failure package ([1781636](https://github.com/bc-solutions-coder/wallow/commit/17816366f1343cb410ee849a34dc6dc2e564ef4f)), closes [#179](https://github.com/bc-solutions-coder/wallow/issues/179)
* **api:** add a wolverine dead-letter queue health check ([a75d94e](https://github.com/bc-solutions-coder/wallow/commit/a75d94e0d8979130d6ae0c3deeba7395daa1defa))
* **api:** emit one OpenAPI document per API version ([bff4a03](https://github.com/bc-solutions-coder/wallow/commit/bff4a0344aabbc23ee02804a06120b34ef875afa))
* **api:** error-code catalog with kind-derived status and OpenAPI export ([a918486](https://github.com/bc-solutions-coder/wallow/commit/a918486ab5fd774c02a36fffdb2d50fed0708b44)), closes [#177](https://github.com/bc-solutions-coder/wallow/issues/177)
* **api:** single problem writer and the unified problem contract ([b4bcc3f](https://github.com/bc-solutions-coder/wallow/commit/b4bcc3f028757b33e4d8e65c83ae897010d6dff7)), closes [#178](https://github.com/bc-solutions-coder/wallow/issues/178)
* **api:** surface the wolverine dlq on /health as wolverine-dlq ([5d07631](https://github.com/bc-solutions-coder/wallow/commit/5d076317aa4a63f666463be5de40c669e89fdb1d))
* **auth:** add the zone alias map and its tsconfig pin test ([eedddfe](https://github.com/bc-solutions-coder/wallow/commit/eedddfef5831a7f227b05d600c682d2f418ba914))
* **auth:** resolve the zone aliases in vite and vitest ([2dff070](https://github.com/bc-solutions-coder/wallow/commit/2dff070123cb5af3585349955235e44275666def))
* **branding:** client branding sub-resource, editor and live preview ([770513d](https://github.com/bc-solutions-coder/wallow/commit/770513dbac22c2247000829d3701480abd239e2f)), closes [#141](https://github.com/bc-solutions-coder/wallow/issues/141)
* **docker:** add optional newt tunnel client behind a pangolin profile ([3348597](https://github.com/bc-solutions-coder/wallow/commit/33485976d0ca0fbce76e07fda423138c502a7a14))
* **docker:** commit the secret-less production seed for git-based deploys ([85750c0](https://github.com/bc-solutions-coder/wallow/commit/85750c0b415b241f4ea5d4dfded094ab248b92a2))
* **docker:** declare pangolin resource via labels; profile-gate edges ([17eb019](https://github.com/bc-solutions-coder/wallow/commit/17eb019becac685dfa693a44cb99c0b31681fac0))
* **docker:** fail closed on missing production secrets, add a bootstrap script ([8dc960d](https://github.com/bc-solutions-coder/wallow/commit/8dc960de72fe29d92586fd94855a6e324959e30c))
* **docker:** seed no production admin; the setup page bootstraps it ([db8e762](https://github.com/bc-solutions-coder/wallow/commit/db8e76289eee57b06ef96b5f2406ec6fb73d376d)), closes [#109](https://github.com/bc-solutions-coder/wallow/issues/109)
* **docker:** split the pangolin newt edge into a standalone compose stack ([a725745](https://github.com/bc-solutions-coder/wallow/commit/a725745817fdbd0286dbca60f163152017673afd))
* **e2e:** isolate runs via per-run project, ports, tags (Wallow-joo0) ([72ce03c](https://github.com/bc-solutions-coder/wallow/commit/72ce03ceacee91b96c2038eb4d8625d99be1c35b))
* **e2e:** parameterize test-stack host ports, image tags and OIDC urls (Wallow-joo0) ([e6100e0](https://github.com/bc-solutions-coder/wallow/commit/e6100e000e40f491392d3098beb8e0bd8eb73b20))
* **env:** add @bc-solutions-coder/env and rewire the apps onto it ([188bfc9](https://github.com/bc-solutions-coder/wallow/commit/188bfc9e2c7024b1954db03660677f538a918c9e))
* **forms:** resolve submit failures through api-errors and the registry ([c492075](https://github.com/bc-solutions-coder/wallow/commit/c49207530ab604098849f25f645adfc8691d96b1)), closes [#183](https://github.com/bc-solutions-coder/wallow/issues/183)
* **hosting:** add WorkerRunOutcome for one-shot worker exit codes ([ffb7469](https://github.com/bc-solutions-coder/wallow/commit/ffb7469065fe2e19feba66c6423e4883f87bead0))
* **identity:** add membership approve, deny, suspend and reinstate ([60fd0e1](https://github.com/bc-solutions-coder/wallow/commit/60fd0e1ced6b85bb5640fdb4457261d9fc5bec3b))
* **identity:** add per-organization enrollment policy ([3fda0a6](https://github.com/bc-solutions-coder/wallow/commit/3fda0a6bb612e7dedbb794ef25a7964ba9538941))
* **identity:** add the access-requested integration event ([a53ccba](https://github.com/bc-solutions-coder/wallow/commit/a53ccba0b2107db7b5d48aa2d1b3a614dcd32a72))
* **identity:** add the Membership aggregate carrying per-org authorization ([aa86667](https://github.com/bc-solutions-coder/wallow/commit/aa866677fbbd091fe763a34ff2f91df7b1de5072))
* **identity:** add the membership repository ([d95b314](https://github.com/bc-solutions-coder/wallow/commit/d95b31406876c8c7593e73edc9adcd9b7d2bfbe2))
* **identity:** back-channel logout on the OP side ([96e61b2](https://github.com/bc-solutions-coder/wallow/commit/96e61b21582e97204fb62c18e98d5bd2f9902adf)), closes [#146](https://github.com/bc-solutions-coder/wallow/issues/146)
* **identity:** client-branded authorize-transaction screens ([a4fba6e](https://github.com/bc-solutions-coder/wallow/commit/a4fba6e437cf4459559cc4a034f4908efc113df3)), closes [#142](https://github.com/bc-solutions-coder/wallow/issues/142)
* **identity:** connected applications API with consent withdrawal ([6272e0a](https://github.com/bc-solutions-coder/wallow/commit/6272e0a25cbb4872d8a850e4f5dcb12d439559dd)), closes [#143](https://github.com/bc-solutions-coder/wallow/issues/143)
* **identity:** consent is a POST with a single-use token ([3127193](https://github.com/bc-solutions-coder/wallow/commit/3127193798b3c23fd599a5258eb765046f359669)), closes [#132](https://github.com/bc-solutions-coder/wallow/issues/132)
* **identity:** decide enrollment in a service, not a controller branch ([d9e42e9](https://github.com/bc-solutions-coder/wallow/commit/d9e42e97285772a29741ce97367aa0a42f3ae123))
* **identity:** describe every scope on the consent screen ([496eeea](https://github.com/bc-solutions-coder/wallow/commit/496eeea430791f63c47b85352272639eeac5ff0f))
* **identity:** drop identity.user_roles and give bootstrap an organization ([3a0099a](https://github.com/bc-solutions-coder/wallow/commit/3a0099acf5f2e02e55ab57b81b8e1d8fdb8ecdd6))
* **identity:** drop the no-op POST /v1/identity/setup/complete ([398d6eb](https://github.com/bc-solutions-coder/wallow/commit/398d6eba2a0a699a750f000b7267ee72f432eb1e)), closes [#109](https://github.com/bc-solutions-coder/wallow/issues/109)
* **identity:** emit audit events for every membership transition ([eb8f101](https://github.com/bc-solutions-coder/wallow/commit/eb8f10173d203674c33f4f2481a87ef2e179c2d6))
* **identity:** expose the RFC 7009 token revocation endpoint ([9a36a9d](https://github.com/bc-solutions-coder/wallow/commit/9a36a9db6cfe0506a89559245ee9a43e6eb0ae68))
* **identity:** first-party is a seed flag, not a client-id prefix ([ab9fb9e](https://github.com/bc-solutions-coder/wallow/commit/ab9fb9ece2dbcd6ae0a9d34d90779dc2092dfffc)), closes [#133](https://github.com/bc-solutions-coder/wallow/issues/133)
* **identity:** grant a client the scopes it may request ([7bbe3ad](https://github.com/bc-solutions-coder/wallow/commit/7bbe3ad21a146831dc265da9ed31500f06bffb46))
* **identity:** hint-only logout and session delete revoke tokens ([df99395](https://github.com/bc-solutions-coder/wallow/commit/df993955de2867b0cd0a49999e4f0e55206e392a)), closes [#158](https://github.com/bc-solutions-coder/wallow/issues/158)
* **identity:** invalid_client lockout and post-auth rate limiting ([0a49197](https://github.com/bc-solutions-coder/wallow/commit/0a4919740a9cdda286dff021ec111e644ad985fe)), closes [#150](https://github.com/bc-solutions-coder/wallow/issues/150)
* **identity:** keep an organization from losing its last owner ([fa80e5f](https://github.com/bc-solutions-coder/wallow/commit/fa80e5ffa3644908649987e67b54c05b82853c62))
* **identity:** let a denial expire instead of standing forever ([1ce5856](https://github.com/bc-solutions-coder/wallow/commit/1ce58565898442ee7113f0004280ed8f55352af3))
* **identity:** let a member leave an organization ([508febc](https://github.com/bc-solutions-coder/wallow/commit/508febc4bd8c505ae206e28a96bb00205b47fec4))
* **identity:** list suspended and denied memberships per organization ([c06797e](https://github.com/bc-solutions-coder/wallow/commit/c06797ee8ae849a3a7ddc82cf6778740bd82a3df))
* **identity:** narrow service-account endpoints from AdminAccess to per-action guards (Wallow-y74w) ([9003910](https://github.com/bc-solutions-coder/wallow/commit/900391074412c5e439b15b14bd791bd9c3e68c72))
* **identity:** notify relying parties of logout via OIDC front-channel ([9783132](https://github.com/bc-solutions-coder/wallow/commit/97831322abe05ffa078b4688aa499a49d96da36e))
* **identity:** org-scoped developer application registration ([f366c1b](https://github.com/bc-solutions-coder/wallow/commit/f366c1b55de7007896d0bd98734c0a64737b48b2)), closes [#135](https://github.com/bc-solutions-coder/wallow/issues/135)
* **identity:** organization deletion with revocation cascade ([e7d3861](https://github.com/bc-solutions-coder/wallow/commit/e7d3861dae2681693830631411cea0e65abd78c3)), closes [#140](https://github.com/bc-solutions-coder/wallow/issues/140)
* **identity:** organization hint and org-less first-party tokens ([d95f3ed](https://github.com/bc-solutions-coder/wallow/commit/d95f3eda718d1a1b1b9e11947848fdbdd30b8425)), closes [#134](https://github.com/bc-solutions-coder/wallow/issues/134)
* **identity:** per-client refresh-token lifetime with pinned refresh defaults ([5d0a71b](https://github.com/bc-solutions-coder/wallow/commit/5d0a71b1817b7b3ff92cabf5809207012b2eb40b)), closes [#144](https://github.com/bc-solutions-coder/wallow/issues/144)
* **identity:** persist consent on permanent authorizations with delta prompts ([d2760ee](https://github.com/bc-solutions-coder/wallow/commit/d2760eea0c690507713d1743852a6211a8ede407)), closes [#143](https://github.com/bc-solutions-coder/wallow/issues/143)
* **identity:** persist memberships and their per-org role assignments ([07c36f2](https://github.com/bc-solutions-coder/wallow/commit/07c36f2a54916905efa66a8d0f934e7c343cd6e5))
* **identity:** platform suspension of clients and organizations ([77b4dd9](https://github.com/bc-solutions-coder/wallow/commit/77b4dd94d3f2d0b53a2b1080a5d0eec355fbcc29)), closes [#139](https://github.com/bc-solutions-coder/wallow/issues/139)
* **identity:** refuse token issuance for a membership that is not active ([764f04c](https://github.com/bc-solutions-coder/wallow/commit/764f04c9841b19758a9bde1c2502875108162e21))
* **identity:** report the signed-in organization on userinfo ([4965ae1](https://github.com/bc-solutions-coder/wallow/commit/4965ae1ed2e902f043707e85a0f8444d5ced6613))
* **identity:** resolve role names per (user, organization) ([1d06b18](https://github.com/bc-solutions-coder/wallow/commit/1d06b18c8de349eb9c829fea17ff603b8e32ac27))
* **identity:** revoke access when a membership leaves active ([e03f668](https://github.com/bc-solutions-coder/wallow/commit/e03f668d85b2734f2f93e00acea093d70c3e68e1))
* **identity:** revoke tokens on end-session and user deactivation ([2d9e83b](https://github.com/bc-solutions-coder/wallow/commit/2d9e83bdfef2e1851881105d256cedf768c23793)), closes [#145](https://github.com/bc-solutions-coder/wallow/issues/145)
* **identity:** rotate client secrets with revoke and audit events ([079ccc9](https://github.com/bc-solutions-coder/wallow/commit/079ccc94bde54d019dbf72bc2ad764b56ed25adf)), closes [#137](https://github.com/bc-solutions-coder/wallow/issues/137)
* **identity:** seed explicit per-organization memberships and roles ([ec6681f](https://github.com/bc-solutions-coder/wallow/commit/ec6681f8f1b2452ed129fdcef64042869d3ee2e5))
* **identity:** seed how each organization admits people ([5f2371a](https://github.com/bc-solutions-coder/wallow/commit/5f2371af11de1697c84a5cd9e7166d452ae66c99))
* **identity:** service accounts on the org-scoped client surface ([d8e5073](https://github.com/bc-solutions-coder/wallow/commit/d8e5073f61642c328344ab7bd96d9d78483bdfcd)), closes [#136](https://github.com/bc-solutions-coder/wallow/issues/136)
* **identity:** stop granting membership from anonymous registration ([4b7b45f](https://github.com/bc-solutions-coder/wallow/commit/4b7b45f7110836256f4140c46564fac3be83d595))
* **identity:** suspend, reinstate and delete organization clients ([afe615d](https://github.com/bc-solutions-coder/wallow/commit/afe615d8b19d2fa0a6b44fa9fe7db55750945b8c)), closes [#138](https://github.com/bc-solutions-coder/wallow/issues/138)
* **identity:** tell a person which organizations they belong to ([137f960](https://github.com/bc-solutions-coder/wallow/commit/137f9600e3c636b0977a461e453382876425c96a))
* **identity:** transaction-scoped authorize-context, drop anonymous client reads ([e79d90d](https://github.com/bc-solutions-coder/wallow/commit/e79d90dbb3e18c3c75c52e220f6fc1bac01cb71f)), closes [#142](https://github.com/bc-solutions-coder/wallow/issues/142)
* **lint:** ban Node built-ins in logger's browser entry (Wallow-lgto) ([8a087e4](https://github.com/bc-solutions-coder/wallow/commit/8a087e483d94de9186a273bb1199ba917be9dade))
* **lint:** keep a library's four module lists in sync with wallow/module-lists-in-sync ([c396001](https://github.com/bc-solutions-coder/wallow/commit/c39600124cc8f2a9ba289c3369d1caf4b46be8b2))
* **logger:** add @bc-solutions-coder/logger and wire both apps ([b03d7ea](https://github.com/bc-solutions-coder/wallow/commit/b03d7ea013bb3f8248f64b5812fbc02c3449b0b1))
* **minimal-app:** external RP example and three-origin acceptance ([14382ed](https://github.com/bc-solutions-coder/wallow/commit/14382ed67b903570f2954ff50e9d7a977ca5dbeb)), closes [#151](https://github.com/bc-solutions-coder/wallow/issues/151)
* **navigation:** extract the app shell into @bc-solutions-coder/navigation ([53da0f7](https://github.com/bc-solutions-coder/wallow/commit/53da0f7b41092033f2b1391b6a16a6248d34e038))
* **notifications:** email an organization's reviewers when someone requests access ([e093db1](https://github.com/bc-solutions-coder/wallow/commit/e093db14695594900c4a82c706574bf4ebe95611))
* **observability:** export the wolverine runtime meter ([2e7dd62](https://github.com/bc-solutions-coder/wallow/commit/2e7dd625d93a84f54b357465c0a617d072d4ed90))
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
* **seeder:** bootstrap the admin through the setup command ([775c151](https://github.com/bc-solutions-coder/wallow/commit/775c151734f287a279cf187ac909390b471c03b0)), closes [#109](https://github.com/bc-solutions-coder/wallow/issues/109)
* **seeder:** key injected client secrets by clientId instead of index ([6fa76fb](https://github.com/bc-solutions-coder/wallow/commit/6fa76fb3ef9515f81d13136795037e016957dbc7))
* **seeder:** remove the bcordes client seeding ([ce16611](https://github.com/bc-solutions-coder/wallow/commit/ce166116ac33a96a012ef845f4d297eaf569133a)), closes [#111](https://github.com/bc-solutions-coder/wallow/issues/111)
* **storage:** sweep orphaned objects left by failed commits ([e143f9a](https://github.com/bc-solutions-coder/wallow/commit/e143f9aa9e481cfd9172307f76a4aa870916db3a))
* **styles:** add sidebar and success semantic tokens ([165bc6a](https://github.com/bc-solutions-coder/wallow/commit/165bc6a215c810942f8c61e057fb76e46dbd2f33))
* **styles:** add the warning token pair ([e11dd02](https://github.com/bc-solutions-coder/wallow/commit/e11dd02da044022f6f67252372479991b2e73fa6))
* **styles:** resolve the fork's outbound links from the environment ([8f3036d](https://github.com/bc-solutions-coder/wallow/commit/8f3036d975e2e9ee7f8a6b1742762a5433f2595f))
* **testing:** add console guard with consume-based error assertions ([b419be5](https://github.com/bc-solutions-coder/wallow/commit/b419be5040fecc1dc0e064d7038fc90df1232844))
* **testing:** add consume-based hand-off assertions and wire wallow-auth navigation guard ([26be562](https://github.com/bc-solutions-coder/wallow/commit/26be562fe21e9d34527e02f5db944deeb2bf7e08))
* **testing:** add network-escape guard blocking unharnessed fetch ([39b03f8](https://github.com/bc-solutions-coder/wallow/commit/39b03f880d8256eee8ad410e16494dfb41d44c2c))
* **testing:** root route options on renderWithWallow ([f6cde6d](https://github.com/bc-solutions-coder/wallow/commit/f6cde6de05d2b3f4177ba227cc3f543a9c651501)), closes [#142](https://github.com/bc-solutions-coder/wallow/issues/142)
* **ui,query:** failure surfaces — toast, banner, provider, client callback ([3d3533a](https://github.com/bc-solutions-coder/wallow/commit/3d3533a0d1714f9b16fba9a23c5e0206af567419)), closes [#182](https://github.com/bc-solutions-coder/wallow/issues/182)
* **ui:** add CardHeader ([639d1ab](https://github.com/bc-solutions-coder/wallow/commit/639d1aba71f16a66d73e88cd4d1ba5fb257efffe))
* **ui:** add NoticeBanner for success and warning notices ([44b98d7](https://github.com/bc-solutions-coder/wallow/commit/44b98d76ac3b11e10c4a5ce18eb71dbbcda2c69e))
* **ui:** add QuietLink for muted footer and back links ([488a8ff](https://github.com/bc-solutions-coder/wallow/commit/488a8ffedcefd95abcc2586baff7e1f949715372))
* **ui:** add the EmptyState component ([72f665b](https://github.com/bc-solutions-coder/wallow/commit/72f665bd70bf23940280937600c13442e3914a23))
* **ui:** add the PageHeader component ([e3e84aa](https://github.com/bc-solutions-coder/wallow/commit/e3e84aaa2e18b035a224c28180dc46fb53aa63f5))
* **ui:** add the Text component ([58455d5](https://github.com/bc-solutions-coder/wallow/commit/58455d59f0fda8f3a85d0293f1bc4ca890213b28))
* **ui:** adopt 20px (text-xl) as the catalog-wide card-heading standard ([dd49f14](https://github.com/bc-solutions-coder/wallow/commit/dd49f14aab112254cedae56819fd764835f5b158))
* **ui:** shared branded header with organization attribution ([2e5888b](https://github.com/bc-solutions-coder/wallow/commit/2e5888bd38b3a0eeae4a85067b5ed157b2c49553)), closes [#142](https://github.com/bc-solutions-coder/wallow/issues/142)
* **ui:** upgrade the Button recipe with outline/ghost/link, sizes, and focus states ([741aa22](https://github.com/bc-solutions-coder/wallow/commit/741aa223df8c9e328eccbfa3fc7470d00313a1c7))
* **utils:** add @bc-solutions-coder/utils and rewire the apps onto it ([bc7c5c7](https://github.com/bc-solutions-coder/wallow/commit/bc7c5c734302c7888def234445c5cbce846345a7))
* **wallow-auth:** add the AuthScreen shell ([df8fbea](https://github.com/bc-solutions-coder/wallow/commit/df8fbea18b8cc626af20724816711a1e192574af))
* **wallow-auth:** add the first-run /setup page ([2c3a60a](https://github.com/bc-solutions-coder/wallow/commit/2c3a60aa756b5b240e055494fa87b05e105d3934)), closes [#109](https://github.com/bc-solutions-coder/wallow/issues/109)
* **wallow-auth:** add the shared useReturnUrlGuard hook ([c09097a](https://github.com/bc-solutions-coder/wallow/commit/c09097a282205f974c027c6acd4ac3a3d895be07))
* **wallow-auth:** give a pending join request its own screen ([c18c21c](https://github.com/bc-solutions-coder/wallow/commit/c18c21ca9acd10f110d593a5bae4f8a9a97b1bb4))
* **wallow-auth:** send a signed-in user to the web app ([f65d173](https://github.com/bc-solutions-coder/wallow/commit/f65d173cfe8743fd4ec2c06159b661f3cc107d0c))
* **wallow-web:** add member role management screen with own-org gate ([c719272](https://github.com/bc-solutions-coder/wallow/commit/c71927287ac081a579c3d26e7cdc64c6d1dcd798))
* **wallow-web:** add member-facing leave-organization screen ([1175333](https://github.com/bc-solutions-coder/wallow/commit/1175333ff043a05a651e691364a94debb69c027a))
* **wallow-web:** add pending-request review and invitation screens ([d2ebf1e](https://github.com/bc-solutions-coder/wallow/commit/d2ebf1edc85201ffdde4ff33e3998e0dc050f624))
* **wallow-web:** migrate org-detail route to $orgId directory form ([b1479af](https://github.com/bc-solutions-coder/wallow/commit/b1479afe2664f8935ec9f2371c81313a0d34e9df))
* **wallow-web:** migrate read and mutation sites onto the failure surfaces ([08e0abc](https://github.com/bc-solutions-coder/wallow/commit/08e0abc2a9095f1f5e97a56c58f63c5fe71139c0))
* **web:** add an oxlint gate enforcing the catalog migration ([a315558](https://github.com/bc-solutions-coder/wallow/commit/a315558b2ae40f1411e71f05b50f307de90d4dd7))
* **web:** connected applications settings card with withdraw ([73ab846](https://github.com/bc-solutions-coder/wallow/commit/73ab8465301eb0f42c1c5a257415d63ae527241e)), closes [#143](https://github.com/bc-solutions-coder/wallow/issues/143)
* **web:** migrate wallow-web page shells, lists, text, and forms onto the catalog ([9c20721](https://github.com/bc-solutions-coder/wallow/commit/9c20721548dd4902a54fe6b2c994f3df3265e60d))
* **web:** name every unlabelled control and drop the bare textarea ([2acfab0](https://github.com/bc-solutions-coder/wallow/commit/2acfab0110e88459779c0c09d1713c242f3a93d7))
* **web:** navigate the apps register CTA client-side ([9bdb6a0](https://github.com/bc-solutions-coder/wallow/commit/9bdb6a0c412b7adc5a89fa85fd82343931aa0028))
* **web:** navigate to detail routes from list rows ([ebe0def](https://github.com/bc-solutions-coder/wallow/commit/ebe0def2c109af3389ba31c86428ad0b486cfc26))
* **web:** pin heading variants with text-heading-variant ([a480973](https://github.com/bc-solutions-coder/wallow/commit/a4809732b029c1448e3185d9fd4e03b2350fb971))
* **web:** render an error state for every query ([a1bce54](https://github.com/bc-solutions-coder/wallow/commit/a1bce54744d910af4f7e1e164bbd4d624846ec55))
* **web:** resolve org members by email in a searchable picker ([e4d8659](https://github.com/bc-solutions-coder/wallow/commit/e4d86591044e758d6eb884c94990c3ddfc89dffa))
* **web:** sweep the last raw text elements out of bff-demo ([a94cb9d](https://github.com/bc-solutions-coder/wallow/commit/a94cb9de79616fdbd4dcdf1b330f24f04fed4469))
* **web:** wire membership screens into nav and org detail ([09eb4d7](https://github.com/bc-solutions-coder/wallow/commit/09eb4d73639b12d1df23541e9f034f02e2c5f32b))


### Bug Fixes

* **announcements:** remove plan targeting and the claim nothing issues ([dbf2d2b](https://github.com/bc-solutions-coder/wallow/commit/dbf2d2baa639c232e1e855b53d7008a82afdd2fe))
* **api:** answer browser error navigations with problem+json, not downloads ([7f4f16e](https://github.com/bc-solutions-coder/wallow/commit/7f4f16ef65eaf6e44384259a14d9be0b0a81e7a4)), closes [#109](https://github.com/bc-solutions-coder/wallow/issues/109)
* **api:** collapse each module's schema name to one constant ([0f3e3b0](https://github.com/bc-solutions-coder/wallow/commit/0f3e3b00a1e2136de2a87065789623be9eb19921))
* **api:** commit handler writes and cascaded messages together ([a2eec93](https://github.com/bc-solutions-coder/wallow/commit/a2eec93ca37b4962cfae81eb5abdfa980e3924c9))
* **api:** give each handler its own chain and retry loop ([4800cd6](https://github.com/bc-solutions-coder/wallow/commit/4800cd6ff8b1b6267770579f4e2bef06bfc3ebd3))
* **api:** pin the Wolverine application assembly explicitly ([3c7d161](https://github.com/bc-solutions-coder/wallow/commit/3c7d161b3f3cf099c9c527ffbf063ebeb21305c7))
* **api:** stop routing a disabled module's controllers ([4f9b0f1](https://github.com/bc-solutions-coder/wallow/commit/4f9b0f1051a4af3974f8267ebffe01a5fb0d8e8e))
* **apps:** name server-only modules *.server.* so import protection fires ([3d5d687](https://github.com/bc-solutions-coder/wallow/commit/3d5d687008c81ef14254ec386ed660f636093318))
* **auth:** standardize wallow-auth card headings to one 16px scale ([23408a9](https://github.com/bc-solutions-coder/wallow/commit/23408a9cc434fb4db11e9685b7e5b4d22a4d3f48))
* **branding:** bypass the tenant filter in the client-branding lookup ([a284b02](https://github.com/bc-solutions-coder/wallow/commit/a284b021adc0dec1a647a85dffe627c36f23227e))
* **branding:** make the display-name sync convergent and the publish atomic with the save ([5d5bab2](https://github.com/bc-solutions-coder/wallow/commit/5d5bab2b056a05c249c371a3112e801ba43420bd)), closes [#154](https://github.com/bc-solutions-coder/wallow/issues/154)
* **branding:** retry the upsert as an update when it loses the registration race ([8204246](https://github.com/bc-solutions-coder/wallow/commit/8204246e0d2350d681f18fa4b5a36c29e79c4fcd)), closes [#153](https://github.com/bc-solutions-coder/wallow/issues/153)
* **build:** run build before typecheck and test in the check gate ([de08d2a](https://github.com/bc-solutions-coder/wallow/commit/de08d2a68ada87c03edcd4dbc1494e4da38ed8fd))
* **ci:** build React images natively and cap docker-images-app at 25 min ([ff7ca9d](https://github.com/bc-solutions-coder/wallow/commit/ff7ca9d07e55d899794236aaebb596dbe5eed63a))
* **config:** vendor an ESM with-selector so SSR stops loading a second React ([769c1ce](https://github.com/bc-solutions-coder/wallow/commit/769c1cec870ad0f66f7cc6a6b7695f5634faeab5))
* **deps:** bump five transitive packages past their Dependabot advisories ([ea9fe78](https://github.com/bc-solutions-coder/wallow/commit/ea9fe789cf178bb20006ba82b1cd4176e629134d))
* **docker:** copy api-errors into the app image builds ([4ea25f2](https://github.com/bc-solutions-coder/wallow/commit/4ea25f29253f1995ad874480b72c5be4a45c7755))
* **docker:** make the COOKIE_PASSWORDS rotation example non-destructive ([84eb7df](https://github.com/bc-solutions-coder/wallow/commit/84eb7df7407481c4fa46cdef97179d1fb31eb426))
* **docker:** route the pangolin profile by subdomain, not path ([a6196ad](https://github.com/bc-solutions-coder/wallow/commit/a6196ad6e8ea42f692a34919ea351b0724311767))
* **docs:** restore the frontend state boundary the CLAUDE.md split dropped ([99d090b](https://github.com/bc-solutions-coder/wallow/commit/99d090b419d1fc10a1e55b76e60a64c9f4653e74))
* **e2e:** pass PORT through wallow-auth's playwright webserver ([8354467](https://github.com/bc-solutions-coder/wallow/commit/83544670a164c3b88d0b8f525865992bc3ecf31d))
* **e2e:** rebuild app images so a run tests the current tree ([14467ee](https://github.com/bc-solutions-coder/wallow/commit/14467eeffe27f5d5e910853d135b9fe35ff3d0fb))
* **e2e:** wait for the post-logout landing before asserting the 401 ([4908212](https://github.com/bc-solutions-coder/wallow/commit/4908212a6b7a5a9e101b2da2abffd639eb8b14d4))
* **env:** gate x-forwarded-proto on the trusted-proxy set (Wallow-cybh) ([f69ecd4](https://github.com/bc-solutions-coder/wallow/commit/f69ecd4f64f244601a59e0e60f3fad64db776cb6))
* **forms:** stable reset memo and honest deprecation pointers ([7aefb10](https://github.com/bc-solutions-coder/wallow/commit/7aefb107ded1eec937216036190fc2b01c70dfd1)), closes [#183](https://github.com/bc-solutions-coder/wallow/issues/183)
* **identity:** address back-channel logout review findings ([8fa6023](https://github.com/bc-solutions-coder/wallow/commit/8fa6023a4ef4829a44fd24dd69e8ca1fe4ea1986))
* **identity:** bind invitation acceptance to the invited verified email and create the membership ([ffe56f8](https://github.com/bc-solutions-coder/wallow/commit/ffe56f84fffd430f8c373f946724e767ea4840e2))
* **identity:** bootstrap admin joins the seeded organization ([c6a0f57](https://github.com/bc-solutions-coder/wallow/commit/c6a0f579d30a498addfe594089060b39dc54f59a))
* **identity:** carry an invitation returnUrl through registration ([2cc7074](https://github.com/bc-solutions-coder/wallow/commit/2cc7074811d69483a00cca33fa3008a628999cf5))
* **identity:** carry tenant identity on org_id alone ([2265cdf](https://github.com/bc-solutions-coder/wallow/commit/2265cdfda1e31fb54ecc1da88c057d0739d7f9ff))
* **identity:** evaluate MFA exemption against every active membership ([c47959e](https://github.com/bc-solutions-coder/wallow/commit/c47959e79100f747758eb8f4c26a27b87abd8357))
* **identity:** gate cross-organization access on per-org permissions ([73db37b](https://github.com/bc-solutions-coder/wallow/commit/73db37b1935ff7fc771a29b8da1a4e9413b99442))
* **identity:** grant the scopes a caller is entitled to instead of refusing the request ([14f77af](https://github.com/bc-solutions-coder/wallow/commit/14f77af4a76e599062e84c4d06ccc7a5ebaad6eb))
* **identity:** guard consent chaining and refuse prompt=none without a session ([e2c0597](https://github.com/bc-solutions-coder/wallow/commit/e2c0597a69251b5cf32eda8545dafcfb03aeb65d)), closes [#143](https://github.com/bc-solutions-coder/wallow/issues/143)
* **identity:** issue org-scoped roles from the authorize endpoint ([8071315](https://github.com/bc-solutions-coder/wallow/commit/8071315cbeb2200093f7b12d9cbafdc03ac6be6d))
* **identity:** keep global role claims out of the auth cookie ([f2c9690](https://github.com/bc-solutions-coder/wallow/commit/f2c9690acbd9f3bd7ce8a173164aed11cde5c07a))
* **identity:** keep one live invitation per email and organization ([9dc7cd7](https://github.com/bc-solutions-coder/wallow/commit/9dc7cd782c781e7d9c79928b3592cc003901c564))
* **identity:** keep the authorize request when a consent post is anonymous ([a178bb7](https://github.com/bc-solutions-coder/wallow/commit/a178bb78dc4787fe50025d4f5dc87027dcd87c2f)), closes [#132](https://github.com/bc-solutions-coder/wallow/issues/132)
* **identity:** publish client lifecycle events through the outbox ([7b7b685](https://github.com/bc-solutions-coder/wallow/commit/7b7b68554c75bc4e7b1ccb9f0f90a4ac6e6c5824)), closes [#162](https://github.com/bc-solutions-coder/wallow/issues/162)
* **identity:** publish ClientRegisteredEvent through the outbox ([bab5e49](https://github.com/bc-solutions-coder/wallow/commit/bab5e49fd2b1c369fdb62fa130176189a294fe7a)), closes [#161](https://github.com/bc-solutions-coder/wallow/issues/161)
* **identity:** re-check membership status on the refresh grant ([14e4eba](https://github.com/bc-solutions-coder/wallow/commit/14e4ebab611d968286ce98e61affdeca9494cff8))
* **identity:** record who granted membership and roles, not the subject ([ac214e1](https://github.com/bc-solutions-coder/wallow/commit/ac214e108863e4b76e1328886f0e726c03c4cce9))
* **identity:** register ILastOwnerGuard in the seeder's DI container ([5a26b0c](https://github.com/bc-solutions-coder/wallow/commit/5a26b0c0f6735c5b44cee5a6881ff03ba7d47ea8))
* **identity:** reject invitation acceptance from an unverified registration ([b5b8044](https://github.com/bc-solutions-coder/wallow/commit/b5b804433a9e0e8957cd8d1d089d25f842d2da46))
* **identity:** resolve invitations outside the ambient tenant ([6726b5d](https://github.com/bc-solutions-coder/wallow/commit/6726b5d498c62b58015cc94c7e23d7401daf65cc))
* **identity:** resolve org-scoped roles on the refresh grant ([728a70a](https://github.com/bc-solutions-coder/wallow/commit/728a70a049505261cb3e6f507214762d33274c9b))
* **identity:** scope invitation queries on their own parameters ([08fb7ff](https://github.com/bc-solutions-coder/wallow/commit/08fb7ff23f67b5c12f785a4aff2d71cdb9ff987b))
* **identity:** stop permission expansion granting across or without a tenant ([0892647](https://github.com/bc-solutions-coder/wallow/commit/0892647f9acae626b1f08d854beb5e54bffac25b))
* **identity:** validate enrollment requests on the constructor parameter ([fb0d5a7](https://github.com/bc-solutions-coder/wallow/commit/fb0d5a74a78c5cea5b66ed3b0381dd96df36e083))
* **identity:** write role assignments to memberships, not the global role store ([58e11f7](https://github.com/bc-solutions-coder/wallow/commit/58e11f7c72787862e2bd4eff2403eb9669a9819a))
* **lint:** register the wallow/* plugin in navigation, ui and forms ([d600e25](https://github.com/bc-solutions-coder/wallow/commit/d600e25f4566047adfe67a944fddfd71c2315a05))
* **logger:** derive the ingest client IP from the host, not a header ([d343968](https://github.com/bc-solutions-coder/wallow/commit/d343968eb9622cf353b13203aef62b8697b27f8e))
* **migrations:** exit non-zero when a migration fails ([2bef239](https://github.com/bc-solutions-coder/wallow/commit/2bef239e2907436be0fcd3c63f6bde54303c440c))
* **notifications:** make the preference checker public so sends generate ([f62c221](https://github.com/bc-solutions-coder/wallow/commit/f62c2214c922c3c2ad11ffa0884d78117d5e9a56))
* **observability:** point the Node logger at Alloy's HTTP port ([e4f3616](https://github.com/bc-solutions-coder/wallow/commit/e4f361642f7c6cabed68f5137d4bcca5bf96e781))
* **packages:** declare files[] so dist actually packs ([184c86a](https://github.com/bc-solutions-coder/wallow/commit/184c86a3fbdaafe9ee4d67057d9e4db2d3f4277d))
* reject an unknown run-tests.sh target instead of running nothing ([32d6eb1](https://github.com/bc-solutions-coder/wallow/commit/32d6eb1e414db356481c9cfaff84329d9a617f26))
* replace stale localhost:5000 fallbacks with the real service urls ([74be8e8](https://github.com/bc-solutions-coder/wallow/commit/74be8e8d7e93298bbe92962006aa45e295298995))
* resolve the client address through a trusted-proxy check ([781041f](https://github.com/bc-solutions-coder/wallow/commit/781041f8a88e1bd451d8050533e18840aab74201))
* **scripts:** make run-tests.sh reach every integration assembly ([5d03093](https://github.com/bc-solutions-coder/wallow/commit/5d0309315b6b19fcec343daece862638e7c9c2c3))
* **sdk:** read a blank COOKIE_NAME as unset, and surface the BFF's remaining knobs ([3520ff1](https://github.com/bc-solutions-coder/wallow/commit/3520ff155049328e3b4505abfce842759854424e))
* **seeder:** exit non-zero when a seed step fails ([62bcb9e](https://github.com/bc-solutions-coder/wallow/commit/62bcb9e262421bdc2c54a1e85ddb5ba860052e02))
* **storage:** commit row removals before deleting stored objects ([12a99a4](https://github.com/bc-solutions-coder/wallow/commit/12a99a4ec4d3c59b82d7b8dc4adc624589f8a83d))
* **storage:** enforce the three storage settings on both upload paths ([75104a8](https://github.com/bc-solutions-coder/wallow/commit/75104a84d929c11ad956ba64f6bfa12f029e01d3))
* **storage:** replace broken async presigned-upload scan with a completion endpoint ([ddca318](https://github.com/bc-solutions-coder/wallow/commit/ddca318d3ba63dc9cd5dd41ad904d4fb7a41f63e))
* **storage:** serve local presigned URLs from real signed endpoints ([57e6eb9](https://github.com/bc-solutions-coder/wallow/commit/57e6eb9bba28603c34d5dcc4735fe7cba4e320bd))
* **styles:** serve the fork theme as a virtual stylesheet for test harnesses ([1eba844](https://github.com/bc-solutions-coder/wallow/commit/1eba8446399f0b808cee0a553789359c50edd5c9))
* **testing:** emit types for the console and network guards ([a8892f8](https://github.com/bc-solutions-coder/wallow/commit/a8892f86d2578ee42ec49e0592ad4f21037fabec))
* **ui:** add a surface axis to fix illegible sidebar-composed controls ([eca77a5](https://github.com/bc-solutions-coder/wallow/commit/eca77a5e5ad7be3f4aa9a09556f873b5e4c3bbaa))
* **ui:** fix contrast on the sidebar navigation menu trigger ([f5e3d92](https://github.com/bc-solutions-coder/wallow/commit/f5e3d92dcd13e5f49ca7bbd718e22b7fff4b5f83))
* **ui:** let composed anchors announce as links, not buttons ([de9dd46](https://github.com/bc-solutions-coder/wallow/commit/de9dd46764df2dec23699a78144d5ca8b84c2c8c))
* **ui:** stamp document.documentElement for scheme-scoped stories ([671d5b8](https://github.com/bc-solutions-coder/wallow/commit/671d5b8e926b160a68229bb8e15e3cd5028109ed))
* **wallow-web:** settle the heading-scale spec on its loaded sections ([a912511](https://github.com/bc-solutions-coder/wallow/commit/a91251195309ee308396fb61db1a0209f2fff535))
* **web,auth:** keep one React Query instance in the SSR bundle ([185f9bb](https://github.com/bc-solutions-coder/wallow/commit/185f9bba401e354c35e9bd31a6c61ee2ad402aad))
* **web:** apply register-app branding as a post-register upsert ([fec26f9](https://github.com/bc-solutions-coder/wallow/commit/fec26f9fe01eeda173962c270b3e024aa53d8b25))
* **web:** replace the typography guard's regex comment-stripper ([0f263f9](https://github.com/bc-solutions-coder/wallow/commit/0f263f99b390ff2860630cc32341d455e4e1b7ca))
* **web:** resolve the mobile-nav SSR flash via a CSS breakpoint ([69aea1b](https://github.com/bc-solutions-coder/wallow/commit/69aea1b0cabbed2fb0a6bae176f52b8a792cfcca))


### Performance Improvements

* **ci:** drop analyzers from the openapi emission build ([f181ffb](https://github.com/bc-solutions-coder/wallow/commit/f181ffbc79f27695ad1a08bc272a668d9107eb62))
* **ci:** path-filter codeql and drop analyzers from its build ([b0fa33b](https://github.com/bc-solutions-coder/wallow/commit/b0fa33beda303766ffbfd942880420b69b74fbb7))
* **ci:** restore mtimes so the build cache is incremental ([c4b8dc8](https://github.com/bc-solutions-coder/wallow/commit/c4b8dc83338fb85903ec1ae2138f2fc1b2ac3557))
* **ci:** route route-tree-drift builds through the turbo remote cache ([f68afc3](https://github.com/bc-solutions-coder/wallow/commit/f68afc3001d2999958b85abc24410092eeff13ba))
* **ci:** route sdk-publish build+test through the turbo remote cache ([6d2fe00](https://github.com/bc-solutions-coder/wallow/commit/6d2fe007f84866639217c7e9565ed24946cd8b8a))


### Code Refactoring

* **identity:** drop OrgMemberRole in favour of the shared role catalog ([2dbd4d7](https://github.com/bc-solutions-coder/wallow/commit/2dbd4d7859d14f610085568bb53e539721a56697))
* **identity:** drop the frozen home tenant from WallowUser ([77bf14c](https://github.com/bc-solutions-coder/wallow/commit/77bf14cf2dd93916860763620e2a9dc6e313db18))
* **identity:** read and write organization members through memberships ([d1eaa3d](https://github.com/bc-solutions-coder/wallow/commit/d1eaa3d57a8527808338f7b58655f26d19b3a44e))
* **sdk:** delete imperative login() and getUser() browser helpers ([42035fc](https://github.com/bc-solutions-coder/wallow/commit/42035fc231768c3546fc9811803e0db8a6133fd0))
* **sdk:** delete the browser claim-bag readers; one typed user model ([722a598](https://github.com/bc-solutions-coder/wallow/commit/722a598ce5cf753909a13b63ae8b9de87443651c))
* **sdk:** delete the module-scope CSRF token store; add a csrf opt-out ([6603cf7](https://github.com/bc-solutions-coder/wallow/commit/6603cf7b46fff66b7ea2d636f7dbe57544b6fe74))
* **sdk:** delete the unadopted WallowRouterContext interface ([107aba2](https://github.com/bc-solutions-coder/wallow/commit/107aba2dea9ec3ab33d997a338ce7cdc0524ceaf))


### Build System

* move the workspace to pnpm 11.24.0 ([0f6ac76](https://github.com/bc-solutions-coder/wallow/commit/0f6ac76334752d06815589db5c8760ddd093b8f4))

## [4.0.0](https://github.com/bc-solutions-coder/wallow/compare/v3.2.1...v4.0.0) (2026-07-26)


### ⚠ BREAKING CHANGES

* **identity:** removes the ClientRegistration, InitialAccessTokens, MembershipRequests, OrganizationDomains, Scim, and Sso API surfaces along with their DTOs, entities, and database tables.
* **identity:** the auth UI is now exclusively apps/wallow-auth (port 3002 in dev). The wallow-auth container image is built from apps/wallow-auth/Dockerfile. The .NET Playwright E2E suite (Wallow.E2E.Tests) and scripts/run-e2e.sh are removed; per-app @playwright/test suites replace them. Blazor behaviour remains readable in git history.
* **deps:** upgrade WolverineFx to 6.19.0
* **deps:** upgrade Asp.Versioning to 10.0.0
* **deps:** upgrade StackExchange.Redis to 3.0.17
* **sdk:** refresh under store.withRefreshLock to protect rotating tokens
* **sdk:** inject SessionStore into createBffHandlers and destroy session on logout
* **sdk:** route readSession/writeSession through an injected SessionStore
* **sdk:** add sessionId, version, csrfToken, and identity fields to BffSession

### Features

* configurable first-party clients and @bc-solutions-coder/sdk TypeScript SDK with BFF tunnel ([#63](https://github.com/bc-solutions-coder/wallow/issues/63)) ([3fa193e](https://github.com/bc-solutions-coder/wallow/commit/3fa193e440c9838e31f34d4b30d59bfb830bb18a))
* **examples:** add minimal-app reference scaffold ([1a3e485](https://github.com/bc-solutions-coder/wallow/commit/1a3e48550400825b2625f336c68135b550bb7bff))
* **identity,web:** resolve oidc issuer and authority at the unified auth origin ([1bfaafa](https://github.com/bc-solutions-coder/wallow/commit/1bfaafa54b3b62d11faf401c6cecdfd23e6d7597))
* **identity:** repoint the dev auth origin onto wallow-auth ([4455633](https://github.com/bc-solutions-coder/wallow/commit/44556335b75a9285176ed1d921662d12a6834402))
* **sdk:** absorb csrf, ssr context, and facade helpers ([a1f64f6](https://github.com/bc-solutions-coder/wallow/commit/a1f64f608ed94d1e967bc309c86dc983319ab2b8))
* **sdk:** add apps query module ([e020264](https://github.com/bc-solutions-coder/wallow/commit/e02026443adc91a1c0bec2196c7a74c8fb9a9541))
* **sdk:** add auth facade and oidc helpers for tanstack auth app ([5d761ea](https://github.com/bc-solutions-coder/wallow/commit/5d761eabab40a0e4e6f1ddc99049ae3f6e4c600a))
* **sdk:** add auth query module ([182881f](https://github.com/bc-solutions-coder/wallow/commit/182881f3788d9c72330033a7ec2e1f44f494ddf0))
* **sdk:** add central query-key factory ([0ebad6f](https://github.com/bc-solutions-coder/wallow/commit/0ebad6fe484072c24f04b9a0631ca031aa71d4ee))
* **sdk:** add configureBffClient and fix two-client config disconnect ([1c3577e](https://github.com/bc-solutions-coder/wallow/commit/1c3577e399b7c0b18d248e72618b7d06dc0d4a05))
* **sdk:** add CookieSessionStore behind SessionStore interface ([0120dc1](https://github.com/bc-solutions-coder/wallow/commit/0120dc146a3e1f9916774fd1629523467a26d00e))
* **sdk:** add CSRF protection to BFF session and proxy ([d5f3b0b](https://github.com/bc-solutions-coder/wallow/commit/d5f3b0b0f54fd7fa2a985ff5ccd1744a81ee20d2))
* **sdk:** add getCurrentUser auth-state facade method ([ddb02f3](https://github.com/bc-solutions-coder/wallow/commit/ddb02f37da7ffc1fcf57d345e9905d68cefb3c65))
* **sdk:** add inquiries query module ([e844369](https://github.com/bc-solutions-coder/wallow/commit/e84436918b108df8310f822f99a04d1e9ca113bd))
* **sdk:** add lazy query-layer bootstrap seam ([565627b](https://github.com/bc-solutions-coder/wallow/commit/565627b4c39041d78cc099fa56fd3e060fb9c3e4))
* **sdk:** add mfa query module ([fabc046](https://github.com/bc-solutions-coder/wallow/commit/fabc046a219afd92a94bb7fde3ee8cb5a5669fc9))
* **sdk:** add organizations query module ([e1891f8](https://github.com/bc-solutions-coder/wallow/commit/e1891f80b031715e4be352cae83ec71c08636155))
* **sdk:** add RFC 7807 error parsing and resilient BFF proxy forward ([48e41bb](https://github.com/bc-solutions-coder/wallow/commit/48e41bb0993ff5d1bd16de1e8eaf4712ef57c793))
* **sdk:** add RP-initiated logout with connect/logout fallback ([8c9887e](https://github.com/bc-solutions-coder/wallow/commit/8c9887ee89b1f49d3a0e8e30a70b8aebd1563af5))
* **sdk:** add session TTL and cookie secure configuration ([a37a46e](https://github.com/bc-solutions-coder/wallow/commit/a37a46e36e1b0fe3e4b4e78fc152985d2dd1e93c))
* **sdk:** add sessionId, version, csrfToken, and identity fields to BffSession ([7cbd230](https://github.com/bc-solutions-coder/wallow/commit/7cbd23008a41da324f6373ba604db495b838d02e))
* **sdk:** add SessionStore and RedisLike interfaces ([e61fcb2](https://github.com/bc-solutions-coder/wallow/commit/e61fcb21734e6881bded1b3b91f6704603b25471))
* **sdk:** add settings query module ([c755ab5](https://github.com/bc-solutions-coder/wallow/commit/c755ab566f2e7af5da8a4716814d3f55abc75d29))
* **sdk:** add user query module ([13d8d91](https://github.com/bc-solutions-coder/wallow/commit/13d8d91019f03b895e9716acfe5ea83410f3c9a6))
* **sdk:** add ValkeySessionStore with server-side revocation and refresh lock ([a336973](https://github.com/bc-solutions-coder/wallow/commit/a336973a85e1d3ced72a993c9e900c2d9206259f))
* **sdk:** adopt openid-client for PKCE authorize URL and code exchange ([9a503bd](https://github.com/bc-solutions-coder/wallow/commit/9a503bd31b63724012e3717e30c04f5e1916155d))
* **sdk:** back OIDC discovery with openid-client, keep split-horizon pinning ([487989a](https://github.com/bc-solutions-coder/wallow/commit/487989af7990db71ec6abdd3c2f046338b35a711))
* **sdk:** expose public bff auth exports with example and docs ([22ed143](https://github.com/bc-solutions-coder/wallow/commit/22ed14346e08192d731f9e498a54beda5fa3ab26))
* **sdk:** expose query layer via ./query subpath ([ae76871](https://github.com/bc-solutions-coder/wallow/commit/ae7687136f4d332b13231277af4177170d7b1cc8))
* **sdk:** harden session cookie with max-age and secure flag ([924246d](https://github.com/bc-solutions-coder/wallow/commit/924246d08ecfa4568d9284cc60106040ac8f916e))
* **sdk:** inject SessionStore into createBffHandlers and destroy session on logout ([724dfc5](https://github.com/bc-solutions-coder/wallow/commit/724dfc5c515a998cb4b4bf3d16a689d200d4677b))
* **sdk:** map userinfo claims into BffSession.user fields ([852bad0](https://github.com/bc-solutions-coder/wallow/commit/852bad09a7073dcd67ad9b9dc4aae3372dcf6547))
* **sdk:** refresh under store.withRefreshLock to protect rotating tokens ([0b4ea50](https://github.com/bc-solutions-coder/wallow/commit/0b4ea50c444a8ae454d1e336f95e8bf5c3e4f160))
* **sdk:** restructure sdk into pnpm monorepo with redis adapter ([b48cb7c](https://github.com/bc-solutions-coder/wallow/commit/b48cb7c6f5c3e3a746dc7b9f16183e98a76d9b19))
* **sdk:** rotate refresh tokens via openid-client refresh grant ([e759c56](https://github.com/bc-solutions-coder/wallow/commit/e759c56d06c4f236215f4627077ba9f1c053dea7))
* **sdk:** route readSession/writeSession through an injected SessionStore ([72c56ce](https://github.com/bc-solutions-coder/wallow/commit/72c56ce94dc574b8cc274c607f1a4ee7c40d23c5))
* **sdk:** share mfa api-wrapper slice across apps ([5436863](https://github.com/bc-solutions-coder/wallow/commit/5436863a975f6b3c10979774dd0fdd87125772a1))
* **styles:** own tailwind via wallowStyles() vite plugin ([2cf8749](https://github.com/bc-solutions-coder/wallow/commit/2cf87499e7d2245c5f2ce297579397f23445cf71))
* **testing:** extract shared vitest preset into @bc-solutions-coder/testing ([8505a30](https://github.com/bc-solutions-coder/wallow/commit/8505a30b99dc58c6c5832358beb686042071c2de))
* **ui:** add shared component library and move ready-indicator/focus-on-navigate ([36b9136](https://github.com/bc-solutions-coder/wallow/commit/36b913632e1979be5899c53ffdf43473562bdedf))
* **ui:** scaffold @bc-solutions-coder/ui package with source.css wiring ([ff7fbd5](https://github.com/bc-solutions-coder/wallow/commit/ff7fbd5461ea680a6e7182e46e694811d59f97a1))
* **wallow-auth:** add remember-me to the OTP login tab ([d3600ac](https://github.com/bc-solutions-coder/wallow/commit/d3600ac7eb63a24fd4200143e34a207fc37c6b13))
* **wallow-auth:** forward the real client ip through the h3 proxy ([6fb3ce9](https://github.com/bc-solutions-coder/wallow/commit/6fb3ce9eba5a7a70beb637451339645d2636553b))
* **wallow-auth:** port invitation, accept-terms, and login password screens ([cea6c68](https://github.com/bc-solutions-coder/wallow/commit/cea6c68fc20f07470990c422af32d4db15425487))
* **wallow-auth:** port logout, mfa, and register screens ([d39facf](https://github.com/bc-solutions-coder/wallow/commit/d39facf9b463db3765d44e7ec6b46516494b0c4e))
* **wallow-auth:** port magic-link, otp, and external provider login tabs ([9e4b6d2](https://github.com/bc-solutions-coder/wallow/commit/9e4b6d276d0ae11e3968ed6777087e7cbea4d397))
* **wallow-auth:** port password reset, verify-email, and consent screens ([1d943eb](https://github.com/bc-solutions-coder/wallow/commit/1d943eb9bf9a85812a9bd89c06a8a47e2c613418))
* **wallow-auth:** scaffold tanstack start auth app ([ac192a7](https://github.com/bc-solutions-coder/wallow/commit/ac192a7a761a32e6a182e0011546ae327ad1ab3e))
* **wallow-auth:** serve SSR from the standalone host and containerise it ([df2d9b6](https://github.com/bc-solutions-coder/wallow/commit/df2d9b66b0340b6722ba2c4bb9022770bd5445c6))
* **wallow-web:** add apps oauth-client feature ([ffcd30a](https://github.com/bc-solutions-coder/wallow/commit/ffcd30ad536accc002af9d2d896cb705726c6b8a))
* **wallow-web:** add dashboard shell, auth gate, and BFF smoke route ([1479827](https://github.com/bc-solutions-coder/wallow/commit/1479827657b2ed345aa044ccf4fbc302a2e76a1d))
* **wallow-web:** add inquiries feature with detail and comments ([3ff29f9](https://github.com/bc-solutions-coder/wallow/commit/3ff29f9db75b083ce2f814de79838db00809c934))
* **wallow-web:** add organizations feature with crud and members ([3747edc](https://github.com/bc-solutions-coder/wallow/commit/3747edc0b8fd800afa39153809bff296050a20e1))
* **wallow-web:** add query client core and getWallowSdk facade ([5b5a497](https://github.com/bc-solutions-coder/wallow/commit/5b5a49776c2ae14f766697a31dbb27aec6bff647))
* **wallow-web:** add settings profile and mfa features ([af61f68](https://github.com/bc-solutions-coder/wallow/commit/af61f6810f35ce08c4de80b8137d7fe8b1c2fc36))
* **web-shell:** add standalone host runtime and vite preset factories ([39c7c19](https://github.com/bc-solutions-coder/wallow/commit/39c7c19274ff0ba0783e74338c515d004c2a5726))
* **web-shell:** build new workspace packages in dockerfiles and ci ([1faa40e](https://github.com/bc-solutions-coder/wallow/commit/1faa40e67e91d42902c75d8d9f3752dc03f3d858))
* **web-shell:** scaffold @bc-solutions-coder/web-shell package ([b41b44a](https://github.com/bc-solutions-coder/wallow/commit/b41b44aa875a53472cf5e875eb665b39cd49f64a))
* **web:** add zustand ui store for the dashboard nav ([0682d68](https://github.com/bc-solutions-coder/wallow/commit/0682d684d0afd1f6ea2f8d7feead274bd867d957))
* **web:** adopt shared ui design language in dashboard features ([a1b3af2](https://github.com/bc-solutions-coder/wallow/commit/a1b3af298b356e28d0c9d48a877cfdccaf5733b3))
* **web:** restore route-tree codegen, centralize styling, regenerate openapi client ([9ea6928](https://github.com/bc-solutions-coder/wallow/commit/9ea69283a1d4885f7af1484358f82fd798c842c1))
* **web:** retire the Blazor Wallow.Web app for the React port ([01fa5ec](https://github.com/bc-solutions-coder/wallow/commit/01fa5ec07238490776b407bfd3d415d2ce5a5eb0))
* **workspace:** normalize package scripts across root and members ([6b14121](https://github.com/bc-solutions-coder/wallow/commit/6b141218066163d25570fa812def1d1f12985c57))


### Bug Fixes

* **api:** configure parent-based ratio trace sampler ([92f8653](https://github.com/bc-solutions-coder/wallow/commit/92f86536219763ef7ee5dcb96bb381010e1888d7))
* **api:** restore Wolverine 6 codegen and expose OpenAPI pre-setup ([fac6050](https://github.com/bc-solutions-coder/wallow/commit/fac60500feb3fac970c73e710cd1fa8365913ed0))
* **auth:** forward requested scopes from the authorize redirect to consent ([ae11a47](https://github.com/bc-solutions-coder/wallow/commit/ae11a47f1bb12d8a11042c8e53f0cc1a530618db))
* **auth:** relay the external-login client_id through mfa-challenge and accept-terms ([6ed8a53](https://github.com/bc-solutions-coder/wallow/commit/6ed8a533bed0721e107ea95da21f219dfa399ef8))
* **auth:** render a not-found page for unmatched routes ([e8f3698](https://github.com/bc-solutions-coder/wallow/commit/e8f36981b6bf083893675c93be0989f4ba963452))
* **auth:** wire the per-client branding overlay on the login screen ([ac4cbbc](https://github.com/bc-solutions-coder/wallow/commit/ac4cbbcbaf4ce54dafe7d9dd352ea1bb3da3daf6))
* correct minimal-app tsconfig extends path after apps/ relocation ([78a4e61](https://github.com/bc-solutions-coder/wallow/commit/78a4e612b5cc868a70b1f5161df312c367f933ff))
* **deps:** patch vulnerable direct and transitive packages ([f4856bb](https://github.com/bc-solutions-coder/wallow/commit/f4856bb207a7dc0c0196eaa9cc51462032c95336))
* **docker:** build tanstack-min image with pnpm workspace ([442826b](https://github.com/bc-solutions-coder/wallow/commit/442826bb2000655efd5df007cdc583c8d234a550))
* **docker:** refuse garage key reimport on placeholder key id ([c3410b2](https://github.com/bc-solutions-coder/wallow/commit/c3410b20648eb293d944dc18a1a2730d795c1ab5))
* **docker:** require BFF_COOKIE_PASSWORD and document it in env example ([1d4d532](https://github.com/bc-solutions-coder/wallow/commit/1d4d53231702d1d7004cdd86d83531f66ca21937))
* **hooks:** point pre-commit format check at api/Wallow.slnx ([9278cb2](https://github.com/bc-solutions-coder/wallow/commit/9278cb20b104c89d9bf9b73b54cb19fb3f94ebce))
* **identity:** align first-party client ids with seeded clients ([f275c57](https://github.com/bc-solutions-coder/wallow/commit/f275c578956eccee4fa226f884dbf1422a21af99))
* **identity:** carry requested scopes to the consent redirect ([a7ad1a3](https://github.com/bc-solutions-coder/wallow/commit/a7ad1a3888214d53c36babc16ca6522e34e92efb))
* **identity:** derive MFA partial-auth cookie Secure flag from the request ([6f8d175](https://github.com/bc-solutions-coder/wallow/commit/6f8d175d3506f33be0eef676075c4aa85af41ad1))
* **identity:** persist PostLogoutRedirectUris when provisioning apps ([8bbc2c9](https://github.com/bc-solutions-coder/wallow/commit/8bbc2c95933199c567a5988b7bf57ca2f42e9e61))
* **identity:** preserve the allow-listed absolute returnUrl on exchange-ticket ([4076c6f](https://github.com/bc-solutions-coder/wallow/commit/4076c6f8a7ee5d1a73deb60fa1cd4e9fa018e78d))
* **identity:** register keyed identity settings service ([8f73c2c](https://github.com/bc-solutions-coder/wallow/commit/8f73c2cd1d6104fd839b63584ed970d0d49cf60d))
* **identity:** return the scheme name from the external provider list ([133d6f2](https://github.com/bc-solutions-coder/wallow/commit/133d6f228032520be0b44e034150a1fdbe8b9d8a))
* **identity:** scope redirect URI validation cache per client ([63212ee](https://github.com/bc-solutions-coder/wallow/commit/63212ee42ffda104c32a7f40b8b2dc0cd31b3fb9))
* **identity:** sign magic-link tokens through the data protection key ring ([32fc727](https://github.com/bc-solutions-coder/wallow/commit/32fc72718755e73260a63a515a0a1c4d5f082449))
* **identity:** thread client_id through the external-login flow ([8089a7b](https://github.com/bc-solutions-coder/wallow/commit/8089a7b7c9f93aafcbb138bc46cbeefc29e54fb3))
* **identity:** wire the React apps' env through the Aspire AppHost ([58fc29d](https://github.com/bc-solutions-coder/wallow/commit/58fc29df4a140ee3ece0966826ed275ca8cdcc39))
* **lint-staged:** match protected sdk paths against absolute paths ([4ce6671](https://github.com/bc-solutions-coder/wallow/commit/4ce667153130eee66ca15d8d141ab1679c042baa))
* **lint-staged:** stop passing ignored routeTree.gen.ts files to oxfmt ([7232af0](https://github.com/bc-solutions-coder/wallow/commit/7232af0787136ebc3a8076d988dad6baadeef0e4))
* **observability:** register custom otel meters and sources ([70e94b8](https://github.com/bc-solutions-coder/wallow/commit/70e94b85b15d9437edd5faf0007720eec396b224))
* restore example app to pnpm workspace after apps/ relocation ([33d2cdf](https://github.com/bc-solutions-coder/wallow/commit/33d2cdf1f12ea4c920deefb055058b285fd49307))
* **scripts:** treat build failures as failures in run-tests.sh ([5341025](https://github.com/bc-solutions-coder/wallow/commit/53410252bf880af64910a5fe0e983a065471ed0e))
* **sdk:** add SSR-safe baseUrl and headers options to getUser ([6b6dd54](https://github.com/bc-solutions-coder/wallow/commit/6b6dd54472e4621ae53476023d438ad03c2d9543))
* **sdk:** bump @hey-api/openapi-ts to 0.99 to clear npm security alerts ([1b1ee70](https://github.com/bc-solutions-coder/wallow/commit/1b1ee7051ed65266dd1230ef04e012927abfc323))
* **sdk:** preserve auth error code through error mapping ([d4e27e0](https://github.com/bc-solutions-coder/wallow/commit/d4e27e0921a8eaf055990e3b80482752d061c882))
* **sdk:** scope client-facing auth queries by client_id ([ce5350d](https://github.com/bc-solutions-coder/wallow/commit/ce5350d59796d0a648013dccd90e9a5c3c2d14f0))
* **seeder:** make admin bootstrap idempotent ([4b8165f](https://github.com/bc-solutions-coder/wallow/commit/4b8165f589d6e16f5f57d2f4092da988ce57b838))
* **wallow-auth:** accept server-vouched absolute return url on mfa challenge ([9d5495f](https://github.com/bc-solutions-coder/wallow/commit/9d5495f42bfb846df0966bd13d2732128fb5f5f2))
* **wallow-auth:** acknowledge password_reset on the login screen ([7f73aa3](https://github.com/bc-solutions-coder/wallow/commit/7f73aa310e82ed1c53c73e8646bdebc8b077f82c))
* **wallow-auth:** default the h3 proxy to the local API ([a6e0467](https://github.com/bc-solutions-coder/wallow/commit/a6e0467948655d62be8bad18516ac3fd0c2edf20))
* **wallow-auth:** link the compiled stylesheet from the production document ([4c67b24](https://github.com/bc-solutions-coder/wallow/commit/4c67b2438458181fdfebc848a279901ce16156bf))
* **wallow-auth:** resolve the org-domain interstitial before submitting register ([0320b4d](https://github.com/bc-solutions-coder/wallow/commit/0320b4da0b9e3faa004f234f6d2c38e4c23e2a45))
* **wallow-web:** serve the client bundle and stabilize hydration in dev-server ([0c11e43](https://github.com/bc-solutions-coder/wallow/commit/0c11e436d00f2315a3ba357798eb7a1d96959096))
* **web-shell:** silence route-file warnings and HMR port collisions ([be03bd5](https://github.com/bc-solutions-coder/wallow/commit/be03bd561f151311c3d4c99f24dd4f0d40d46888))
* **web:** forward SSR request origin and session cookie to BFF fetches ([423e83d](https://github.com/bc-solutions-coder/wallow/commit/423e83de947ba70111ea80bbf726553f6842e160))
* **web:** route SSR self-fetch through an internal origin override ([43285af](https://github.com/bc-solutions-coder/wallow/commit/43285aff3b8b9dcea5639ec7d1b330c0654dd2a0))
* **web:** stop the forced-login branch of / SSR from 500ing ([0774edf](https://github.com/bc-solutions-coder/wallow/commit/0774edf25094c5d77d41edd9812a0779b3594524))
* **web:** stop unauthenticated /dashboard SSR from 500ing ([3340cc2](https://github.com/bc-solutions-coder/wallow/commit/3340cc204fbf9826c03ec7203fe31854c743bff9))


### Miscellaneous Chores

* **deps:** upgrade Asp.Versioning to 10.0.0 ([6dc0674](https://github.com/bc-solutions-coder/wallow/commit/6dc067450e5e816c308e4db6c2c798a3ff952a57))
* **deps:** upgrade StackExchange.Redis to 3.0.17 ([55a7948](https://github.com/bc-solutions-coder/wallow/commit/55a7948886d9b3918b4c8b5a4c003506d02f22d5))
* **deps:** upgrade WolverineFx to 6.19.0 ([1ed121b](https://github.com/bc-solutions-coder/wallow/commit/1ed121b9a9760d19a5ac69ed03c5ec7ec1202fe4))


### Code Refactoring

* **identity:** collapse client provisioning and simplify tenant model ([eb653d5](https://github.com/bc-solutions-coder/wallow/commit/eb653d5c5285107f3d3b8f483e6ec9f86306081e))
* **identity:** delete Blazor Wallow.Auth and the .NET E2E suite ([9c4c940](https://github.com/bc-solutions-coder/wallow/commit/9c4c940c9423f379e1266f29994517b1659ed971))

## [3.2.1](https://github.com/bc-solutions-coder/wallow/compare/v3.2.0...v3.2.1) (2026-07-05)


### Bug Fixes

* **ci:** wait for :sha images before promoting on release ([e694fb3](https://github.com/bc-solutions-coder/wallow/commit/e694fb3ca48852b10db42d8bf87c9f4dab7478c4))
* **docker:** make production seed file host path configurable ([8c26fad](https://github.com/bc-solutions-coder/wallow/commit/8c26fade5de41e9c22077215c9c4eb065f10ccff))
* **docker:** pin PGDATA to legacy path for postgres 18 ([3d01788](https://github.com/bc-solutions-coder/wallow/commit/3d0178821d2195637d5772dfd5541767a7c213f1))
* web styling, deploy CSS regeneration, and register nav performance ([#62](https://github.com/bc-solutions-coder/wallow/issues/62)) ([59d9d38](https://github.com/bc-solutions-coder/wallow/commit/59d9d384d6356dfcc2263fe38b0acddfd0a63f31))

## [3.2.0](https://github.com/bc-solutions-coder/wallow/compare/v3.1.0...v3.2.0) (2026-07-04)


### Features

* **docker:** seed bcordes.dev OIDC and BFF clients in production ([a6492b8](https://github.com/bc-solutions-coder/wallow/commit/a6492b8564f5aadb70d828a03b0326c510a69565))
* extract seeding into dedicated SeederService container ([8a46601](https://github.com/bc-solutions-coder/wallow/commit/8a46601e8b28c1896e84ec49f699248066d7ca0f))
* **seeder:** add SeederService for automated tenant and user provisioning ([36a74b4](https://github.com/bc-solutions-coder/wallow/commit/36a74b44d0bf75e08bc18b1387775c7fab3b4bd8))


### Bug Fixes

* **build:** disable static web asset compression on container publish ([1aa1893](https://github.com/bc-solutions-coder/wallow/commit/1aa1893315dbe00bf93b678474070807d0de07ce))
* **build:** keep NuGet audit CVEs as warnings, not build errors ([d1dc0d9](https://github.com/bc-solutions-coder/wallow/commit/d1dc0d9bf16dac8ecd0287cb560732d681add8d2))
* **ci:** add wallow-seeder image to E2E pipeline ([4b79245](https://github.com/bc-solutions-coder/wallow/commit/4b792459768a05952cb5b3545047e8478027d3a2))
* **ci:** build and publish wallow-seeder container image ([5a10e57](https://github.com/bc-solutions-coder/wallow/commit/5a10e578deacee657127c5362c1418828cfc2048))
* **e2e:** use NetworkIdle wait for login page in logout test ([5470b60](https://github.com/bc-solutions-coder/wallow/commit/5470b60391986a439db6fbfc4eec799f8f1284b4))
* **seeder,e2e:** env var precedence and test reliability in containers ([4b19c59](https://github.com/bc-solutions-coder/wallow/commit/4b19c5909bf721eca062b64a7a2b3cc82ca3c531))

## [3.1.0](https://github.com/bc-solutions-coder/wallow/compare/v3.0.2...v3.1.0) (2026-04-02)


### Features

* **identity:** add OIDC diagnostic logging across entire auth flow ([540522c](https://github.com/bc-solutions-coder/wallow/commit/540522c24ae9af554e5abca160950afad148fee7))
* **identity:** add OIDC diagnostic logging across entire auth flow ([45746e2](https://github.com/bc-solutions-coder/wallow/commit/45746e262428f8e4c6f9ca60a4c790fcf0ceef88))


### Bug Fixes

* **auth:** use BbButton component for consent approve/deny buttons ([b802f8e](https://github.com/bc-solutions-coder/wallow/commit/b802f8e34d33d08fb7d6ec526e40c0403b82d6d9))
* **docker:** add cert volume init for non-root API container ([77039ed](https://github.com/bc-solutions-coder/wallow/commit/77039ed1349064c9003f2730a13b0d1c2feff376))
* **identity:** resolve login loop from cookie path and OIDC redirect mismatch ([e7af177](https://github.com/bc-solutions-coder/wallow/commit/e7af177bed8129255910c933d41486a5f7a41041))
* **identity:** resolve production login loop from ephemeral certs and DP keys ([9fcb26c](https://github.com/bc-solutions-coder/wallow/commit/9fcb26c65dd66071d2b6050aed0b9bce98f222d0))

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
