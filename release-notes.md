:robot: I have created a release *beep* *boop*
---


<details><summary>5.0.0</summary>

## [5.0.0](https://github.com/bc-solutions-coder/wallow/compare/v4.0.0...v5.0.0) (2026-09-06)


###   BREAKING CHANGES

* **api:** give missing tenant scope a dedicated code
* **forms:** retire legacy failure helpers
* **wallow-auth:** the auth and MFA endpoints answer RFC 7807 problems with Auth.*/Mfa.* codes instead of `{ error }` bodies; PasswordlessResult is gone.
* **wallow-web:** wallow-web's read and mutation sites no longer accept per-site error fallback strings; `errorText` is no longer used by the app.
* **forms:** resolve submit failures through api-errors and the registry
* **ui,query:** failure surfaces  toast, banner, provider, client callback
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
* **styles:** add the warning token pair ([e11dd02](https://github.com/bc-solutions-coder/wallow/commit/e11dd02da044022f6f67252372479991b2e73fa6))
* **styles:** resolve the fork's outbound links from the environment ([8f3036d](https://github.com/bc-solutions-coder/wallow/commit/8f3036d975e2e9ee7f8a6b1742762a5433f2595f))
* **testing:** add console guard with consume-based error assertions ([b419be5](https://github.com/bc-solutions-coder/wallow/commit/b419be5040fecc1dc0e064d7038fc90df1232844))
* **testing:** add consume-based hand-off assertions and wire wallow-auth navigation guard ([26be562](https://github.com/bc-solutions-coder/wallow/commit/26be562fe21e9d34527e02f5db944deeb2bf7e08))
* **testing:** add network-escape guard blocking unharnessed fetch ([39b03f8](https://github.com/bc-solutions-coder/wallow/commit/39b03f880d8256eee8ad410e16494dfb41d44c2c))
* **testing:** root route options on renderWithWallow ([f6cde6d](https://github.com/bc-solutions-coder/wallow/commit/f6cde6de05d2b3f4177ba227cc3f543a9c651501)), closes [#142](https://github.com/bc-solutions-coder/wallow/issues/142)
* **ui,query:** failure surfaces  toast, banner, provider, client callback ([3d3533a](https://github.com/bc-solutions-coder/wallow/commit/3d3533a0d1714f9b16fba9a23c5e0206af567419)), closes [#182](https://github.com/bc-solutions-coder/wallow/issues/182)
* **ui:** add CardHeader ([639d1ab](https://github.com/bc-solutions-coder/wallow/commit/639d1aba71f16a66d73e88cd4d1ba5fb257efffe))
* **ui:** add NoticeBanner for success and warning notices ([44b98d7](https://github.com/bc-solutions-coder/wallow/commit/44b98d76ac3b11e10c4a5ce18eb71dbbcda2c69e))
* **ui:** add QuietLink for muted footer and back links ([488a8ff](https://github.com/bc-solutions-coder/wallow/commit/488a8ffedcefd95abcc2586baff7e1f949715372))
* **ui:** adopt 20px (text-xl) as the catalog-wide card-heading standard ([dd49f14](https://github.com/bc-solutions-coder/wallow/commit/dd49f14aab112254cedae56819fd764835f5b158))
* **ui:** offer sign-in from authentication failure toasts ([10c860a](https://github.com/bc-solutions-coder/wallow/commit/10c860a953467de7986771c32e4ff447010bf6d4)), closes [#200](https://github.com/bc-solutions-coder/wallow/issues/200)
* **ui:** shared branded header with organization attribution ([2e5888b](https://github.com/bc-solutions-coder/wallow/commit/2e5888bd38b3a0eeae4a85067b5ed157b2c49553)), closes [#142](https://github.com/bc-solutions-coder/wallow/issues/142)
* **utils:** add @bc-solutions-coder/utils and rewire the apps onto it ([bc7c5c7](https://github.com/bc-solutions-coder/wallow/commit/bc7c5c734302c7888def234445c5cbce846345a7))
* **wallow-auth:** add the AuthScreen shell ([df8fbea](https://github.com/bc-solutions-coder/wallow/commit/df8fbea18b8cc626af20724816711a1e192574af))
* **wallow-auth:** add the first-run /setup page ([2c3a60a](https://github.com/bc-solutions-coder/wallow/commit/2c3a60aa756b5b240e055494fa87b05e105d3934)), closes [#109](https://github.com/bc-solutions-coder/wallow/issues/109)
* **wallow-auth:** add the shared useReturnUrlGuard hook ([c09097a](https://github.com/bc-solutions-coder/wallow/commit/c09097a282205f974c027c6acd4ac3a3d895be07))
* **wallow-auth:** auth/mfa endpoints answer problems, one registry ([9479110](https://github.com/bc-solutions-coder/wallow/commit/9479110342e9cc94e19df25ac7ca8a8b9181c360))
* **wallow-auth:** give a pending join request its own screen ([c18c21c](https://github.com/bc-solutions-coder/wallow/commit/c18c21ca9acd10f110d593a5bae4f8a9a97b1bb4))
* **wallow-auth:** send a signed-in user to the web app ([f65d173](https://github.com/bc-solutions-coder/wallow/commit/f65d173cfe8743fd4ec2c06159b661f3cc107d0c))
* **wallow-web:** add member role management screen with own-org gate ([c719272](https://github.com/bc-solutions-coder/wallow/commit/c71927287ac081a579c3d26e7cdc64c6d1dcd798))
* **wallow-web:** add member-facing leave-organization screen ([1175333](https://github.com/bc-solutions-coder/wallow/commit/1175333ff043a05a651e691364a94debb69c027a))
* **wallow-web:** add pending-request review and invitation screens ([d2ebf1e](https://github.com/bc-solutions-coder/wallow/commit/d2ebf1edc85201ffdde4ff33e3998e0dc050f624))
* **wallow-web:** migrate org-detail route to $orgId directory form ([b1479af](https://github.com/bc-solutions-coder/wallow/commit/b1479afe2664f8935ec9f2371c81313a0d34e9df))
* **wallow-web:** migrate read and mutation sites onto the failure surfaces ([08e0abc](https://github.com/bc-solutions-coder/wallow/commit/08e0abc2a9095f1f5e97a56c58f63c5fe71139c0))
* **web:** connected applications settings card with withdraw ([73ab846](https://github.com/bc-solutions-coder/wallow/commit/73ab8465301eb0f42c1c5a257415d63ae527241e)), closes [#143](https://github.com/bc-solutions-coder/wallow/issues/143)
* **web:** pin heading variants with text-heading-variant ([a480973](https://github.com/bc-solutions-coder/wallow/commit/a4809732b029c1448e3185d9fd4e03b2350fb971))
* **web:** resolve org members by email in a searchable picker ([e4d8659](https://github.com/bc-solutions-coder/wallow/commit/e4d86591044e758d6eb884c94990c3ddfc89dffa))
* **web:** wire membership screens into nav and org detail ([09eb4d7](https://github.com/bc-solutions-coder/wallow/commit/09eb4d73639b12d1df23541e9f034f02e2c5f32b))


### Bug Fixes

* **announcements:** remove plan targeting and the claim nothing issues ([dbf2d2b](https://github.com/bc-solutions-coder/wallow/commit/dbf2d2baa639c232e1e855b53d7008a82afdd2fe))
* **api:** answer browser error navigations with problem+json, not downloads ([7f4f16e](https://github.com/bc-solutions-coder/wallow/commit/7f4f16ef65eaf6e44384259a14d9be0b0a81e7a4)), closes [#109](https://github.com/bc-solutions-coder/wallow/issues/109)
* **api:** collapse each module's schema name to one constant ([0f3e3b0](https://github.com/bc-solutions-coder/wallow/commit/0f3e3b00a1e2136de2a87065789623be9eb19921))
* **api:** commit handler writes and cascaded messages together ([a2eec93](https://github.com/bc-solutions-coder/wallow/commit/a2eec93ca37b4962cfae81eb5abdfa980e3924c9))
* **api:** give each handler its own chain and retry loop ([4800cd6](https://github.com/bc-solutions-coder/wallow/commit/4800cd6ff8b1b6267770579f4e2bef06bfc3ebd3))
* **api:** give missing tenant scope a dedicated code ([cc50b90](https://github.com/bc-solutions-coder/wallow/commit/cc50b90644009c294d4dc9d47e46e969586cdc38)), closes [#188](https://github.com/bc-solutions-coder/wallow/issues/188)
* **api:** pin the Wolverine application assembly explicitly ([3c7d161](https://github.com/bc-solutions-coder/wallow/commit/3c7d161b3f3cf099c9c527ffbf063ebeb21305c7))
* **api:** stop routing a disabled module's controllers ([4f9b0f1](https://github.com/bc-solutions-coder/wallow/commit/4f9b0f1051a4af3974f8267ebffe01a5fb0d8e8e))
* **apps:** name server-only modules *.server.* so import protection fires ([3d5d687](https://github.com/bc-solutions-coder/wallow/commit/3d5d687008c81ef14254ec386ed660f636093318))
* **auth:** standardize wallow-auth card headings to one 16px scale ([23408a9](https://github.com/bc-solutions-coder/wallow/commit/23408a9cc434fb4db11e9685b7e5b4d22a4d3f48))
* **branding:** bypass the tenant filter in the client-branding lookup ([a284b02](https://github.com/bc-solutions-coder/wallow/commit/a284b021adc0dec1a647a85dffe627c36f23227e))
* **branding:** make the display-name sync convergent and the publish atomic with the save ([5d5bab2](https://github.com/bc-solutions-coder/wallow/commit/5d5bab2b056a05c249c371a3112e801ba43420bd)), closes [#154](https://github.com/bc-solutions-coder/wallow/issues/154)
* **branding:** retry the upsert as an update when it loses the registration race ([8204246](https://github.com/bc-solutions-coder/wallow/commit/8204246e0d2350d681f18fa4b5a36c29e79c4fcd)), closes [#153](https://github.com/bc-solutions-coder/wallow/issues/153)
* **build:** run build before typecheck and test in the check gate ([de08d2a](https://github.com/bc-solutions-coder/wallow/commit/de08d2a68ada87c03edcd4dbc1494e4da38ed8fd))
* **ci:** build React images natively and cap docker-images-app at 25 min ([ff7ca9d](https://github.com/bc-solutions-coder/wallow/commit/ff7ca9d07e55d899794236aaebb596dbe5eed63a))
* **ci:** pack the workspace deps the fork smoke tarballs depend on ([5ee823b](https://github.com/bc-solutions-coder/wallow/commit/5ee823bb6d0bda88f1cc6506c7f65d243d81231d))
* **config:** vendor an ESM with-selector so SSR stops loading a second React ([769c1ce](https://github.com/bc-solutions-coder/wallow/commit/769c1cec870ad0f66f7cc6a6b7695f5634faeab5))
* **deps:** bump five transitive packages past their Dependabot advisories ([ea9fe78](https://github.com/bc-solutions-coder/wallow/commit/ea9fe789cf178bb20006ba82b1cd4176e629134d))
* **docker:** copy api-errors into the app image builds ([4ea25f2](https://github.com/bc-solutions-coder/wallow/commit/4ea25f29253f1995ad874480b72c5be4a45c7755))
* **docker:** make the COOKIE_PASSWORDS rotation example non-destructive ([84eb7df](https://github.com/bc-solutions-coder/wallow/commit/84eb7df7407481c4fa46cdef97179d1fb31eb426))
* **docker:** route the pangolin profile by subdomain, not path ([a6196ad](https://github.com/bc-solutions-coder/wallow/commit/a6196ad6e8ea42f692a34919ea351b0724311767))
* **docs:** correct theme picker and dropdown contrast ([c8eeddd](https://github.com/bc-solutions-coder/wallow/commit/c8eeddd6a152df8447308be20f626af00a423513))
* **docs:** restore DocFX theme contrast ([cb927eb](https://github.com/bc-solutions-coder/wallow/commit/cb927ebc536c23199b94baca98742005e1135028))
* **docs:** restore the frontend state boundary the CLAUDE.md split dropped ([99d090b](https://github.com/bc-solutions-coder/wallow/commit/99d090b419d1fc10a1e55b76e60a64c9f4653e74))
* **e2e:** pass PORT through wallow-auth's playwright webserver ([8354467](https://github.com/bc-solutions-coder/wallow/commit/83544670a164c3b88d0b8f525865992bc3ecf31d))
* **e2e:** rebuild app images so a run tests the current tree ([14467ee](https://github.com/bc-solutions-coder/wallow/commit/14467eeffe27f5d5e910853d135b9fe35ff3d0fb))
* **e2e:** wait for the post-logout landing before asserting the 401 ([4908212](https://github.com/bc-solutions-coder/wallow/commit/4908212a6b7a5a9e101b2da2abffd639eb8b14d4))
* **env:** gate x-forwarded-proto on the trusted-proxy set (Wallow-cybh) ([f69ecd4](https://github.com/bc-solutions-coder/wallow/commit/f69ecd4f64f244601a59e0e60f3fad64db776cb6))
* **forms:** stable reset memo and honest deprecation pointers ([7aefb10](https://github.com/bc-solutions-coder/wallow/commit/7aefb107ded1eec937216036190fc2b01c70dfd1)), closes [#183](https://github.com/bc-solutions-coder/wallow/issues/183)
* **forms:** surface unmatched validation messages in the banner ([ded89cb](https://github.com/bc-solutions-coder/wallow/commit/ded89cb38ca33c049d332305e448e416ffb541be)), closes [#201](https://github.com/bc-solutions-coder/wallow/issues/201)
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
* **navigation:** restore sidebar sizing and use theme icons ([dc20fb7](https://github.com/bc-solutions-coder/wallow/commit/dc20fb7d0ff8be54d056a8e94271cd7f8aa0d0f1))
* **notifications:** make the preference checker public so sends generate ([f62c221](https://github.com/bc-solutions-coder/wallow/commit/f62c2214c922c3c2ad11ffa0884d78117d5e9a56))
* **observability:** point the Node logger at Alloy's HTTP port ([e4f3616](https://github.com/bc-solutions-coder/wallow/commit/e4f361642f7c6cabed68f5137d4bcca5bf96e781))
* **packages:** declare files[] so dist actually packs ([184c86a](https://github.com/bc-solutions-coder/wallow/commit/184c86a3fbdaafe9ee4d67057d9e4db2d3f4277d))
* reject an unknown run-tests.sh target instead of running nothing ([32d6eb1](https://github.com/bc-solutions-coder/wallow/commit/32d6eb1e414db356481c9cfaff84329d9a617f26))
* replace stale localhost:5000 fallbacks with the real service urls ([74be8e8](https://github.com/bc-solutions-coder/wallow/commit/74be8e8d7e93298bbe92962006aa45e295298995))
* resolve the client address through a trusted-proxy check ([781041f](https://github.com/bc-solutions-coder/wallow/commit/781041f8a88e1bd451d8050533e18840aab74201))
* **scripts:** make run-tests.sh reach every integration assembly ([5d03093](https://github.com/bc-solutions-coder/wallow/commit/5d0309315b6b19fcec343daece862638e7c9c2c3))
* **sdk:** read a blank COOKIE_NAME as unset, and surface the BFF's remaining knobs ([3520ff1](https://github.com/bc-solutions-coder/wallow/commit/3520ff155049328e3b4505abfce842759854424e))
* **sdk:** return problems for remaining BFF rejections ([412ee61](https://github.com/bc-solutions-coder/wallow/commit/412ee618fa970152da2647e7861e7aa68865e253)), closes [#199](https://github.com/bc-solutions-coder/wallow/issues/199)
* **sdk:** verify packages from an external consumer ([5b77b2e](https://github.com/bc-solutions-coder/wallow/commit/5b77b2ee3a2ad07082c8e1015ea90d9804d95665))
* **seeder:** exit non-zero when a seed step fails ([62bcb9e](https://github.com/bc-solutions-coder/wallow/commit/62bcb9e262421bdc2c54a1e85ddb5ba860052e02))
* **storage:** commit row removals before deleting stored objects ([12a99a4](https://github.com/bc-solutions-coder/wallow/commit/12a99a4ec4d3c59b82d7b8dc4adc624589f8a83d))
* **storage:** enforce the three storage settings on both upload paths ([75104a8](https://github.com/bc-solutions-coder/wallow/commit/75104a84d929c11ad956ba64f6bfa12f029e01d3))
* **storage:** replace broken async presigned-upload scan with a completion endpoint ([ddca318](https://github.com/bc-solutions-coder/wallow/commit/ddca318d3ba63dc9cd5dd41ad904d4fb7a41f63e))
* **storage:** serve local presigned URLs from real signed endpoints ([57e6eb9](https://github.com/bc-solutions-coder/wallow/commit/57e6eb9bba28603c34d5dcc4735fe7cba4e320bd))
* **styles:** serve the fork theme as a virtual stylesheet for test harnesses ([1eba844](https://github.com/bc-solutions-coder/wallow/commit/1eba8446399f0b808cee0a553789359c50edd5c9))
* **testing:** emit types for the console and network guards ([a8892f8](https://github.com/bc-solutions-coder/wallow/commit/a8892f86d2578ee42ec49e0592ad4f21037fabec))
* **ui:** add a surface axis to fix illegible sidebar-composed controls ([eca77a5](https://github.com/bc-solutions-coder/wallow/commit/eca77a5e5ad7be3f4aa9a09556f873b5e4c3bbaa))
* **ui:** adopt stable navigation keyboard behavior ([#214](https://github.com/bc-solutions-coder/wallow/issues/214)) ([a7da7c0](https://github.com/bc-solutions-coder/wallow/commit/a7da7c00dab61b7550a57663db4cfcd5d7af32fd))
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

* **forms:** retire legacy failure helpers ([4100cd0](https://github.com/bc-solutions-coder/wallow/commit/4100cd071fcebd832c2785b8ab4d12a40fef9b37)), closes [#187](https://github.com/bc-solutions-coder/wallow/issues/187)
* **identity:** drop OrgMemberRole in favour of the shared role catalog ([2dbd4d7](https://github.com/bc-solutions-coder/wallow/commit/2dbd4d7859d14f610085568bb53e539721a56697))
* **identity:** drop the frozen home tenant from WallowUser ([77bf14c](https://github.com/bc-solutions-coder/wallow/commit/77bf14cf2dd93916860763620e2a9dc6e313db18))
* **identity:** read and write organization members through memberships ([d1eaa3d](https://github.com/bc-solutions-coder/wallow/commit/d1eaa3d57a8527808338f7b58655f26d19b3a44e))
* **sdk:** delete imperative login() and getUser() browser helpers ([42035fc](https://github.com/bc-solutions-coder/wallow/commit/42035fc231768c3546fc9811803e0db8a6133fd0))
* **sdk:** delete the browser claim-bag readers; one typed user model ([722a598](https://github.com/bc-solutions-coder/wallow/commit/722a598ce5cf753909a13b63ae8b9de87443651c))
* **sdk:** delete the module-scope CSRF token store; add a csrf opt-out ([6603cf7](https://github.com/bc-solutions-coder/wallow/commit/6603cf7b46fff66b7ea2d636f7dbe57544b6fe74))
* **sdk:** delete the unadopted WallowRouterContext interface ([107aba2](https://github.com/bc-solutions-coder/wallow/commit/107aba2dea9ec3ab33d997a338ce7cdc0524ceaf))


### Build System

* move the workspace to pnpm 11.24.0 ([0f6ac76](https://github.com/bc-solutions-coder/wallow/commit/0f6ac76334752d06815589db5c8760ddd093b8f4))
</details>

<details><summary>api-errors: 1.0.0</summary>

## 1.0.0 (2026-09-06)


###   BREAKING CHANGES

* **api:** give missing tenant scope a dedicated code
* **forms:** retire legacy failure helpers
* **wallow-auth:** the auth and MFA endpoints answer RFC 7807 problems with Auth.*/Mfa.* codes instead of `{ error }` bodies; PasswordlessResult is gone.
* **wallow-web:** wallow-web's read and mutation sites no longer accept per-site error fallback strings; `errorText` is no longer used by the app.
* **ui,query:** failure surfaces  toast, banner, provider, client callback
* **sdk:** the SDK-private code constants CSRF_INVALID_CODE, NETWORK_ERROR_CODE, and NETWORK_TIMEOUT_CODE are deleted (pinned deleted by src/index.test.ts); their wire values are now Bff.CsrfInvalid, Transport.NetworkError, and Transport.Timeout from api-errors, and the proxy's forward timeout answers 504 instead of 503. The bodiless 401s from the proxy and /bff/user now carry a problem body.
* **sdk:** throw ApiFailure from the SDK and retarget consumers

### Features

* **api-errors:** publish the dependency-free failure package ([1781636](https://github.com/bc-solutions-coder/wallow/commit/17816366f1343cb410ee849a34dc6dc2e564ef4f)), closes [#179](https://github.com/bc-solutions-coder/wallow/issues/179)
* **sdk:** originate BFF proxy and passthrough failures as problems ([732b573](https://github.com/bc-solutions-coder/wallow/commit/732b573db2ee829b31c08edb441606a035002a84)), closes [#181](https://github.com/bc-solutions-coder/wallow/issues/181)
* **sdk:** throw ApiFailure from the SDK and retarget consumers ([5b04bc3](https://github.com/bc-solutions-coder/wallow/commit/5b04bc3e63bcddb085ac348fd5e94a364f3db011)), closes [#180](https://github.com/bc-solutions-coder/wallow/issues/180)
* **ui,query:** failure surfaces  toast, banner, provider, client callback ([3d3533a](https://github.com/bc-solutions-coder/wallow/commit/3d3533a0d1714f9b16fba9a23c5e0206af567419)), closes [#182](https://github.com/bc-solutions-coder/wallow/issues/182)
* **wallow-auth:** auth/mfa endpoints answer problems, one registry ([9479110](https://github.com/bc-solutions-coder/wallow/commit/9479110342e9cc94e19df25ac7ca8a8b9181c360))
* **wallow-web:** migrate read and mutation sites onto the failure surfaces ([08e0abc](https://github.com/bc-solutions-coder/wallow/commit/08e0abc2a9095f1f5e97a56c58f63c5fe71139c0))


### Bug Fixes

* **api:** give missing tenant scope a dedicated code ([cc50b90](https://github.com/bc-solutions-coder/wallow/commit/cc50b90644009c294d4dc9d47e46e969586cdc38)), closes [#188](https://github.com/bc-solutions-coder/wallow/issues/188)
* **forms:** surface unmatched validation messages in the banner ([ded89cb](https://github.com/bc-solutions-coder/wallow/commit/ded89cb38ca33c049d332305e448e416ffb541be)), closes [#201](https://github.com/bc-solutions-coder/wallow/issues/201)


### Code Refactoring

* **forms:** retire legacy failure helpers ([4100cd0](https://github.com/bc-solutions-coder/wallow/commit/4100cd071fcebd832c2785b8ab4d12a40fef9b37)), closes [#187](https://github.com/bc-solutions-coder/wallow/issues/187)
</details>

<details><summary>sdk: 1.0.0</summary>

## [1.0.0](https://github.com/bc-solutions-coder/wallow/compare/sdk-v0.2.0...sdk-v1.0.0) (2026-09-06)


###   BREAKING CHANGES

* **api:** give missing tenant scope a dedicated code
* **forms:** retire legacy failure helpers
* **wallow-auth:** the auth and MFA endpoints answer RFC 7807 problems with Auth.*/Mfa.* codes instead of `{ error }` bodies; PasswordlessResult is gone.
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
* **wallow-auth:** auth/mfa endpoints answer problems, one registry ([9479110](https://github.com/bc-solutions-coder/wallow/commit/9479110342e9cc94e19df25ac7ca8a8b9181c360))


### Bug Fixes

* **api:** give missing tenant scope a dedicated code ([cc50b90](https://github.com/bc-solutions-coder/wallow/commit/cc50b90644009c294d4dc9d47e46e969586cdc38)), closes [#188](https://github.com/bc-solutions-coder/wallow/issues/188)
* **docs:** restore the frontend state boundary the CLAUDE.md split dropped ([99d090b](https://github.com/bc-solutions-coder/wallow/commit/99d090b419d1fc10a1e55b76e60a64c9f4653e74))
* **identity:** address back-channel logout review findings ([8fa6023](https://github.com/bc-solutions-coder/wallow/commit/8fa6023a4ef4829a44fd24dd69e8ca1fe4ea1986))
* **identity:** bootstrap admin joins the seeded organization ([c6a0f57](https://github.com/bc-solutions-coder/wallow/commit/c6a0f579d30a498addfe594089060b39dc54f59a))
* **identity:** keep the authorize request when a consent post is anonymous ([a178bb7](https://github.com/bc-solutions-coder/wallow/commit/a178bb78dc4787fe50025d4f5dc87027dcd87c2f)), closes [#132](https://github.com/bc-solutions-coder/wallow/issues/132)
* **identity:** validate enrollment requests on the constructor parameter ([fb0d5a7](https://github.com/bc-solutions-coder/wallow/commit/fb0d5a74a78c5cea5b66ed3b0381dd96df36e083))
* **lint:** register the wallow/* plugin in navigation, ui and forms ([d600e25](https://github.com/bc-solutions-coder/wallow/commit/d600e25f4566047adfe67a944fddfd71c2315a05))
* **sdk:** read a blank COOKIE_NAME as unset, and surface the BFF's remaining knobs ([3520ff1](https://github.com/bc-solutions-coder/wallow/commit/3520ff155049328e3b4505abfce842759854424e))
* **sdk:** return problems for remaining BFF rejections ([412ee61](https://github.com/bc-solutions-coder/wallow/commit/412ee618fa970152da2647e7861e7aa68865e253)), closes [#199](https://github.com/bc-solutions-coder/wallow/issues/199)
* **sdk:** verify packages from an external consumer ([5b77b2e](https://github.com/bc-solutions-coder/wallow/commit/5b77b2ee3a2ad07082c8e1015ea90d9804d95665))
* **storage:** replace broken async presigned-upload scan with a completion endpoint ([ddca318](https://github.com/bc-solutions-coder/wallow/commit/ddca318d3ba63dc9cd5dd41ad904d4fb7a41f63e))


### Code Refactoring

* **forms:** retire legacy failure helpers ([4100cd0](https://github.com/bc-solutions-coder/wallow/commit/4100cd071fcebd832c2785b8ab4d12a40fef9b37)), closes [#187](https://github.com/bc-solutions-coder/wallow/issues/187)
* **identity:** read and write organization members through memberships ([d1eaa3d](https://github.com/bc-solutions-coder/wallow/commit/d1eaa3d57a8527808338f7b58655f26d19b3a44e))
* **sdk:** delete imperative login() and getUser() browser helpers ([42035fc](https://github.com/bc-solutions-coder/wallow/commit/42035fc231768c3546fc9811803e0db8a6133fd0))
* **sdk:** delete the browser claim-bag readers; one typed user model ([722a598](https://github.com/bc-solutions-coder/wallow/commit/722a598ce5cf753909a13b63ae8b9de87443651c))
* **sdk:** delete the module-scope CSRF token store; add a csrf opt-out ([6603cf7](https://github.com/bc-solutions-coder/wallow/commit/6603cf7b46fff66b7ea2d636f7dbe57544b6fe74))
* **sdk:** delete the unadopted WallowRouterContext interface ([107aba2](https://github.com/bc-solutions-coder/wallow/commit/107aba2dea9ec3ab33d997a338ce7cdc0524ceaf))
</details>

---
This PR was generated with [Release Please](https://github.com/googleapis/release-please). See [documentation](https://github.com/googleapis/release-please#release-please).