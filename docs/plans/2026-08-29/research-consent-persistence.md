**status: completed**

# Research: consent persistence in Wallow's OpenIddict authorization store

_Resolves wayfinder research ticket #116 (map #112). Extends
`docs/plans/2026-08-29/1254-external-idp-research.md` § "open question 9" — that document asked
whether grants are remembered per user+client or re-prompted every login; this one answers it
against the code and the OpenIddict primary sources and lists what is missing for "a bcordes.dev
user consents once, and can take it back". No product code was modified._

Sources: OpenIddict 7.6.0 (`api/Directory.Packages.props`), the OpenIddict documentation and
`openiddict-core` `dev` source, and the Velusia reference sample. Every third-party claim carries
its URL; every repo claim carries its path.

---

## 1. Headline answers

| Question (ticket) | Answer |
| --- | --- |
| Permanent or ad-hoc authorizations for non-first-party clients? | **Permanent.** `AuthorizationController` creates `AuthorizationTypes.Permanent` / `Statuses.Valid` on `consent_granted=true`, and a later authorize with the same-or-fewer scopes finds it and skips the consent screen. A user already consents once today. |
| How does the `wallow-` bypass interact with the stored-authorization lookup? | It **skips the lookup and the create entirely**; first-party clients never get a permanent authorization. OpenIddict still creates an **ad-hoc** authorization per sign-in for them (and for third-party clients too) because Wallow never links tokens to its permanent one. |
| Scope-change semantics | Superset check: any granted scope outside the stored set re-prompts for the **whole** granted set, then stores a **second** permanent authorization. No delta prompt exists. |
| User-facing revocation | **None.** No endpoint or page lists or revokes OpenIddict authorizations, nothing in product code calls `TryRevokeAsync` / `RevokeByAuthorizationIdAsync`, and because tokens are not linked to the permanent authorization, revoking it would not revoke a single refresh token. |

The consent record and the token chain are two unconnected things in Wallow today. That is
the one structural gap; everything else is polish.

---

## 2. What exists — `AuthorizationController.cs`

File: `api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthorizationController.cs`.

### 2.1 First-party decision

`isFirstParty` is true when the client id starts with `wallow-` (`FirstPartyClientPrefix`) or is
listed in `Identity:FirstPartyClients` (empty in `api/src/Wallow.Api/appsettings.json`; the
container e2e stack sets `Identity__FirstPartyClients__0: "wallow-web-client"` in
`docker/docker-compose.test.yml`, with a comment explaining why `bff-example-client` is
deliberately left third-party so the consent path stays covered).

OpenIddict's own per-application `ConsentType` is **not used**: neither
`OpenIddictDeveloperAppService.cs` nor `PreRegisteredClientSyncService.cs` (both under
`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Services/`) sets
`OpenIddictApplicationDescriptor.ConsentType`, and the controller never reads it. Only the
integration fixtures set `ConsentTypes.Implicit`
(`api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/IdentityFixture.cs`), where it is a
no-op. The Velusia sample drives its whole consent switch off `GetConsentTypeAsync`
(`Explicit` / `Implicit` / `External` / `Systematic`) —
https://raw.githubusercontent.com/openiddict/openiddict-samples/dev/samples/Velusia/Velusia.Server/Controllers/AuthorizationController.cs.
Wallow's prefix+config rule is a parallel, coarser mechanism.

### 2.2 The stored-authorization lookup (third-party only)

A hand-rolled loop over `authorizationManager.FindBySubjectAsync(userId)` keeps an authorization
when `GetApplicationIdAsync == applicationId`, `GetStatusAsync == Statuses.Valid`, and
`grantedScopes.All(s => authorizedScopes.Contains(s))`. Two observations:

- This reproduces OpenIddict's `FindAsync(subject, client, status, type, scopes)` superset
  semantics (`HasScopesAsync` is `ToHashSet(StringComparer.Ordinal).IsSupersetOf(scopes)`,
  https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Core/Managers/OpenIddictAuthorizationManager.cs;
  the EF store applies the same in-memory check,
  https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.EntityFrameworkCore/Stores/OpenIddictEntityFrameworkCoreAuthorizationStore.cs)
  but **does not filter on `Type`**. The Velusia sample passes `type: AuthorizationTypes.Permanent`.
- Because the type is not filtered, a **Valid ad-hoc** authorization (see § 2.4) satisfies the
  lookup too. Consequence: revoking the permanent "consent" record would not re-prompt the user
  while any ad-hoc authorization from a previous login for that client still has a live refresh
  token (up to `OpenIddict:RefreshTokenLifetimeDays`, default 7 —
  `IdentityInfrastructureExtensions.cs`).

### 2.3 Creating the permanent authorization

On `consent_granted=true` with no match, the controller builds an
`OpenIddictAuthorizationDescriptor { ApplicationId, CreationDate, Status = Valid, Subject,
Type = Permanent, Scopes = grantedScopes }` and calls `CreateAsync`. Scopes are the
**granted** set after role-based narrowing (`ResolveGrantedScopesAsync`), which is correct: the
record describes what was actually issued. `consent_denied=true` returns
`Errors.ConsentRequired` and stores nothing (no `Statuses.Rejected` record), so a denial is
re-asked next time — reasonable.

There is a **second, unreachable** create block after `BuildClaimsIdentityAsync`, guarded by
`!isFirstParty && !hasValidAuthorization` (the block whose comment reads "Store a permanent
authorization so consent is not re-prompted"). For a third-party client, `!hasValidAuthorization`
already redirected to `/consent` earlier in the method, so this branch can never execute. It
dates from the original consent commit (`4419a4af`) and survived the scope-narrowing rewrite
(`14f77af4`). It is also the only place that sets `descriptor.Principal`.

### 2.4 What is missing: `SetAuthorizationId`

Nowhere in the controller (or `TokenController.cs`) is `identity.SetAuthorizationId(...)` called
(grep of `api/src/Modules/Identity` for `SetAuthorizationId|GetAuthorizationId` returns nothing).
OpenIddict's behaviour when the principal carries no authorization id, from
https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Server/OpenIddictServerHandlers.cs
(`AttachAuthorization` handler, active because authorization storage is enabled): it creates an
authorization with `Type = AuthorizationTypes.AdHoc`, `Status = Valid`, scopes =
`principal.GetScopes()`, and calls `SetAuthorizationId` on the principal "so that it is attached
to all the derived tokens, allowing batched revocations support". The documentation says the
same in prose — permanent authorizations are created explicitly by the app, ad-hoc ones
automatically by the server for each sign-in without one:
https://github.com/openiddict/openiddict-documentation/blob/dev/configuration/authorization-storage.md?plain=1#L1.

So on every authorize for **any** client Wallow gets one ad-hoc authorization, and the code,
access, id and refresh tokens are all linked to it (`CreateTokenEntry` persists
`AuthorizationId = principal.GetAuthorizationId()`,
https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Server/OpenIddictServerHandlers.Protection.cs).
For third-party clients that ad-hoc entry sits **beside** the permanent consent record, which
nothing references. The Velusia sample instead reuses the found permanent authorization
(`authorizations.LastOrDefault()`), creates one only if none exists, and then calls
`identity.SetAuthorizationId(await _authorizationManager.GetIdAsync(authorization))` — one
authorization per user+client, with every token chained to it.

The id survives token exchange without the controller's help: `RestoreInternalClaims` copies the
`oi_`-prefixed private claims (`oi_au_id`) from the incoming code/refresh token principal into
the new one (same file as `AttachAuthorization`). `TokenController.HandleAuthorizationCodeOrRefreshAsync`
builds a fresh `ClaimsIdentity` from `result.Principal`, which is exactly what the sample does,
so setting the id once in the authorize step is sufficient.

### 2.5 Why the link matters: revocation is enforced per request

`ValidateAuthorizationEntry` (Protection.cs, above) rejects any token whose authorization is
missing or not `Statuses.Valid` — for refresh tokens and authorization codes at the token
endpoint the error surfaces as `invalid_grant`
(https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Server/OpenIddictServerHandlers.Exchange.cs).
`TryRevokeAsync` on an authorization flips its status and does **not** cascade to tokens; the
per-request check is what blocks them. `IOpenIddictTokenManager.RevokeByAuthorizationIdAsync`
marks the whole chain `Revoked`
(https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Core/Managers/OpenIddictTokenManager.cs),
and because Wallow enables `EnableTokenEntryValidation()` on the validation side
(`IdentityInfrastructureExtensions.cs`), revoked **access** tokens stop working on the next API
call rather than at expiry.

Net: once tokens are chained to the permanent authorization, "withdraw consent" =
`TryRevokeAsync(authorization)` + `RevokeByAuthorizationIdAsync(id)` and every refresh and
access token for that client dies immediately. Today neither call exists in product code
(grep for `TryRevoke|RevokeBy` under `api/src` hits only API keys, service accounts and
membership access).

### 2.6 Pruning

`api/src/Modules/Identity/Wallow.Identity.Infrastructure/Jobs/OpenIddictTokenPruningJob.cs`
(Hangfire, every 4 h, `api/src/Wallow.Api/Program.cs`) calls `tokenManager.PruneAsync(now)` then
`authorizationManager.PruneAsync(now)` — the order OpenIddict's manager doc comment requires.
The EF store's predicate is `CreationDate < threshold && (Status != Valid || Type == AdHoc) &&
!Tokens.Any()`, so **Valid permanent authorizations are never pruned** and consent memory is
durable; ad-hoc ones disappear once their tokens are gone. OpenIddict's own Quartz job uses a
14-day `MinimumAuthorizationLifespan`
(https://raw.githubusercontent.com/openiddict/openiddict-core/dev/src/OpenIddict.Quartz/OpenIddictQuartzOptions.cs);
Wallow's `threshold = UtcNow` is more aggressive but safe because the `!Tokens.Any()` guard
holds. Not a gap.

---

## 3. Scope-change semantics

- OpenIddict offers nothing for delta consent; the docs, sample and server source do not address
  it. The only hint is a comment in the sample's `Accept()` that the app "may want to allow the
  user to uncheck specific scopes".
- Wallow's behaviour: `{profile,email}` stored, `{profile,email,storage.read}` requested →
  superset check fails → redirect to `/consent` carrying all three scopes
  (`ConsentScreen.tsx` renders exactly the `scope` query it is given, via
  `GET v1/identity/apps/consent-info/{clientId}?scopes=...` in `AppsController.cs`) → on
  approval a **second** permanent authorization with three scopes is created; the two-scope one
  stays `Valid`.
- The ticket's target ("re-prompt for the delta only") is app-level work: compute
  `granted − ∪(scopes of the user's Valid permanent authorizations for this client)`, pass only
  the delta to `/consent` (the screen already takes an explicit scope list), and on approval
  either `UpdateAsync` the existing authorization's scopes to the union or create one for the
  union and revoke the old. The sample's "reuse `LastOrDefault()`" pattern favours one record per
  user+client, which is also what a connected-apps page wants to display.
- `prompt=consent` / `prompt=none` are not handled (no `prompt` or `PromptValues` reference in the
  module). The sample forces the consent view on `prompt=consent` and returns
  `consent_required` on `prompt=none` when interaction would be needed. Cheap to add; relevant for
  a bcordes.dev client that wants to re-verify consent or do silent renewal.

---

## 4. User-facing revocation — what exists and what does not

Exists:
- RFC 7009 `/connect/revoke` (client-authenticated, per token) — `IdentityInfrastructureExtensions.cs`.
- Per-user **Wallow session** list/revoke, `GET|DELETE v1/identity/sessions` (`SessionController.cs`,
  `SessionService.cs` — Redis-marked cookie sessions, not OpenIddict authorizations). No app
  page consumes it either (only the generated SDK references the route).

Does not exist:
- Any list of "applications you have authorized" or a way to withdraw one. Withdrawing would
  today have to be an admin deleting rows, and even that leaves refresh tokens valid (§ 2.4).
- A doc for the behaviour. `docs/integrations/bff-pattern.md` § Troubleshooting says "Consent
  screen appears on every login | Application not granted `offline_access`" — `offline_access`
  plays no part; the real causes are a scope outside the stored set or a `consent_denied` on the
  previous attempt.

Seam check (`.claude/rules/IDENTITY.md`): a self-service authorizations endpoint is Identity's
own user-facing API on its own persistence, comparable to `SessionController`; it is not a new
cross-module surface. A fork on an external IdP would get the equivalent from that IdP's
account console, so the page should be built as "Identity's account console", not something
other modules depend on.

---

## 5. Recommendation

"Consent once" already holds. To make it *correct* and *revocable* for external clients like
bcordes.dev, in priority order:

1. **Chain tokens to the permanent authorization** (blocking for any revocation story). In the
   authorize step: find via `FindAsync(subject, client, Statuses.Valid, AuthorizationTypes.Permanent,
   grantedScopes)`, reuse `LastOrDefault()` or create, then `identity.SetAuthorizationId(id)`.
   Delete the unreachable second create block. Side effects: no more ad-hoc entries for
   third-party clients, one record per user+client, and the § 2.2 ad-hoc-satisfies-lookup hole
   closes because the lookup now filters on `Permanent`.
2. **Connected-applications surface**: `GET v1/identity/me/authorizations` (client display name,
   scopes, created date, from Valid permanent authorizations) and `DELETE .../{id}` doing
   `TryRevokeAsync` + `tokenManager.RevokeByAuthorizationIdAsync`. Immediate effect is guaranteed
   by § 2.5. Put the page next to sessions in wallow-web settings; wire the existing sessions
   endpoint into the same page while there.
3. **Delta consent + `prompt` handling** as in § 3 — show only new scopes, store the union.
4. **Decide whether to adopt OpenIddict `ConsentType`** instead of the `wallow-` prefix +
   `Identity:FirstPartyClients` list. `Implicit` on first-party registrations and `Explicit` on
   developer-registered apps would make the rule a per-client attribute visible in the admin UI
   rather than a naming convention — worth a decision ticket, not required for 1–3.
5. Fix the `bff-pattern.md` troubleshooting row.

First-party clients: leaving them on ad-hoc authorizations is fine (that is OpenIddict's default
model), but if a connected-apps page should also let a user sign a first-party BFF out of the
token chain, give them the same permanent-authorization treatment with the consent screen
skipped.
