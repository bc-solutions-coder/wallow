**status: completed**

# Research: security audit of the external-RP login flow

Wayfinder research ticket for map issue #112. Audits the end-to-end flow an external relying
party (bcordes.dev) runs against Wallow's IdP — `bcordes.dev` → `wallow.dev/auth` (authorize,
login, consent) → `/callback` on the RP's BFF → SDK session → `/api` proxy calls → logout —
against RFC 9700 (OAuth 2.0 Security BCP), RFC 6749/6819, OpenID Connect Core 1.0, RFC 10017
(OAuth 2.0 for Browser-Based Applications), RFC 7591/7009, and OpenIddict 7 defaults.

This document **extends** `docs/plans/2026-08-29/1254-external-idp-research.md` (which
inventories the OpenIddict configuration, the endpoint set, split-horizon discovery and the
first-party/third-party consent split). It does not restate that inventory; it reads each
component against the spec clauses and produces a findings list with severity, location and
clause. All file paths are repo-relative. **No product code was modified.**

## Method

- Repo evidence: read every file on the flow's path (Identity module OpenIddict wiring,
  controllers, redirect-URI validator, client/app registration, pre-registered client sync,
  the BFF SDK server entry, wallow-auth consent screen and route layout, the API security
  headers middleware).
- Spec text fetched from primary sources via ref.tools: RFC 9700, RFC 7591, RFC 7009,
  RFC 10017, OpenIddict token-storage docs and `OpenIddictApplicationManager` source
  (`openiddict/openiddict-core` `dev` branch).
- Severity scale: **HIGH** = exploitable by an unprivileged party against a logged-in user
  or client; **MEDIUM** = violates a spec MUST/SHOULD with realistic misuse; **LOW** =
  hardening / defense-in-depth or documentation drift; **INFO** = conforms, recorded for
  the decision.

## Findings

| # | Severity | Finding | Location | Spec clause |
|---|----------|---------|----------|-------------|
| F1 | **HIGH** | Consent is decided by an unauthenticated GET query flag. `/connect/authorize` is `[AllowAnonymous]`; `consent_granted=true` on the request creates a **permanent** authorization for `(subject, client, scopes)` and continues the code flow. A third-party client (or any page that can navigate a logged-in user) can craft `…/connect/authorize?client_id=X&redirect_uri=<X's registered URI>&…&consent_granted=true` and obtain a code and a lasting grant without the user ever seeing the consent screen. The consent UI itself submits by top-level `location.href` GET with no server-issued token. | `api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthorizationController.cs` (`consent_granted` branch, `AuthorizationTypes.Permanent`); `apps/wallow-auth/src/features/consent/components/ConsentScreen.tsx` (`buildConsentSubmitUrl` navigation); `packages/sdk/src/auth-oidc.ts` (`buildConsentSubmitUrl`) | OIDC Core §3.1.2.4 (AS MUST obtain End-User consent for non-pre-authorised clients); RFC 6749 §10.12 (CSRF against the AS); RFC 9700 §4.7 / §4.11.2 (AS MUST authenticate the user *and* only auto-redirect to trusted redirect URIs) |
| F2 | **MEDIUM** | `registration_access_token` is the client secret. `OpenIddictDeveloperAppService` returns `RegistrationAccessToken: clientSecret` and `AppsController` echoes it. There is no RFC 7592 management endpoint, so the field has no consumer and leaks the secret under a second name; the response also omits `client_secret_expires_at`, which is REQUIRED whenever `client_secret` is issued. | `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/OpenIddictDeveloperAppService.cs`; `api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AppsController.cs` (`register`) | RFC 7591 §3.2.1 (`client_secret_expires_at` REQUIRED); RFC 7592 §3 (registration access token belongs to the management endpoint) |
| F3 | **MEDIUM** | Admin client and seed paths accept any absolute URI as a redirect / post-logout URI. `ClientsController.Create/Update` and `PreRegisteredClientSyncService` call `new Uri(uri)` with no scheme check, so `http://` non-loopback and custom schemes register. OpenIddict's manager only validates "absolute, no fragment, no `iss` query" — it does not enforce HTTPS. The developer `apps/register` path *does* enforce HTTPS-or-localhost. | `api/src/Modules/Identity/Wallow.Identity.Api/Controllers/ClientsController.cs` (`descriptor.RedirectUris.Add(new Uri(uri))`); `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/PreRegisteredClientSyncService.cs` | RFC 9700 §2.6 / §4.1.3 (AS MUST NOT allow `http` redirect URIs except native loopback); RFC 7591 §5 |
| F4 | **MEDIUM** | Login and consent pages ship no clickjacking defence. `SecurityHeadersMiddleware` sets `X-Frame-Options: DENY` and a CSP on the **.NET API** only; the HTML the user actually sees at `wallow.dev/auth` is rendered by `apps/wallow-auth` (TanStack Start SSR), which sets no `X-Frame-Options` / `frame-ancestors` anywhere. No reverse-proxy config in `docker/` adds them either. | `api/src/Wallow.Api/Middleware/SecurityHeadersMiddleware.cs`; `apps/wallow-auth/` (no header emission found) | RFC 9700 §4.16 (AS MUST prevent clickjacking: `X-Frame-Options` **and** CSP `frame-ancestors`) |
| F5 | **LOW** | `returnUrl` / post-logout validation is origin-level, not exact, and served from a 5-minute cache. `OpenIddictRedirectUriValidator` compares only `scheme://host[:port]` of every client's registered redirect + post-logout URIs (plus `AuthUrl`), and with no `client_id` it accepts the union of **all** clients' origins. Used by `AccountController` (incl. the anonymous `GET redirect-uri/validate` oracle) and `LogoutController` for post-login/post-logout `returnUrl`, not for OAuth `redirect_uri` (OpenIddict does exact match there). A removed URI stays valid up to 5 minutes; any path on a registered origin is an accepted return target. | `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/OpenIddictRedirectUriValidator.cs`; `AccountController.cs` (`redirect-uri/validate`); `LogoutController.cs` | RFC 9700 §2.1 / §4.1.3 (exact string match), §4.11.2 (only trusted redirect targets); OIDC Core §3.1.2.1 |
| F6 | **LOW** | Secret rotation has no overlap window. `ClientsController.RotateSecret` and `OpenIddictServiceAccountService.RotateSecretAsync` replace the hash immediately; a running BFF with the old `OIDC_CLIENT_SECRET` fails token/refresh calls until redeployed. Not a spec violation (RFC 7591 §5 only says do not share one secret across instances) but an operational hazard for the external RP. | `ClientsController.cs` (`RotateSecret`); `api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/OpenIddictServiceAccountService.cs` | RFC 9700 §2.5 (client auth), RFC 7591 §5 (informative) |
| F7 | **LOW** | Per-client PKCE requirement is only set on seed clients. `Requirements.Features.ProofKeyForCodeExchange` is attached in `PreRegisteredClientSyncService` only; `ClientsController`, `AppsController` and `OpenIddictDeveloperAppService` register without it. The **global** `RequireProofKeyForCodeExchange()` already refuses code requests without `code_challenge`, so RFC 9700 §4.8.2 is satisfied today; this is defense-in-depth against a future relaxation of the global switch. | `PreRegisteredClientSyncService.cs`; `IdentityInfrastructureExtensions.cs` (`AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()`) | RFC 9700 §2.1.1, §4.8.2 |
| F8 | **LOW** | SDK session cookie defaults to `SameSite=Lax`; RFC 10017 says SHOULD `Strict`. `COOKIE_SAMESITE=strict` is available (`packages/sdk/src/server/config.ts`). The login-transaction cookie `_tx` is deliberately `Lax` (it must ride the cross-site callback redirect) — correct. The cookie uses the `__Host-` prefix (spec suggests `__Host-Http-`, a proposed prefix not yet widely implemented — informative). | `packages/sdk/src/server/config.ts`; `packages/sdk/src/server/handlers.ts` | RFC 10017 §6.1.3.2 (SHOULD `SameSite=Strict`, `__Host-` prefix, encrypted session) |
| F9 | **LOW** | UI client-type drift. `OrganizationDetail.tsx` shows a `clientType` select defaulting to `public` whose value is dropped by `toVariables`; `ClientsController.Create` hardcodes `Confidential` and `CreateClientRequest` has no `ClientType`. `RegisterAppForm.tsx` also defaults to `public`, which `apps/register` rejects. Misleading, and it lets an operator believe a public client was created. | `apps/wallow-web/src/features/organizations/components/OrganizationDetail.tsx`; `apps/wallow-web/src/features/apps/components/RegisterAppForm.tsx`; `ClientsController.cs` | RFC 10017 §6.1.3.1 (BFF MUST be a confidential client) — conformance is correct, the UI is wrong |
| F10 | **INFO** | No CORS is the right answer. There is no `AddCors`/`UseCors` in `api/src`. The BFF is same-origin with its SPA and talks to Wallow server-to-server; the browser only follows top-level redirects to `/connect/authorize` and `/connect/logout`. RFC 9700 explicitly forbids CORS at the authorization endpoint. Adding CORS would be a seam leak per `.claude/rules/IDENTITY.md`. | `api/src` (grep negative) | RFC 9700 §2.6 (CORS MUST NOT be supported at the authorization endpoint); RFC 10017 §6.1 |
| F11 | **INFO** | state / nonce / PKCE are correctly bound. `handlers.ts` seals `state`, `nonce` and the PKCE verifier into `${cookie}_tx` (HttpOnly, Secure, Lax, Max-Age 600); `/callback` returns 400 unless the tx cookie is present and `state` matches, then calls openid-client `authorizationCodeGrant` with `expectedState`, `expectedNonce`, `pkceCodeVerifier`. openid-client validates `iss` in the callback when the discovery document advertises `authorization_response_iss_parameter_supported`. Single AS, so RFC 9700 §4.4.2 mix-up defence is not required. | `packages/sdk/src/server/handlers.ts`; `packages/sdk/src/server/oidc.ts`; `packages/sdk/src/server/txstate.ts` | RFC 9700 §2.1.1, §4.5.3, §4.7.1; OIDC Core §3.1.2.7 (nonce) |
| F12 | **INFO** | BFF CSRF and outbound allowlist conform. Double-submit `-csrf` cookie + `x-csrf-token` header, timing-safe compare, enforced on POST/PUT/PATCH/DELETE before refresh or forward; `/bff/logout` is POST-only. Proxy accepts only `/api/*` at a segment boundary, rejects `..`, and `upstreamTarget` enforces `origin === apiBase.origin && pathname.startsWith(basePath)`; `Cookie` is never forwarded (allowlist); hop-by-hop headers stripped; `redirect: "manual"`. | `packages/sdk/src/server/csrf.ts`; `packages/sdk/src/server/proxy.ts`; `packages/sdk/src/server/bff-server.ts` | RFC 10017 §6.1.3.3 (CSRF MUST), §6.1.3.6 (outbound allowlist MUST) |
| F13 | **INFO** | Token hygiene conforms. Access 15 min, refresh 7 d with sliding expiration **disabled**, id 10 min; audience `wallow-api`; `EnableTokenEntryValidation()` gives immediate revocation at the API; OpenIddict rolling refresh tokens + reuse detection are on by default (token storage not disabled). Access tokens are unencrypted JWTs (`DisableAccessTokenEncryption()`) so JWKS validation works — acceptable for a bearer-over-TLS design. | `IdentityInfrastructureExtensions.cs` | RFC 9700 §2.2.2 / §4.14.2 (rotation or sender-constraining), §2.3 / §4.10.2 (audience), RFC 7009 §2 |
| F14 | **INFO** | Endpoint surface (lead f). Enabled: authorize, token, end-session, userinfo, **revocation**. Introspection is **not** enabled — correct, since resources validate locally via JWKS. Every client kind (seed web, developer app, service account) is granted `Permissions.Endpoints.Revocation`, so the RP can revoke its refresh token on logout. Front-channel logout metadata is injected into discovery. | `IdentityInfrastructureExtensions.cs`; `OpenIddictEndpointUris.cs`; `OpenIddictDeveloperAppService.cs`; `PreRegisteredClientSyncService.cs` | RFC 7009 §2 (MUST revoke refresh tokens), RFC 9700 §4.14 |
| F15 | **INFO** | Client secrets are hashed at rest (lead i). Wallow passes the plaintext secret to `IOpenIddictApplicationManager.CreateAsync(descriptor)` / `UpdateAsync(app, secret)`; OpenIddict's default manager obfuscates it with PBKDF2 (random salt, versioned header) and verifies with `CryptographicOperations.FixedTimeEquals`, re-hashing on parameter change. Secrets are 32 random bytes (service accounts) / cryptographically random strings (developer apps), shown once. | `OpenIddictApplicationManager.ObfuscateClientSecretAsync` / `ValidateClientSecretAsync` (openiddict-core `dev`); `OpenIddictServiceAccountService.cs`; `OpenIddictDeveloperAppService.cs` | RFC 9700 §2.5; OpenIddict manager source |
| F16 | **INFO** | Transport policy fails closed. Plain-HTTP endpoints are allowed only in `Development`/`Testing` or with `OpenIddict:AllowPlainHttpEndpoints=true`; issuer = `OpenIddict:Issuer` else `AuthUrl`. Identity's own cookie is `HttpOnly; Secure; SameSite=Lax`. | `OpenIddictTransportSecurityPolicy.cs`; `OpenIddictIssuerResolver.cs` | RFC 6749 §1.6, RFC 7009 §2, RFC 9700 §4.16 |

## Answers to the ticket's leads (a)–(i)

- **(a) `registration_access_token` = client secret** — confirmed, F2. Drop the field (no
  RFC 7592 endpoint exists) and add `client_secret_expires_at` (`0` = never) to the
  registration response.
- **(b) No `AddCors`** — correct as-is, F10. The external RP never makes a cross-origin XHR to
  Wallow; the BFF does the cross-origin work server-side. Do not add CORS.
- **(c) `ClientsController` hardcodes confidential vs UI select** — confirmed, F9. Decision:
  keep the API confidential-only for the RP use case (RFC 10017 requires it) and delete the
  cosmetic select, or add `ClientType` to `CreateClientRequest` and honour it. The former is
  the smaller, safer change.
- **(d) Redirect-URI exactness + 5-min cache** — OAuth `redirect_uri` on `/connect/authorize`
  is exact-matched by OpenIddict (`ValidateRedirectUriAsync`, ordinal string compare; the
  loopback-port relaxation applies only to `ApplicationTypes.Native`). The origin-level
  matcher and cache apply only to Wallow's own `returnUrl` / post-logout handling — F5.
- **(e) state / nonce / PKCE binding, `_tx` Lax, `isSafeReturnUrl`** — conforms, F11. The
  `_tx` cookie *must* be Lax (Strict would drop it on the cross-site redirect back from
  Wallow). `isSafeReturnUrl` only admits same-origin relative paths (no `//`, no scheme).
- **(f) Endpoints enabled / advertised, revocation permissions** — F14. Revocation on and
  granted to all client kinds; introspection off by design.
- **(g) Issuer / discovery under path-based hosting; is `OIDC_METADATA_URL` sufficient?** —
  Yes. `OpenIddict:Issuer` should be the *public* URL including the path prefix
  (e.g. `https://wallow.dev/auth`); `OIDC_METADATA_URL` lets the RP's BFF fetch discovery
  over an internal URL while `rebaseToIssuer` re-pins the browser-facing `authorization` and
  `end_session` endpoints to the public issuer. The wallow-auth `/.well-known/$` passthrough
  route keeps discovery and JWKS on the public origin. The one hard requirement is that the
  `iss` claim in tokens equals the issuer the BFF discovered — which `SetIssuer` guarantees.
- **(h) Is `/bff/*` configurable?** — No. `WALLOW_BFF_MOUNT = "/bff"` and
  `WALLOW_API_MOUNT = "/api"` are constants in `packages/sdk/src/server/bff-server.ts`, and
  the browser client hardcodes `/bff/login`, `/bff/logout`, `/bff/user`. An external RP must
  reserve those paths or fork the SDK. Configurability is a product decision, not a
  security gap; note that a configurable mount must keep the segment-boundary check.
- **(i) Secret storage / hashing, rotate-secret semantics** — F15 (hashed, PBKDF2,
  constant-time) and F6 (rotation is immediate, no overlap).

## Recommendation

1. **Fix F1 before any external RP goes live.** Consent must be a POST carrying a
   server-issued, single-use, user-bound token (or an antiforgery token tied to the Identity
   cookie), and `AuthorizationController` must reject `consent_granted` arriving on a GET.
   Alternatively store the pending authorize request server-side and have the consent POST
   reference it by id. This is the only exploitable finding.
2. Fix F2, F3, F4 together as one hardening ticket: drop `registration_access_token`, add
   `client_secret_expires_at`, apply the `apps/register` HTTPS-or-localhost rule to
   `ClientsController` and `PreRegisteredClientSyncService`, and emit
   `X-Frame-Options: DENY` + `Content-Security-Policy: frame-ancestors 'none'` from
   wallow-auth's server (or its ingress).
3. F5–F9 are backlog-grade hardening; F9 (delete the cosmetic client-type select) is a
   five-minute change worth bundling with F2.
4. Everything the external RP actually depends on — PKCE, state/nonce, refresh rotation,
   audience restriction, revocation, BFF CSRF and outbound allowlist, split-horizon discovery,
   secret hashing — conforms (F10–F16). Nothing here blocks #124 beyond F1.

## Sources

- RFC 9700, OAuth 2.0 Security Best Current Practice — https://www.rfc-editor.org/rfc/rfc9700.html
- RFC 10017, OAuth 2.0 for Browser-Based Applications — https://www.rfc-editor.org/rfc/rfc10017.html
- RFC 7591, OAuth 2.0 Dynamic Client Registration — https://www.rfc-editor.org/rfc/rfc7591.html
- RFC 7009, OAuth 2.0 Token Revocation — https://www.rfc-editor.org/rfc/rfc7009.html
- OpenID Connect Core 1.0 §3.1.2 — https://openid.net/specs/openid-connect-core-1_0.html
- OpenIddict token storage — https://documentation.openiddict.com/configuration/token-storage
- OpenIddict `OpenIddictApplicationManager` (secret hashing, redirect-URI validation) —
  https://github.com/openiddict/openiddict-core/blob/dev/src/OpenIddict.Core/Managers/OpenIddictApplicationManager.cs
- Prior research: `docs/plans/2026-08-29/1254-external-idp-research.md`
