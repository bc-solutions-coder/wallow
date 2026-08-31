# Revocation completeness: end-session, user deactivation, SDK refresh failure

**status: active**

Issue: #145 (parent #131). Fixed review point: `5d0a71b1`.

## Problem

Three revocation gaps:

1. **End-session revokes nothing.** `LogoutController` signs the identity cookie out and
   notifies relying parties, but every access and refresh token minted under the session's
   `sid` survives — a refresh after logout succeeds.
2. **Deactivating a user revokes nothing.** `UserManagementService.DeactivateUserAsync` only
   sets lockout; issued tokens keep working until they expire.
3. **The Valkey session denylist is inert.** `SessionRevocationMiddleware` reads a
   `wallow.session` cookie nothing ever sets, so the `session:revoked:*` keys
   `SessionService` writes are never consulted.

On the SDK side, a failed refresh answers 401 but leaves the session record in the store and
the session cookie on the browser, so the dead session is replayed on every request.

## Design

### sid → token chain (the core change)

Tokens today chain inconsistently: first-party sign-ins with an organization chain to a
per-login **ad-hoc** authorization; org-less first-party sign-ins chain to nothing; bound
(third-party) sign-ins chain to the user's shared **permanent** consent record. The permanent
record is shared across browser sessions, so revoking it at logout would kill the user's
other sessions — unacceptable.

New rule: **every interactive sign-in mints a per-login ad-hoc authorization carrying the
session's `sid` (and org, when present), and all its tokens chain to it.** Permanent
authorizations become pure consent records — still created/widened by `StoreConsentAsync`,
still what `ConnectedApplicationService` lists, never a chain-point.

- `AuthorizationProperties.SessionId` (`"sid"`) joins `OrganizationId`; the Api/Infrastructure
  descriptor-extension twins gain `SetSessionId`/`GetSessionId`.
- `AuthorizationController`: mint `sid` before authorization creation; always create the
  ad-hoc authorization (first-party and bound alike, org or no org) with the sid stamped;
  `identity.SetAuthorizationId(adhocId)` unconditionally.
- OpenIddict validates the chained authorization's status at the refresh grant, so revoking
  the ad-hoc row makes refresh answer `invalid_grant` with no further code; token-entry
  revocation already produces bearer 401 via the validation path.

### End-session revocation

`IAccessRevoker.RevokeSessionAsync(Guid userId, string sessionId)`: walk
`authorizationManager.FindBySubjectAsync`, match `GetSessionId() == sessionId`, revoke each
chained token (`FindByAuthorizationIdAsync`) then the authorization. `LogoutController`
(GET phase one, while the cookie still carries sid + user id, and POST) calls it. No realtime
hang-up: realtime connections cannot be attributed to one sid, and hanging up all of the
user's connections would touch their other sessions.

### Consent withdrawal keeps killing tokens

With tokens no longer chained to the permanent record,
`ConnectedApplicationService.WithdrawAsync` revokes the permanent row **and** walks
`tokenManager.FindBySubjectAsync`, revoking every token of that user issued to that
application (plus that application's ad-hoc rows, so nothing valid is left chained). The
existing `Withdraw_KillsRefreshAndBearerAccess_AndEmptiesTheList` integration test pins this.

### Deactivation

`IAccessRevoker.RevokeUserAsync(Guid userId)`: revoke every token
`FindBySubjectAsync` yields, revoke the user's ad-hoc authorizations, and hang up realtime
per membership (`IRealtimeAccessRevoker.RevokeAsync(userId, orgId)` for each active
membership — no user-wide realtime method needed). Permanent consents are untouched:
deactivation is reversible and re-consent is not the point.
`UserManagementService.DeactivateUserAsync` calls it after setting lockout.

### Denylist deletion

Delete `SessionRevocationMiddleware` (+ `UseSessionRevocation` wiring in
`Wallow.Api/Program.cs`), the `session:revoked:*` writes in `SessionService`
(`RevokedKeyPrefix`, TTL, both write sites), and `SessionRevocationMiddlewareTests`; adjust
`SessionServiceTests`. `SessionActivityMiddleware`, `SessionController`, and the
ActiveSession ledger stay — they are bookkeeping, not a denylist.

### SDK refresh failure

Any refresh failure on the proxy path (proactive `ensureFreshSession` or reactive
`forceRefreshSession`) surfaces as a typed refresh-failure error; the proxy then destroys the
store record (`store.destroy(ref)`), clears the session cookie, its CSRF companion and chunk
cookies (the same `clearSession` logout uses, exported to the proxy), and answers 401. Fail
closed: a transient IdP outage costs a silent re-login round-trip, not a security hole.

`CookieSessionStore` is documented as single-replica / development-only, with the revocation
ceiling spelled out: no server-side record exists to destroy, so a session revoked at the OP
lives until its access token expires and the refresh is refused — the ceiling is the
access-token lifetime.

## Test seams

- **Backend integration** (`Wallow.Identity.IntegrationTests`, OIDC front door only):
  - logout → refresh answers `invalid_grant`; bearer call with the session's access token
    answers 401; a second browser session of the same user keeps refreshing.
  - deactivate → refresh `invalid_grant`, bearer 401.
  - existing consent-withdrawal and revocation suites stay green under the new chaining.
- **SDK node specs** (`packages/sdk`, vitest node): refresh failure leaves no session record
  in the store, clears the session cookie, response is 401 — proactive and reactive paths.

## Out of scope

`SessionActivityMiddleware` (inert but harmless bookkeeping), realtime hang-up per sid,
back-channel logout, a user-wide `IRealtimeAccessRevoker` method.
