**status: completed**

# Back-channel logout: OP side (#146)

Wallow's end-session pipeline performs OpenID Connect back-channel logout as an extension
OpenIddict 8 can delete. Parent spec: #131 § Back-channel logout. RP/SDK side is #151, not here.

## Already in place (from #135 and earlier)

- `backchannel_logout_uri` property key, descriptor accessors (Api + Infrastructure twins),
  org-scoped DTOs/requests/responses/controller validation, frontend Redirects-step field,
  generated SDK types.
- Session-participation registry `sso_session_clients` (`SsoSessionClient`,
  `ISsoClientSessionService`) written per login, read by front-channel logout.
- WireMock.Net in the Identity integration test project (Resilience folder prior art).

## Work

### 1. `backchannel_logout_session_required` end to end

- `ClientApplicationProperties.BackchannelLogoutSessionRequired = "backchannel_logout_session_required"`.
- Bool accessors in both `OpenIddictApplicationExtensions` twins (absent → false).
- Org surface: register/update requests, `ClientConfigurationInput`, `OrganizationClientDto`,
  `OrganizationClientResponse`, `OrganizationClientService.ApplyConfiguration`/`ToDto`.
- Admin surface (`ClientsController`): `CreateClientRequest`/`UpdateClientRequest`/`ClientResponse`
  gain **both** back-channel fields (currently front-channel only), with validation.
- Seed: `PreRegisteredClientDefinition.BackchannelLogoutUri` + `BackchannelLogoutSessionRequired`,
  applied in `PreRegisteredClientSyncService` (create + sync), validated in `Validate()`.
- Frontend: Redirects step + `ClientSettingsEditor` gain a "requires session id" switch.

### 2. Back-channel URI validation rule

Spec: absolute, no fragment, `http://` only for confidential clients (public clients need
https). New `ClientUriRules.TryParseBackchannelLogoutUri(string, bool isConfidential, out Uri?)`
+ error constant; wired on org surface, admin surface, and seed validation. Org-registered
applications and admin-created clients are confidential (they hold secrets), so http passes
there; the rule still guards the day a public client type appears.

### 3. Discovery flags

`backchannel_logout_supported` + `backchannel_logout_session_supported` = true, added to the
existing `HandleConfigurationRequestContext` inline handler beside the front-channel twins.

### 4. Notifier (`IBackchannelLogoutNotifier` / `BackchannelLogoutNotifier`)

- Application interface: `Task NotifyAsync(string sid, Guid userId, string issuer, CancellationToken ct)`
  — sid-keyed so admin session revocation / password change can call it later.
- Registry walk mirrors `SsoClientSessionService.BuildLogoutNotificationUrisAsync`: distinct
  client ids for the sid → application descriptor → `GetBackchannelLogoutUri()`.
- Logout token per participating client, minted directly with the server's signing credentials
  (`IOptionsMonitor<OpenIddictServerOptions>.CurrentValue.SigningCredentials`) via
  `JsonWebTokenHandler` — deliberately not the id-token pipeline:
  `iss, sub, aud = client_id, iat, exp = iat + 2 min, jti, sid`,
  `events: { "http://schemas.openid.net/event/backchannel-logout": {} }`, header
  `typ: logout+jwt`, no `nonce`.
- Delivery: POST `logout_token=<jwt>` form-encoded, all RPs in parallel; per-RP timeout
  (default 3 s) via linked CTS; one delayed retry (default 1 s) on timeout/5xx/network error;
  overall bound (default 10 s) so a slow RP never delays logout past it. Failures are logged,
  never surfaced — back-channel is best-effort like front-channel.
- Typed `HttpClient` registered as `AddHttpClient<BackchannelLogoutNotifier>` with plain
  timeout (no resilience profile — the notifier owns its single-retry policy).
- **SSRF policy knob**: `BackchannelLogoutOptions` (`Identity:BackchannelLogout`) with
  `AllowPrivateNetworkHosts` (default **false**), plus the timeout/retry knobs above. Before
  POSTing, the target host is resolved and refused when it lands on loopback, link-local,
  RFC1918/ULA space unless the knob allows it (dev/e2e compose networks set it true). The
  resolution check is best-effort defense in depth, not a rebinding-proof gate; OpenIddict 8's
  own back-channel support replaces this whole class.

### 5. Logout hook

`LogoutController`: on the first (notification) phase of GET `Logout` and on `LogoutPost`,
after token revocation and **before** local sign-out and `ForgetAsync`, await
`NotifyAsync(sid, userId, issuer)`. Front-channel iframe rendering unchanged.

### 6. Tests

- Unit: back-channel URI rule matrix; SSRF target policy; notifier claim set + retry/timeout
  behavior against a scripted `HttpMessageHandler` where cheap.
- Integration (WireMock RP, prior art `Resilience/`): logout delivers a `logout_token` that
  validates against the host's JWKS with all required claims and `typ: logout+jwt`; a slow RP
  (WireMock delay > bound) does not delay the logout response beyond the bound; exactly one
  retry on a failing RP; a private-network host is refused when `AllowPrivateNetworkHosts`
  is false; the front-channel iframe page still renders.
- Integration config: test factory sets `AllowPrivateNetworkHosts=true` (WireMock on
  loopback) except in the SSRF-refusal spec.

### 7. Docs + generated surface

- Seed field documented (`docs/getting-started/configuration.md` seed table).
- OpenAPI snapshot + SDK client regenerated (`packages/sdk/openapi/v1.json`, `src/generated`).

## Order

1. URI rule + `session_required` plumbing (org/admin/seed) — TDD at validator + controller seams.
2. Discovery flags.
3. Notifier unit-first (claims, SSRF policy), then WireMock integration specs.
4. Logout hook + front-channel-still-runs spec.
5. Frontend field, docs, OpenAPI/SDK regen, gates, review.
