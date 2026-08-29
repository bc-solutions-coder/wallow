**status: active**

# Research: Wallow as an Identity Provider for external, cross-domain clients

_Research document (not an implementation plan). Investigates how wallow.dev should serve
external clients such as bcordes.dev — the Keycloak / FusionAuth / PocketID model — with every
claim cited to its owning spec, first-party product doc, or repo file._

---

## 0. Headline finding: Wallow already is an OIDC Identity Provider

Before anything else: the premise "Wallow serves only same-origin auth today" undersells the
codebase. The Identity module already embeds **OpenIddict 7** as a standards-based OpenID
Provider on top of ASP.NET Core Identity:

- `AddOpenIddict().AddCore/.AddServer/.AddValidation` with
  `AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()`, refresh tokens, and
  client credentials —
  `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`.
- `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/logout` controllers in
  `api/src/Modules/Identity/Wallow.Identity.Api/Controllers/`, with OpenIddict serving
  `/.well-known/openid-configuration` and `/.well-known/jwks` implicitly.
- A consent screen for non-first-party clients (`apps/wallow-auth/src/app/routes/consent.tsx`;
  first-party bypass via the `wallow-` client-id prefix in `AuthorizationController.cs`).
- Admin client CRUD + secret rotation (`ClientsController.cs`) and a self-service developer
  app-registration surface (`AppsController.cs`).
- **A passing three-origin e2e that already proves an external-origin, cross-domain login**:
  `apps/wallow-web/e2e-cross-app/external-origin-login.spec.ts` drives a `bff-example`
  container on `:3003` through redirect → wallow-auth login → consent → code+PKCE exchange →
  session on the external origin → logout, with no token ever reaching the browser.

So the real question is not "how does Wallow become an IdP" but "**what remains between
today's IdP and productized external-client support**" — registration UX, back-channel logout,
per-client branding coverage, public-client policy, and packaging of the RP-side SDK. The rest
of this document grounds the mental model, cites the governing specs, compares first-party
products, and ends with the ordered gap list (§9).

---

## 1. The mental-model correction: it is a redirect dance, not cookie/token sharing

The confusion to retire: _"bcordes.dev → wallow.dev/auth requires CSRF handling"_ and _"the
BFF sends the user's wallow session token to the platform"_. Both assume that cross-site login
means moving a credential (cookie or token) **across** domains. The standardized answer moves
**the user's browser** across domains instead, and never moves a cookie:

1. **Nothing shares cookies across sites.** Cookies are scoped to the host/domain that set
   them; a `SameSite` cookie is by definition withheld from cross-site requests except the
   narrow `Lax` carve-out for top-level navigations using safe methods
   ([draft-ietf-httpbis-rfc6265bis-22](https://datatracker.ietf.org/doc/draft-ietf-httpbis-rfc6265bis/),
   currently in the RFC Editor queue: Lax "sends same-site cookies along with cross-site
   requests if and only if they are top-level navigations which use a 'safe' … HTTP method").
   The design goal is therefore that **no cookie ever needs to cross an origin**.

2. **The credential that crosses domains is a one-time authorization code in a URL**, handed
   through front-channel redirects, then redeemed server-to-server. This is the OAuth 2.0
   authorization code grant ([RFC 6749 §4.1](https://www.rfc-editor.org/rfc/rfc6749#section-4.1))
   profiled by OpenID Connect
   ([OIDC Core 1.0 §3.1](https://openid.net/specs/openid-connect-core-1_0.html#CodeFlowAuth)).

3. **Two independent first-party sessions exist afterwards.**
   - wallow.dev keeps its own IdP session cookie, set by wallow.dev, sent only to wallow.dev.
     That is what makes SSO work: the second client's authorize redirect finds the user
     already logged in.
   - bcordes.dev keeps its own session cookie, set by bcordes.dev after its backend exchanged
     the code. Per the browser-apps BCP, that cookie is `Secure`, `HttpOnly`, and ideally
     `SameSite=Strict`
     ([RFC 10017 §6.1.3.2](https://www.rfc-editor.org/rfc/rfc10017.html)).

4. **"CSRF on the authorize endpoint" is solved inside the protocol, not with a shared CSRF
   token.** A cross-site GET to `https://wallow.dev/connect/authorize` is not a forgery hazard
   in the classic sense — it performs no state change on behalf of the user; it starts a
   ceremony whose result can only land on a **pre-registered, exact-match `redirect_uri`**
   ([RFC 9700 §2.1](https://www.rfc-editor.org/rfc/rfc9700.html#section-2.1): "authorization
   servers MUST utilize exact string matching except for port numbers in localhost redirection
   URIs"; also
   [§4.1.3](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.1.3)). The CSRF-shaped
   attack that _does_ exist — injecting an attacker's authorization response into the victim's
   RP session ("login CSRF") — is countered by parameters bound to the browser session:
   - `state`: "one-time use CSRF tokens carried in the state parameter that are securely bound
     to the user agent MUST be used" when not relying on PKCE
     ([RFC 9700 §2.1](https://www.rfc-editor.org/rfc/rfc9700.html#section-2.1); background in
     [RFC 6749 §10.12](https://www.rfc-editor.org/rfc/rfc6749#section-10.12)).
   - PKCE ([RFC 7636](https://www.rfc-editor.org/rfc/rfc7636)): "PKCE provides robust
     protection against CSRF attacks even in the presence of an attacker that can read the
     authorization response"
     ([RFC 9700 §4.7.1](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.7.1)).
   - `nonce`: "In OpenID Connect flows, the nonce parameter provides CSRF protection"
     ([RFC 9700 §2.1](https://www.rfc-editor.org/rfc/rfc9700.html#section-2.1); defined in
     [OIDC Core §3.1.2.1](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest),
     notes in [§15.5.2](https://openid.net/specs/openid-connect-core-1_0.html#NonceNotes)).

   Wallow's SDK-level `x-csrf-token` mechanism (`packages/sdk/src/server/csrf.ts`) remains the
   right tool for the RP's **own** state-changing `/bff/**` endpoints — it just plays no role
   in the cross-domain hop.

5. **The only place a token travels to the platform is the back channel.** The RP's server
   (its BFF) POSTs the code + `client_secret` + `code_verifier` to wallow.dev's token endpoint
   over TLS (server-to-server, no browser, no cookies), receives access/ID/refresh tokens, and
   thereafter calls Wallow APIs with `Authorization: Bearer …`
   ([RFC 6749 §4.1.3](https://www.rfc-editor.org/rfc/rfc6749#section-4.1.3);
   [RFC 10017 §6.1.3.1](https://www.rfc-editor.org/rfc/rfc10017.html): "the BFF MUST act as a
   confidential client"). So "the BFF sends the user token to the wallow platform" is true
   only in this precise, safe sense: a Bearer access token issued _by_ Wallow, presented on
   the back channel — never the user's wallow.dev session cookie, and never anything readable
   by browser JavaScript.

6. **No CORS is required anywhere in this design.** The front channel is top-level
   navigations; the back channel is server-to-server. This matches the repo today: there is no
   `AddCors`/`UseCors` anywhere in `api/src`, and the cross-origin e2e passes regardless,
   because the SDK's `/bff/api/**` proxy keeps browser calls same-origin
   (`packages/sdk/src/server/proxy.ts`; pattern per
   [RFC 10017 §6.1](https://www.rfc-editor.org/rfc/rfc10017.html), where the BFF "handles all
   OAuth responsibilities and API interactions"). CORS only enters the picture if a
   third-party **SPA** is ever allowed to call Wallow APIs directly — which §5 recommends
   against.

### 1.1 The flow end-to-end (wallow.dev = OP, bcordes.dev = RP)

| Step | Channel | Cookies involved |
| --- | --- | --- |
| 1. User clicks "Log in" on bcordes.dev → `GET bcordes.dev/bff/login` | first-party | none yet |
| 2. RP BFF stores `state`/`nonce`/`code_verifier` in a transaction cookie **on bcordes.dev**, 302 → `wallow.dev/connect/authorize?client_id=…&redirect_uri=…&code_challenge=…&state=…&nonce=…` | top-level redirect | bcordes.dev txn cookie set |
| 3. Browser lands on wallow.dev; the IdP session cookie (if any) rides along because this is a top-level safe-method navigation and the cookie is `SameSite=Lax` | first-party on wallow.dev | wallow.dev session cookie |
| 4. No session → wallow-auth `/login` (and `/consent` for third-party clients) renders, branded per `client_id`; user authenticates **on wallow.dev only** | first-party | wallow.dev sets/refreshes its session cookie |
| 5. OP 302s to the registered `redirect_uri` with `code` + `state` | top-level redirect | code is in the URL, not a cookie |
| 6. RP BFF validates `state` against its txn cookie, POSTs code + secret + `code_verifier` to `wallow.dev/connect/token`; validates ID token `iss`/`aud`/`nonce` | back channel (TLS) | no cookies |
| 7. RP BFF sets its own session cookie **on bcordes.dev** (`__Host-…`, `HttpOnly`, `Secure`) | first-party | bcordes.dev session cookie |
| 8. bcordes.dev frontend calls its own BFF; BFF proxies to Wallow APIs with the Bearer token | back channel | RP cookie only, same-origin |

Every row is either first-party cookies or a cookieless back channel. Nothing is shared across
domains; that is the entire point of the design.

### 1.2 SameSite on the IdP's own cookies

- The wallow.dev auth session cookie must be **`SameSite=Lax`** (not `Strict`): step 3 above
  is a _cross-site_ top-level GET from bcordes.dev, and only Lax's carve-out lets the session
  cookie accompany it so SSO can skip the login form
  ([rfc6265bis-22 §SameSite](https://datatracker.ietf.org/doc/draft-ietf-httpbis-rfc6265bis/)).
  `Strict` would force a fresh login for every external client. Wallow already does this:
  the Identity application cookie is `SameSite = Lax`, `HttpOnly`, `SecurePolicy = Always`
  (`IdentityInfrastructureExtensions.cs`), and nothing in the repo uses `SameSite=None`.
- The Lax carve-out covers **safe methods only**, so it works because Wallow uses the default
  `response_mode=query` (GET redirects). If `response_mode=form_post` were ever adopted, the
  cross-site POST would strip Lax cookies — Microsoft's ASP.NET Core guidance calls this out:
  "Some forms of authentication like OpenID Connect … default to POST based redirects. The
  POST based redirects trigger the SameSite browser protections, so SameSite is disabled
  \[i.e. `None`\] for these components"
  ([Work with SameSite cookies in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/samesite)).
  `SameSite=None` in turn requires `Secure`
  ([rfc6265bis-22](https://datatracker.ietf.org/doc/draft-ietf-httpbis-rfc6265bis/)). Staying
  on `query` responses keeps Wallow out of that swamp.
- The RP-side BFF cookie, by contrast, _should_ be `SameSite=Strict` where the app tolerates
  it ([RFC 10017 §6.1.3.2](https://www.rfc-editor.org/rfc/rfc10017.html): Secure/HttpOnly
  MUSTs, `SameSite=Strict` SHOULD). The SDK currently defaults to `sameSite: "lax"`
  (`packages/sdk/src/server/handlers.ts`) — Lax is what makes the post-callback redirect
  landing carry the fresh session; a `Strict` upgrade is a possible hardening knob, not a gap.

### 1.3 Governing specs for this architecture

- [RFC 6749](https://www.rfc-editor.org/rfc/rfc6749) — OAuth 2.0 framework (roles: wallow.dev
  = authorization server, bcordes.dev = client; §1.1).
- [OIDC Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) — identity layer:
  ID tokens (§2), code flow (§3.1); wallow.dev = OpenID Provider (OP), bcordes.dev = Relying
  Party (RP).
- [RFC 7636](https://www.rfc-editor.org/rfc/rfc7636) — PKCE.
- [RFC 9700 / BCP 240](https://www.rfc-editor.org/rfc/rfc9700.html) — OAuth 2.0 Security BCP:
  exact-match redirect URIs (§2.1, §4.1.3), no implicit ("Clients SHOULD NOT use the implicit
  grant", §2.1.2), no open redirectors (§2.1), mix-up defenses via `iss` or per-AS redirect
  URIs (§4.4.2).
- [RFC 10017 / BCP 212](https://www.rfc-editor.org/rfc/rfc10017.html) — **OAuth 2.0 for
  Browser-Based Applications, published as an RFC in 2026** (formerly
  draft-ietf-oauth-browser-based-apps). BFF is its most-recommended architecture: "strongly
  recommended for business applications, sensitive applications, and applications that handle
  personal data" (§6.1.4.3).
- [OAuth 2.1](https://datatracker.ietf.org/doc/draft-ietf-oauth-v2-1/) — still a draft
  (draft-ietf-oauth-v2-1-15, 2026-03-02; IESG-submission milestone Dec 2026). It consolidates
  6749 + PKCE + RFC 9700: PKCE mandatory, exact redirect-URI matching, implicit and ROPC
  grants removed. Wallow's `RequireProofKeyForCodeExchange()` + code-only flows are already
  OAuth 2.1-shaped.

---

## 2. Client registration: how bcordes.dev becomes a client

### 2.1 The spec landscape

- **Static / admin-managed registration** is the baseline assumed by RFC 6749 §2. Client
  types: **confidential** (can keep a secret — server-side apps, BFFs) vs **public** (SPAs,
  native apps) ([RFC 6749 §2.1](https://www.rfc-editor.org/rfc/rfc6749#section-2.1)).
- **Dynamic Client Registration**: [RFC 7591](https://www.rfc-editor.org/rfc/rfc7591) defines
  a `POST /register` protocol (client metadata in, `client_id`/`client_secret` +
  `registration_access_token` out); [RFC 7592](https://www.rfc-editor.org/rfc/rfc7592) adds
  the management protocol (read/update/delete a registration). OIDC has its own profile
  ([OIDC Registration 1.0](https://openid.net/specs/openid-connect-registration-1_0.html)).
- **Client authentication methods**: `client_secret_basic` / `client_secret_post`
  ([RFC 6749 §2.3.1](https://www.rfc-editor.org/rfc/rfc6749#section-2.3.1)) and the stronger
  `private_key_jwt` assertion profile ([RFC 7523](https://www.rfc-editor.org/rfc/rfc7523)).
  Secrets suffice for a first external-client release; `private_key_jwt` is the upgrade path
  for high-assurance clients.
- **Redirect URIs**: registered per client and compared with exact string matching
  ([RFC 9700 §2.1/§4.1.3](https://www.rfc-editor.org/rfc/rfc9700.html#section-2.1)); no
  wildcards.

### 2.2 How the reference products model it (first-party docs)

- **Keycloak**: a **realm** "manages a set of users, credentials, roles, and groups"; clients
  are "entities that can request Keycloak to authenticate a user," created per realm in the
  admin console with valid-redirect-URI lists and a confidential/public toggle
  ([Server Administration Guide](https://www.keycloak.org/docs/latest/server_admin/index.html)).
  Its [Client Registration service](https://www.keycloak.org/securing-apps/client-registration)
  "extends OAuth 2.0 Dynamic Client Registration Protocol" (RFC 7591/7592), gated by
  **initial access tokens** ("configurable expiration as well as a configurable limit on how
  many clients can be created") and rotating **registration access tokens**.
- **FusionAuth**: an **Application** "holds configuration for how an application … interacts
  with FusionAuth"; each application _is_ the OAuth client ("both an applicationId and a
  client_id, but both use the same Id value"), belongs to a **Tenant**, and carries authorized
  redirect URLs ("URLs that are not authorized may not be utilized in the redirect_uri
  parameter"), client secret requirements, and optional/required PKCE
  ([Applications — core concepts](https://fusionauth.io/docs/get-started/core-concepts/applications)).
- **PocketID**: pure admin-UI registration — "OIDC Clients → Create client", set a callback
  URL, optionally upload a per-client logo, and toggle "Public Client" and "PKCE" per client
  ([client examples, e.g. WG-Access-Server](https://pocket-id.org/docs/client-examples/wg-access-server),
  [OpenCloud](https://pocket-id.org/docs/client-examples/opencloud)). No dynamic registration;
  deliberately small.

### 2.3 What Wallow has, and the recommendation

Wallow already has **three** registration surfaces:

1. **Seeding**: `api/seed.json` / `docker/seed.production.json` → `PreRegisteredClientOptions`
   (`clientId`, `displayName`, `secret`, `redirectUris[]`, `postLogoutRedirectUris[]`,
   `frontchannelLogoutUri`, `scopes[]`, `tenantId`, `public`, seed members) →
   `PreRegisteredClientSyncService` builds an `OpenIddictApplicationDescriptor` with
   endpoint/grant/response-type permissions and the PKCE requirement. The in-flight working
   tree is actively shrinking production seeds to just `wallow-web-client`, and
   `docs/operations/deployment.md` now says additional OIDC clients are created **through the
   UI**, not seeds. That direction is correct: seeds for the platform's own first-party
   clients, runtime registration for everyone else.
2. **Admin CRUD**: `ClientsController.cs` (`/v1/identity/clients`, incl. `rotate-secret`).
3. **Self-service developer registration**: `AppsController.cs`
   (`/v1/identity/apps/register`) — authenticated, permission-gated, `app-`-prefixed ids,
   **rejects public clients**.

**Recommendation:** follow the Keycloak/FusionAuth/PocketID consensus rather than open RFC
7591 registration. "Clients register themselves" should mean _an authenticated human (or
service account) in the Wallow admin/developer UI creates a client and receives a one-time
secret_ — exactly what `ClientsController`/`AppsController` already do — not an anonymous
`POST /register`. RFC 7591 §3 itself notes an open endpoint invites spam registrations unless
protected by initial access tokens; Keycloak's token-gated design is the model **if** protocol
DCR is ever wanted (it is genuinely useful only for ecosystems where software registers
itself, e.g. MCP-style integrations). Near-term gaps are policy, not plumbing: decide whether
external confidential clients stay `app-`-prefixed, whether public/SPA clients remain banned
(recommended: yes, require BFFs — §3), and what the first-party `wallow-` prefix bypass means
once true third parties exist.

---

## 3. The RP side: what bcordes.dev installs

[RFC 10017 §6.1](https://www.rfc-editor.org/rfc/rfc10017.html) settles this: the external site
should run a **BFF** — "the BFF MUST act as a confidential client" (§6.1.3.1), it "handles all
OAuth responsibilities and API interactions," keeps every token server-side, and issues its
own first-party cookie (Secure/HttpOnly MUSTs, §6.1.3.2). The alternatives the BCP ranks lower
(token-mediating backend, browser-only client) leave tokens or codes exposed to script; §6.1.4.3
gives BFF the "strongly recommended" endorsement for apps handling personal data.

**Wallow already ships this component.** `packages/sdk` is a per-RP, server-side BFF:
`openid-client`-based code+PKCE flow (`src/server/oidc.ts`, `pkce.ts`), sealed `__Host-wallow_bff`
session cookie (`src/server/config.ts`), `/bff/login|callback|user|logout` handlers plus an
`/bff/api/**` proxy (`src/server/bff-server.ts`, `proxy.ts`), CSRF for its own mutating routes
(`src/server/csrf.ts`), and cookie/Valkey session stores. Its configuration is already
issuer/client/secret-driven (`OIDC_ISSUER`, `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`,
`OIDC_REDIRECT_URI`, `BFF_API_BASE_URL` — `docs/integrations/typescript-sdk.md`), i.e. nothing
about it is intrinsically same-origin. The `external-origin-login.spec.ts` e2e runs this exact
SDK from a third origin today, and `docs/integrations/bff-pattern.md` already documents the
protocol for non-TypeScript BFFs.

**Recommendation:** productize rather than build — publish `@bc-solutions-coder/sdk` (or a
trimmed `wallow-client` package) to the real registry with a quickstart of the form "register
a client at wallow.dev, set five env vars, mount the BFF." The gap is packaging, docs, and a
non-workspace distribution channel, not architecture.

---

## 4. Discovery and the minimum-viable IdP endpoint surface

What a minimum-viable OP must expose, and where Wallow stands:

| Capability | Spec | Wallow today |
| --- | --- | --- |
| Provider metadata at `/.well-known/openid-configuration` | [OIDC Discovery 1.0 §4](https://openid.net/specs/openid-connect-discovery-1_0.html#ProviderConfig); OAuth analogue [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414) | ✅ served by OpenIddict defaults; issuer pinned to the public auth origin (`OpenIddictIssuerResolver.cs`), proxied to the auth origin by `apps/wallow-auth/src/app/routes/[.]well-known/$.ts` |
| Authorization endpoint | RFC 6749 §3.1 / OIDC Core §3.1.2 | ✅ `AuthorizationController.cs` |
| Token endpoint | RFC 6749 §3.2 | ✅ `TokenController.cs` (code, refresh, client_credentials) |
| JWKS + key rotation | [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517); OIDC Discovery `jwks_uri` | ✅ endpoint; ⚠️ production signing/encryption keys come from config — a rotation runbook (publish next key in JWKS before signing with it) is undocumented |
| UserInfo | [OIDC Core §5.3](https://openid.net/specs/openid-connect-core-1_0.html#UserInfo) | ✅ `UserinfoController.cs` |
| RP-initiated logout (`end_session_endpoint`, `id_token_hint`, `post_logout_redirect_uri`) | [OIDC RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html) | ✅ `LogoutController.cs` via OpenIddict's EndSession support |
| Front-channel logout | [OIDC Front-Channel Logout 1.0](https://openid.net/specs/openid-connect-frontchannel-1_0.html) | ✅ hand-rolled (OpenIddict 7 ships RP-initiated only; Wallow injects `frontchannel_logout_supported` into discovery and iframes each RP's `frontchannel_logout_uri` with `iss`+`sid`) |
| Back-channel logout | [OIDC Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) | ❌ absent — and it matters for external clients: back-channel "communication … can be more reliable than communication through the User Agent" (§1), since front-channel iframes need the RP's cookie in a third-party context that browsers increasingly block |
| Token revocation | [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009) | ✅/⚠️ OpenIddict supports it; verify the endpoint URI is enabled and advertised |
| Token introspection | [RFC 7662](https://www.rfc-editor.org/rfc/rfc7662) | ⚠️ optional — Wallow issues unencrypted JWTs (`DisableAccessTokenEncryption()`), so resource servers validate locally; introspection becomes relevant only for opaque tokens or third-party resource servers |
| Dynamic registration | RFC 7591/7592 | ❌ absent, deliberately (§2.3) |

Also present and worth keeping: per-user session listing/revocation (`SessionController.cs`,
`SessionRevocationMiddleware`), which is the operational backstop logout specs don't give you.

---

## 5. Multi-tenant white-label login pages

How the references do per-client branding of the hosted login page:

- **FusionAuth**: themes attach at tenant _and_ application level — "When a theme is selected,
  it will be used for this application instead of the tenant theme"
  ([Applications](https://fusionauth.io/docs/get-started/core-concepts/applications)); the
  hosted login pages resolve the theme from the `client_id`/tenant on the OAuth request.
- **Keycloak**: themes are realm-level by default, but "the theme configured for the realm is
  used, with the exception of clients being able to override the login theme"
  ([Working with themes](https://www.keycloak.org/ui-customization/themes)) — i.e. a per-client
  login-theme override resolved from the authorize request's client.
- **Auth0**: Organizations give "lightweight branding of the authentication experience" per
  business customer, with either the default Universal Login page "or … a login page specific
  to each organization using page templates"
  ([Organizations overview](https://auth0.com/docs/manage-users/organizations/organizations-overview);
  theming surface: [Customize themes](https://auth0.com/docs/customize/login-pages/universal-login/customize-themes)).
- **PocketID**: per-client logo uploaded on the client record, shown on its login prompt
  ([client examples](https://pocket-id.org/docs/client-examples/wg-access-server)).

The industry pattern is uniform: **branding is data attached to the client (or its tenant),
resolved server-side from the `client_id` on the authorize request, rendered by the IdP-hosted
page**. The client never hosts the login UI, which is exactly what makes "their logo + powered
by Wallow" trustworthy — the URL bar stays on wallow.dev.

**Wallow is already most of the way there**, with a two-layer model that mirrors
FusionAuth's tenant/application split:

- Fork-level identity: `packages/styles/branding.json` (`appName`, `appIcon`, `tagline`,
  ~26 oklch theme tokens per mode) — this is the "powered by Wallow" layer.
- Per-client overlay: the Branding module's `ClientBrandingController.cs`
  (`GET /v1/identity/apps/{clientId}/branding`, anonymous), consumed by
  `apps/wallow-auth/src/app/routes/login.tsx` via
  `mergeClientBranding(forkBranding, data, BASE_PATH)` keyed on the `client_id` that
  `AuthorizationController` forwards in the login redirect.

Gaps: the overlay is wired on `/login` only — `consent.tsx` explicitly notes the per-client
overlay is not applied there, and `register`/`forgot-password`/`mfa`/`error` pages inherit only
fork branding. For white-label external clients, the consent screen is the page where
third-party identity matters _most_ (it is the "bcordes.dev wants access to your Wallow
account" moment). Recommendation: thread the same `client_id` → branding merge through
`/consent` first, then the rest of the funnel, and let the admin client UI edit the branding
record alongside redirect URIs.

---

## 6. The C# implementation path — validated, not open

Because the repo already chose, this section is a validation rather than a selection:

- **Microsoft's current position**: ASP.NET Core Identity alone is not an OIDC server — "An
  OIDC server is typically preferred to provide a secure and scalable solution for single sign
  on," with self-host options enumerated separately
  ([Choose an identity management solution](https://learn.microsoft.com/en-us/aspnet/core/security/how-to-choose-identity-solution)).
  Its [solutions list](https://learn.microsoft.com/en-us/aspnet/core/security/identity-management-solutions)
  names **OpenIddict** (self-host, OSS Apache 2.0), **Duende IdentityServer** (self-host,
  commercial), and **Keycloak** (container, Apache 2.0) among others.
- **OpenIddict** (what Wallow runs): free, Apache-2.0, embeds in the app —
  server/client/validation stacks
  ([documentation.openiddict.com](https://documentation.openiddict.com/)); EF Core stores via
  `options.UseOpenIddict()` + `AddCore().UseEntityFrameworkCore()`, endpoints and flows via
  `AddServer` (`SetTokenEndpointUris`, `AllowAuthorizationCodeFlow`,
  `AddDevelopmentSigningCertificate`, `UseAspNetCore().Enable…Passthrough`), clients via
  `IOpenIddictApplicationManager` + `OpenIddictApplicationDescriptor`
  ([Creating your own server instance](https://documentation.openiddict.com/guides/getting-started/creating-your-own-server-instance)).
  This is a **perfect structural fit for a fork-first modular monolith**: the IdP is a module,
  not a sidecar container, and forks inherit it. Wallow uses the standard Core+EF mode — the
  right call; **degraded mode** (Core disabled, every check reimplemented via custom
  `IOpenIddictServerHandler<TContext>` event handlers, "MUST be enabled with extreme caution")
  exists for stateless proxy scenarios and should stay unused
  ([OpenIddict introduction](https://documentation.openiddict.com/introduction);
  [Chalet, degraded-mode walkthrough](https://kevinchalet.com/2020/02/18/creating-an-openid-connect-server-proxy-with-openiddict-3-0-s-degraded-mode/)).
  Custom authorize/consent behavior hooks in via the AspNetCore passthrough (Wallow's
  controllers) or event handlers (Wallow's discovery-document injection for front-channel
  logout) — both already exercised in `IdentityInfrastructureExtensions.cs`.
- **Duende IdentityServer**: source-available but **commercial** — free for dev/test, and the
  community edition caps at "up to 10 clients in production"
  ([duendesoftware.com/products/identityserver](https://duendesoftware.com/products/identityserver)).
  For a fork-first platform this license is contagious: every fork that exceeds the community
  tier owes Duende. Wrong fit; no reason to migrate.
- **Building the protocol by hand**: advise against, permanently. RFC 9700 exists precisely
  because a decade of deployments got redirect-URI validation, code injection, mix-up attacks
  (§4.4), and CSRF binding wrong; its normative MUSTs (exact-match URIs, PKCE enforcement,
  no open redirectors, `iss` in the authorization response per
  [RFC 9207](https://www.rfc-editor.org/rfc/rfc9207)) are table stakes an implementation
  inherits from OpenIddict for free.
- **Keycloak/FusionAuth/PocketID as products** remain relevant only as _design references_
  (§2, §5) — running one alongside the monolith would abandon Wallow's "identity is a module
  of the platform" premise and its org/tenant integration.

---

## 7. Inventory: exists vs missing

**Exists** (see §0 and the file paths throughout): OpenIddict 7 server with code+PKCE
(mandatory), refresh, client credentials; discovery + JWKS behind the public auth origin;
consent flow with real seeded scope descriptions; first-party consent bypass
(`wallow-` prefix / `Identity:FirstPartyClients`); admin client CRUD + secret rotation;
self-service `app-` registration (confidential only); seed-based first-party clients with
fail-closed validation (`PreRegisteredClientOptions.Validate`); per-client login branding;
RP-initiated + hand-rolled front-channel logout; per-user session revocation; MFA, external
inbound federation (Google/Microsoft), passwordless; the TS BFF SDK with sealed cookies,
CSRF, proxy, Valkey store; a protocol-level BFF guide for other stacks
(`docs/integrations/bff-pattern.md`); and the three-origin external-login e2e.

**Missing for productized external clients**:

1. **Back-channel logout** ([OIDC Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html)) — front-channel iframes are unreliable for third-party RPs under third-party-cookie blocking; the SDK would also need a `backchannel_logout_uri` handler.
2. **Client ↔ organization policy**: `AuthorizationController` errors with
   `client_not_bound_to_organization`; a true external client may need to exist _without_ a
   Wallow org, or with an org owned by the external party — currently unmodeled.
3. **Per-client branding on `/consent`** (and the rest of the auth funnel) — §5.
4. **Public/SPA client policy**: `AppsController` rejects public clients. Keep the ban and
   document "BFF required" (RFC 10017 §6.1), or add public+PKCE clients with the BCP's weaker
   guarantees — a decision, currently implicit.
5. **SDK distribution**: `workspace:*`-only today; external clients can't `npm install` it.
6. **Key-rotation runbook** for production signing keys (config-sourced today).
7. **Registration UX**: self-service flow for an outside developer (account → create app →
   one-time secret → docs), vs today's permission-gated internal surface.
8. **Revocation/introspection surface check**: confirm which OpenIddict endpoints beyond the
   four passthroughs are enabled and advertised in discovery.
9. **Consent persistence**: verify whether grants are remembered per user+client
   (OpenIddict authorizations store) or re-prompted every login.

---

## 8. Answering the original confusions, verbatim

- _"bcordes.dev → wallow.dev/auth requires CSRF"_ → No. The cross-domain hop is a top-level
  redirect into a protocol whose response can only land on an exact-match registered
  `redirect_uri`, session-bound by `state`/`nonce`/PKCE (RFC 9700 §2.1, §4.7.1). Wallow's
  CSRF tokens protect the RP's own `/bff/**` mutations, which are same-origin.
- _"The BFF sends the user token to the wallow platform"_ → The RP's BFF sends **an access
  token that Wallow itself issued**, as a Bearer header on server-to-server calls — obtained
  by redeeming a one-time code, never by forwarding a browser credential (RFC 6749 §4.1.3;
  RFC 10017 §6.1.3.1).
- _"How do we share the session across domains?"_ → You don't. wallow.dev's Lax session
  cookie makes repeat authorize redirects silent (that _is_ SSO); each RP mints its own
  first-party session from the ID token. Logout across sites is what the logout specs (§4)
  exist for — which is why back-channel logout is gap #1.

---

## 9. Ordered recommendation

1. **Adopt the mental model above as doctrine** (this doc; consider distilling §1 into
   `docs/architecture/authentication.md`).
2. **Finish the branding funnel**: per-client branding on `/consent`, then register/MFA/error
   pages; admin UI for the branding record next to redirect URIs (§5).
3. **Decide the client↔org model for external clients** and remove the
   `client_not_bound_to_organization` cliff (§7.2) — this is the one true product-modeling
   decision.
4. **Add back-channel logout** (OP side + SDK handler) and keep front-channel as best-effort
   (§4).
5. **Productize the RP SDK**: publish to a real registry, write the external-client
   quickstart ("register, five env vars, mount"), keep `docs/integrations/bff-pattern.md` as
   the polyglot fallback (§3).
6. **Build the self-service registration journey** on top of `AppsController` (developer
   account → app → one-time secret → scopes), staying admin/UI-gated; skip RFC 7591 unless a
   software-registration ecosystem emerges, and copy Keycloak's initial-access-token design if
   it does (§2).
7. **Operational hardening**: key-rotation runbook, revocation endpoint check, consent
   persistence check, decide `private_key_jwt` support (RFC 7523) for high-assurance clients
   (§7.6–9).
8. **Write the policy line on public clients**: "external browser apps must run a BFF"
   (RFC 10017 §6.1.4.3), enforced today by `AppsController`'s rejection — make it documented
   policy instead of incidental behavior.

Nothing on this list requires replacing anything. Wallow's architecture — OpenIddict module +
hosted auth app + per-RP BFF SDK — is precisely the architecture RFC 10017 and RFC 9700
recommend; the work remaining is product surface, not protocol.
