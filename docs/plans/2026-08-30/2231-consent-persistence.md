**status: completed**

# Consent persistence: permanent-authorization chain, delta consent, prompt, connected apps

Issue: #143 (parent #131; builds on #132's consent POST protocol and the research in
`docs/plans/2026-08-29/research-consent-persistence.md`).

## What exists today

`AuthorizationController` already chains tokens (`identity.SetAuthorizationId`), detects
first-party by OpenIddict `ConsentTypes.Implicit`, and records an AdHoc authorization for
first-party sign-ins so revocation can find the organization. The third-party path has three
gaps:

1. The stored-consent lookup enumerates `FindBySubjectAsync` and accepts any **Valid**
   authorization whose scopes cover the request — it never filters by
   `AuthorizationTypes.Permanent`, so a leftover ad-hoc record can satisfy consent.
2. Scope growth creates a **second** permanent authorization instead of updating the one that
   exists; the consent screen always re-asks for the full granted set, not the delta.
3. `prompt=consent` / `prompt=none` are ignored, there is no self-service list/revoke surface,
   and `docs/integrations/bff-pattern.md` blames consent repetition on `offline_access`.

## Design

### Backend — authorize flow (AuthorizationController)

- Enumerate the user's **Valid + Permanent** authorizations for the application; compute
  `union` = ∪(their scopes) and remember the newest record. `delta = grantedScopes − union`.
- Order of decision (third-party only):
  1. A POSTed, redeemed consent decision wins (it must override `prompt=consent`, or the
     screen loops forever — the returnUrl carries the original query). Denied → Forbid
     `consent_required`. Granted → **union-update-or-create**: update the newest permanent
     authorization's scopes to `union ∪ granted` via `UpdateAsync(auth, descriptor)`
     (create when none exists), then proceed.
  2. `prompt=none` with a non-empty delta (or no permanent authorization) → Forbid
     `consent_required` (no redirect to the screen — the RP asked for no UI).
  3. `prompt=consent`, or non-empty delta → `RedirectToConsent`, whose `scope` query carries
     the **delta** when non-empty (else the full granted set, for a forced re-confirmation) —
     the auth app's authorize-context read already narrows the described scopes by that
     parameter (#142).
  4. Otherwise consent is covered: chain to the existing permanent authorization's id.
- First-party flow unchanged (implicit consent, AdHoc + organization).

### Backend — connected applications surface

`MeAuthorizationsController` at `v1/identity/me/authorizations` (mirrors
`SessionController`), delegating to a new Infrastructure service
`IConnectedApplicationService` (Identity is service-heavy thin CQRS):

- `GET` → `ConnectedApplicationDto(id, clientId, displayName, scopes, createdAt)` per
  Valid + Permanent authorization of the caller.
- `DELETE {id}` → 404 unless the authorization exists, belongs to the caller and is
  Permanent; else `TryRevokeAsync` + `IOpenIddictTokenManager.RevokeByAuthorizationIdAsync`
  (refresh dies with `invalid_grant`; token-entry validation kills live access tokens on the
  next API call), 204.

This is Identity's own account-console API — the seam check in the research doc §4 approves
it; it is not a new seam leak.

### Frontend + docs

- Regenerate `packages/sdk/openapi/v1.json` + generated client.
- wallow-web: `ConnectedAppsSection` in `features/settings` (the settings screen already
  composes sections), exposed through the feature's `api.ts` seam, composed into
  `/dashboard/settings`, with a withdraw button per row + browser-mode spec.
- Fix the bff-pattern Troubleshooting row: consent repeats when the request carries a scope
  outside the stored permanent authorization, when `prompt=consent` is sent, or after a
  denial — `offline_access` plays no part.

## TDD seams (pre-agreed)

1. **OIDC flow over HTTP** — `AuthorizationCodeFlowHarness` in
   `Wallow.Identity.IntegrationTests/OAuth2/` (new `ConsentPersistenceTests`,
   `ConnectedApplicationTests`): second login same scopes → straight to code; grown scope set
   → consent asks only the delta and stores the union (one permanent record); `prompt=consent`
   forces the screen; `prompt=none` fails `consent_required`; tokens carry the permanent
   authorization id (`oi_au_id` claim); withdraw → refresh fails `invalid_grant`, bearer call
   401s next request.
2. **Settings screen in the browser** — SdkHarness-driven spec for the connected-apps
   section (list + withdraw + invalidation), mirroring `settings.test.tsx`.

## Steps

1. Integration tests (red) → controller rework (green).
2. `me/authorizations` tests (red) → service + controller (green).
3. SDK regen; settings section + spec; docs row fix.
4. Full gates: `./scripts/run-tests.sh api`, project-path integration run, `pnpm check`;
   two-axis /code-review against the pre-#143 fixed point; push; close #143.
