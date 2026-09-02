**status: active**

# One API failure model end to end

Spec issue: [#176](https://github.com/bc-solutions-coder/wallow/issues/176); the eleven implementation issues are its sub-issues.

Spec produced from the wayfinder map [Wayfinder: one API failure model end to end (api-errors, failure messages, toasts)](https://github.com/bc-solutions-coder/wallow/issues/163). Every decision below lives on a closed ticket of that map; this document is the synthesis the implementation issues cite. Vocabulary is `CONTEXT.md` § Errors: **problem**, **API failure**, **transport failure**, **failure message**, **relayed problem**, **originated problem**, **failure surface**, **handled failure**, **error code**, **field error**.

## Problem Statement

When a request fails, a Wallow user may see raw transport text ("fetch failed", an undici socket message), an empty response, a `;`-joined list of validation messages, or nothing at all. Which of these appears depends on which of three wire shapes the API happened to use, which of two parsers the SDK happened to run, and which of ~25 copy-pasted banner sites the screen happened to be. Field errors from one validation path are dropped. A fork author who wants to change one sentence has to find it in five per-feature code tables, an MFA map, and a pile of per-site fallbacks. A developer building on the published SDK has no way to name a failure except by reading the SDK's error class.

## Solution

One failure model from the API to the screen:

- The API answers every non-OAuth error with one problem+json contract: `type`, `title`, `status`, `code`, `traceId`, a user-safe `detail` on 4xx, and a camelCase `errors` dictionary on validation. Codes come from per-module catalogs and are exported to TypeScript through the OpenAPI document.
- A small published, dependency-free package, `@bc-solutions-coder/api-errors`, parses any failure (problem, OAuth body, transport error, unrecognised response) into one **API failure** value and resolves it to a **failure message** through a code-first registry with shipped defaults. Raw transport text lives only on the failure's `cause`; nothing user-facing reads it.
- The browser-facing server hop (BFF proxy and passthrough) relays API problems untouched and writes a problem of its own for every fault it originates, with the same envelope.
- The shared React packages provide the **failure surfaces**: an inline banner for failed reads, the existing form banner plus field errors for submissions, a sonner toast for unhandled mutation failures, and the route boundary for reads that failed while loading. A mutation is toasted unless the screen marks it handled; a read is shown in place unless it opts into the toast.
- Both apps migrate onto those surfaces and one registry each; minimal-app shows the pattern an external SDK consumer follows.

## User Stories

### End user

1. As a user whose connection dropped, I want to see "Unable to reach the server. Check your connection and try again." instead of "fetch failed", so that I know what happened and what to do.
2. As a user whose request timed out, I want a sentence saying the server took too long, so that I know retrying is reasonable.
3. As a user submitting a form with invalid input, I want each invalid field to show its own message and the form to show one banner, so that I can fix exactly what is wrong.
4. As a user submitting a form that failed for a non-field reason, I want the banner to show a sentence about that reason, never a joined list of field messages, so that the banner reads as one thing.
5. As a user whose session expired mid-session, I want the failing surface to say my session expired and offer a "Sign in" action that brings me back to where I was, so that I am not silently thrown to the login page and I do not lose my place.
6. As a user without permission for an action, I want to see "You don't have permission to do that.", so that I stop trying.
7. As a user who opens a link to something that no longer exists, I want the page to show the not-found screen, so that a missing record is not presented as an outage.
8. As a user who deleted something that is already gone, I want a "That could not be found." message on the action, so that I understand the state.
9. As a user who is rate limited, I want to be told how many seconds to wait, so that I do not hammer the button.
10. As a user hit by a server fault, I want a generic "Something went wrong on our side" sentence with a reference I can copy, so that I can report it without reading stack text.
11. As a user whose background refetch failed, I want the data I already see to stay on screen rather than be replaced by an error, so that a flaky network does not blank my page.
12. As a user whose action failed on a screen that does not show its own errors, I want a toast at the bottom right that I can dismiss, so that failures never go silent.
13. As a user whose action failed on a screen that does show its own errors (a destructive dialog, an editor, an MFA step), I want the error inline where I am looking and no duplicate toast, so that I see one notice, not two.
14. As a user who cancelled a request myself, I want no error shown at all, so that my own navigation is not reported as a failure.
15. As a user of the dark theme, I want toasts to follow the theme, so that notices match the rest of the app.
16. As a user who lost the network while a page was loading, I want the page-level notice to offer "Try again", so that I can recover without a full reload.
17. As a user of the auth app who typed a wrong password, I want "Invalid email or password." regardless of whether the account exists, so that the app never reveals which addresses are registered.
18. As a user of the auth app who is locked out of MFA, I want the lockout sentence and the same rate-limit wording the rest of the product uses, so that the auth app does not feel like a different product.
19. As a user of the auth app whose passwordless request was throttled, I want a real "wait N seconds" message rather than a sentence that reads like a code, so that the throttle is understandable.
20. As a user of the first-run install, I want the setup-required condition to read as a sentence, so that the API's readiness state is not shown as a crash.

### Application developer (wallow-web, wallow-auth, a fork)

21. As an app developer, I want one `createQueryClient` option that receives every unhandled failure, so that I wire the toast in one line at the root.
22. As an app developer, I want mutations to toast by default and to opt out with one `meta` helper, so that a new screen is safe without extra work and a screen that renders its own error is not duplicated.
23. As an app developer, I want reads to stay inline by default and to opt into the toast with the mirror helper, so that a failed list does not toast over its own banner.
24. As an app developer, I want a `FailureBanner` that takes the raw error and resolves its own message, so that I never write a per-site fallback string again.
25. As an app developer, I want the root route boundary to render a failure banner when the thrown value is an API failure and fixed copy otherwise, so that loaders and render bugs are handled by one component.
26. As an app developer, I want one registry per app, built with `defineFailureMessages`, mounted with one provider, so that all app copy is in one reviewable place.
27. As an app developer, I want to pass call-site `messages` when a screen's wording must differ from the app's, so that "invalid code" can read differently on the challenge, backup, and OTP screens without a second registry.
28. As an app developer, I want the resolver to work with no provider mounted, so that tests, Storybook stories, and minimal-app need no setup.
29. As an app developer, I want field errors split from banner errors by one function that already knows the form's field names, so that a dotted or unknown key never produces a phantom field.
30. As an app developer, I want a 401 to be a surface with a "Sign in" action rather than an automatic redirect, so that in-flight edits are not discarded by a navigation I did not trigger.
31. As an app developer, I want one warning logged per unhandled failure with its code, status, and reference, so that diagnostics exist without console noise on handled ones.
32. As an app developer, I want reads that are silent by design to be marked with a comment and nothing else, so that intentional silence is distinguishable from an oversight.
33. As a fork author on another toast library, I want to replace one callback, so that the query layer does not force sonner on me.
34. As a fork author whose backend adds a code before the snapshot is regenerated, I want the registry key type to still accept it, so that the build does not fail on a string.
35. As a fork author, I want editor autocomplete on every known code when writing registry entries, so that typos are caught at write time.

### SDK consumer (external relying party, minimal-app)

36. As an SDK consumer, I want the SDK to throw one branded `ApiFailure` from every request, so that one check names every failure.
37. As an SDK consumer, I want to install `api-errors` next to the SDK and import the brand check, the codes, and the resolver from it, so that there is exactly one import path for the failure type.
38. As an SDK consumer, I want a `failureFromResponse` helper for a plain `fetch`, so that I can use the model without the SDK at all.
39. As an SDK consumer, I want the resolver to run on the server too, so that my own route handlers can answer with a sentence.
40. As an SDK consumer, I want the integration guide's error section and minimal-app to show the same two call sites, so that I copy a pattern that is known to work.
41. As an SDK consumer, I want `RefreshFailedError` to still be catchable and to carry the same failure shape, so that my session teardown logic is unchanged.

### API developer

42. As an API developer, I want to construct an `Error` from a catalog entry that carries the code, kind, and default sentence, so that I never invent a code string or a status inline.
43. As an API developer, I want every writer to go through the one problem-details path, so that `traceId` and `code` are present without me remembering them.
44. As an API developer, I want a source guard and an integration sweep that fail when a new body bypasses the writer, so that the contract cannot regress silently.
45. As an API developer, I want the OpenAPI document to declare 400, 401, 403, 404, 429, and 500 on every operation and an `ErrorCode` enum, so that consumers see the full contract.
46. As an API developer, I want a test that fails when a code exists in the catalog but not in the document, or vice versa, so that drift is caught before publish.
47. As an API developer adding a module, I want to register the module's catalog with the module, so that no shared file is edited to add a reason.

### Operator

48. As an operator reading logs, I want the BFF to log the real transport message while sending the user a fixed sentence, so that diagnostics are complete and users see nothing internal.
49. As an operator, I want every problem written by the API to carry the trace id and every problem written by the BFF to carry the request id, so that a copied reference finds the log line.

## Implementation Decisions

### API contract

- Every non-OAuth error is a problem with `type` `about:blank`, `title`, `status`, `code`, `traceId`, `detail`, and on validation problems only `errors` as `{ field: [messages] }` with camelCase, dot-preserved keys. The `api`, `version`, and `instance` extensions are dropped; the Development-only `exception` extension on 5xx stays.
- `detail` is a user-safe sentence on every 4xx and one fixed generic sentence on every 5xx. The validation problem's `detail` is one generic sentence, never the joined field messages.
- Codes are dotted PascalCase `Area.Reason`. Status-generic codes fill in when a writer sets none: `Validation.Failed` 400, `Auth.Unauthenticated` 401, `Auth.Forbidden` 403, `Http.NotFound` 404, `Http.MethodNotAllowed` 405, `RateLimit.Exceeded` 429, `Setup.Required` 503, `Server.Error` 5xx.
- `Error` in the shared kernel gains an explicit kind (Validation 400, NotFound 404, Conflict 409, Unauthenticated 401, Forbidden 403, BusinessRule 422, Failure 500); the result-to-response mapping derives status from kind, and prefix parsing plus the placeholder default codes go.
- The Identity auth and MFA endpoints replace their bare `{ succeeded, error }` bodies with problems through the same result path; snake_case tokens become `Auth.*`/`Mfa.*` codes; `user_not_found` folds into `Auth.InvalidCredentials`; the passwordless throttle becomes a 429 with `Retry-After`. Success bodies are untouched.
- Every error body is written by the problem-details service or MVC's factory. `CustomizeProblemDetails` is the single place that adds `traceId` and fills a missing `code`, so status-code-pages 404/405 hold the invariant too. The exception handler, the rate limiter, the result mapping, the API-key middleware, and every inline problem in controllers move onto that path.
- OpenAPI: a schema transformer adds `code` and `traceId` as required members, emits an `ErrorCode` string enum from the aggregated catalogs with each entry's default sentence as its description, and points `code` at it; a global filter declares the standard error statuses on every operation. The committed snapshot and the SDK client are regenerated.

### Error-code catalog

- Each module owns one static catalog and registers it when the module is added; the shared kernel holds only the status-generic entries. The API aggregates registered catalogs for the document and the drift test. Codes stay strings; there is no C# enum.
- An entry is code + kind + default user-safe sentence. `Error` and domain exceptions are constructed from an entry with an optional message override; bare-string constructors go.

### `api-errors` package

- A new published package, zero runtime dependencies, no React. Owns `ApiFailure` (`status`, `code`, `title`, `detail?`, `traceId?`, `requestId?`, `fieldErrors?`, `retryAfter?`, native `cause`), `isApiFailure` (brand check on a global-registry symbol), `toApiFailure(input, context?)`, `failureFromResponse(response, bodyText)`, `resolveFailureMessage`, `defineFailureMessages`, `splitFieldErrors(failure, knownFields)`, `isSilentFailure`, the `ClientErrorCode` const, the generated `ErrorCode` const, and `FailureCode` as their union. `WallowError`/`isWallowError` are deleted, not aliased.
- `message` is `[<status> <code>] <title>` for logs only. Nothing user-facing reads `message` or `cause`.
- Parser: passes an existing failure through; classifies a thrown error without a status as a transport failure (`Transport.NetworkError` 503, `Transport.Timeout` 504, `Transport.Aborted` 499); parses problem+json; normalises OAuth bodies to `OAuth.<PascalCase(error)>` with `detail` from the description; anything else with a response becomes `Client.UnrecognizedResponse` at the response's status with the body text on `cause`. `retryAfter` is parsed from the `Retry-After` header.
- `ClientErrorCode`: `Transport.NetworkError`, `Transport.Timeout`, `Transport.Aborted`, `Client.UnrecognizedResponse`, `Bff.CsrfInvalid`, `Bff.SessionRefreshFailed`, `Bff.SessionMissing`.
- Registry: `defineFailureMessages(entries)` holds app overrides only; keys are `FailureCode | (string & {})`; every entry is `(failure) => string`.
- Resolver precedence, first hit wins: call-site `messages` by code; app registry by code; shipped defaults by code; the problem's `detail` only when the status is 4xx and the code is not a client code; shipped defaults by status (401, 403, 404, 409, 429, 5xx); call-site `fallback`; the generic sentence. Transport failures, unrecognised responses, and 5xx never reach the `detail` step. A non-failure input resolves to the generic sentence, never to `Error.message`.
- Shipped copy: network unreachable, timeout, unrecognised/5xx generic, session expired (401 and refresh failure), 403, 404, 409, 429 with and without a known wait. Silence is a separate predicate (`isSilentFailure`, true for aborts), not a return value.
- Generation: the package runs openapi-ts against the SDK's committed snapshot at dev time, typescript plugin only, JavaScript enums, filtered to `ErrorCode`, output committed. A root `check:generated` script regenerates both packages and fails on a diff as part of `pnpm check`.
- Publishing: release-please node component with tag prefix `api-errors-v`; the SDK publish workflow becomes one workflow triggered by either tag, deriving the package directory from the tag; the SDK depends on the package via a workspace caret range; `api-errors` publishes first.

### SDK

- The SDK keeps `RefreshFailedError` (now extending `ApiFailure` with code `Bff.SessionRefreshFailed`), `redact`, the error-interceptor wiring, and the proxy re-emit. Both of its RFC 7807 parsers are replaced by the package's parser. The SDK re-exports nothing from `api-errors`.
- Every BFF-originated response is a problem: unknown or escaping path 404 `Http.NotFound`; no or unreadable session 401 `Bff.SessionMissing`; refresh failure 401 `Bff.SessionRefreshFailed` (teardown behaviour unchanged); CSRF mismatch 403 `Bff.CsrfInvalid`; a login redirect surviving replay 401 `Auth.Unauthenticated`; upstream transport failure 503 `Transport.NetworkError`; forward timeout 504 `Transport.Timeout`. The passthrough wraps its upstream fetch (503 problem) and answers a 404 problem for paths outside its allowlist; no timeout is added to it.
- Originated problems match the API envelope member for member with `requestId` and no `traceId`; `detail` is fixed wording per case; the transport message goes only to the redacted log record. One shared server-side problem writer serves both presets, and its per-code title/detail table is the server counterpart of the shipped failure-message defaults.
- Upstream bodies are relayed byte for byte. The single 429 replay after `Retry-After`, bounded at five seconds, stays.

### Shared React packages

- `query` stays UI-free. `createQueryClient(options)` gains `onUnhandledFailure({ kind: "mutation" | "query", error })`, invoked by the mutation cache for every mutation not marked handled and by the query cache for queries that opted in. The `meta` helpers `handledFailure(meta)` and `toastedFailure(meta)` set the two flags and compose with TanStack's own `meta`. This shape came out of the prototype (branch `prototype/failure-toast`), which showed no new React package is needed.
- `ui` owns the React binding of the registry: `FailureMessagesProvider({ registry })` mounted once at the root with an empty-registry default, and `useFailureMessage(error, options?)` returning `null` for a nullish error. It also owns `FailureToaster` (sonner, themed from `useTheme`, bottom right, close button, Tailwind tokens forced over sonner's injected CSS), `toastFailure(message, reference?)` with a "Copy reference" action, and `FailureBanner` (`error`, `messages?`, `fallback?`, `onRetry?`, `children?`) rendering the existing `ErrorBanner` primitive with "Try again", a 401 "Sign in" link carrying the current path, and the copy-reference affordance. The Base UI toast wrapper is deleted.
- `forms` keeps only the TanStack Form adapter: it consumes the resolver and `splitFieldErrors`, marks every form mutation handled, drops `errorText` and its SDK dependency. `FormError` and field errors are unchanged as surfaces.
- Layering: `api-errors` (pure) → `query` and `ui` → `forms` → apps. `forms`, `ui`, `query`, and wallow-auth depend on `api-errors`, never on the SDK.

### Surface catalog

| Failure | Surface | Message | Actions |
|---|---|---|---|
| Failed read in a component | Inline `FailureBanner` replacing the region; guard `isError && data === undefined` so a failed refetch never replaces cached data | hook, no per-site fallback | "Try again"; reference + copy on transport/5xx |
| Failed read in a loader | Root boundary rendering `FailureBanner` for API failures, fixed copy otherwise | registry | "Try again" invalidates the router; 404 renders the not-found component |
| Form submission | Form banner + field errors (existing path) | `splitFieldErrors` + resolver | none |
| Non-form mutation | Toast unless marked handled | resolved in the app's callback | close; reference + copy on transport/5xx |
| Non-form mutation the screen shows itself | Inline banner, mutation marked handled | hook | as for reads |
| Render error | Root boundary, fixed copy | none | none |

Status rules: 401 is a surface with a "Sign in" action, never an automatic redirect; 403 message only; 404 from a mutation message only, from a loader the not-found component; 429 static copy from `retryAfter`, no countdown; `Setup.Required` shipped copy only; transport and 5xx show shipped copy and the reference line, never `detail`. The app's callback logs one warning through the logger with code, status, and reference. Silent-by-design reads get a comment and nothing else.

### App migrations

- wallow-web: the client callback resolves with the app registry and toasts; the toaster mounts in the root route inside the theme provider; the root boundary handles API failures; the MFA map becomes registry entries; every read site moves to `FailureBanner`, every non-form mutation to toast or handled, and every per-site fallback goes. The bff-demo page and the null-body not-found checks on detail screens are replaced by the model.
- wallow-auth: the five per-feature code tables and the invitation/register mappers collapse into one registry; per-screen wording moves to call-site `messages`; the feature modules keep only their success-body navigation decisions. The bypass screens keep their submit escape hatch and `serverError` prop, fed from the hook. Rate limit reads from the 429; the setup gate is untouched.
- minimal-app: takes `api-errors` as a direct dependency next to the SDK; the demo page shows the resolved message plus the code with a one-entry registry; the contact route answers its own `{ error }` JSON filled from the resolver at the failure's status; no problem writer is added to the package for it.

### Docs

Each implementation issue rewrites the docs its code invalidates: the API development guide (codes, problem details), the SDK integration guide's error section (with a no-SDK snippet), the SDK, `ui`, `query`, and `forms` package guides, the forms development guide, and the frontend setup line about minimal-app.

## Testing Decisions

A good test exercises a seam a caller also crosses and asserts what the caller sees: the body of an HTTP response, the value a parser returns, the text a component renders, the sentence a user reads. No test reads source off disk, asserts on a private helper, or checks that a specific internal function was called.

Seams, highest first, all but one already existing:

1. **API error routes over HTTP** (integration category): one sweep requests each known failing route (404, 405, 401, 403, 400 validation, 422 business rule, 429, 503 setup, 500) and asserts `application/problem+json`, the always-present members, and the code. A companion test asserts the aggregated catalog equals the document's `ErrorCode` enum and covers every code the sweep observed. Prior art: the existing module integration suites and the OpenAPI snapshot comparison in CI. A source-level guard fails on hand-built problems or direct JSON writes outside the writer path.
2. **`api-errors` public interface** (the one new seam, unavoidable for a new package): fixture bodies and thrown errors in, `ApiFailure` out; resolver precedence per step; `splitFieldErrors` against known fields; `isSilentFailure`. Prior art: the SDK's existing parser tests, which these replace.
3. **BFF proxy and passthrough over HTTP with a fake upstream**: the existing proxy and passthrough suites, with the test deltas listed on the transport ticket (code values, status 504 for timeouts, body assertions on the formerly bodiless 401/404s, one new unreachable-upstream passthrough test). The 429 replay tests are untouched.
4. **Components in a real browser** (Vitest browser mode, stories): `FailureBanner` per status rule, `FailureToaster` content and reference action, a form submit against a failing mutation showing field errors plus banner and no toast, a query client whose callback fires for an unhandled mutation and not for a handled one or an un-opted query. Prior art: the existing `ui` stories and `forms` browser specs; the prototype's two stories.
5. **Playwright E2E** in wallow-web: network down (toast and banner), 429 copy, validation field errors, 401 mid-session with the "Sign in" action, loader 404 → not-found. wallow-auth: rate-limit and lockout copy. minimal-app's existing cross-app assertion is unchanged.

Backend tests run only through the repo's test script (integration category for the writer sweep); TypeScript through `pnpm check`.

## Out of Scope

- Internationalisation: the registry holds plain strings; an i18n effort would wrap it later.
- Changing the OAuth wire shape on `/connect/*`; the parser normalises it.
- Content-negotiated redirects from API URLs (ruled out by the first-run setup map).
- Rollout or compatibility shims: pre-release, nothing to protect.
- `createServiceClient` transport and token-grant failures: server-side consumer, raw error belongs in a log.
- A problem+json writer in `api-errors`; a countdown on 429; automatic redirect on 401; a dedicated 403 screen; any setup page.

## Further Notes

Implementation order, from [Handoff shape: implementation issues and their order](https://github.com/bc-solutions-coder/wallow/issues/175). Eleven issues as sub-issues of the spec issue with native blocked-by edges; each cites its decision tickets by name and this plan by path.

1. API: error-code catalog and its OpenAPI export.
2. API: single problem writer and the unified contract (blocked by 1). The Identity auth and MFA endpoints' body change is **not** here: it moves to issue 10 so that issue is a vertical slice and the auth app's E2E stays green in between.
3. `api-errors` package (blocked by 1).
4. SDK cut-over and the minimal-app reference, the expand step: every `isWallowError` import retargeted mechanically so main stays green (blocked by 3).
5. BFF proxy and passthrough failures as problems (blocked by 3).
6. `ui` and `query` surfaces, folding the prototype branch (blocked by 3).
7. `forms` onto `api-errors` (blocked by 4, 6).
8. wallow-web wiring (blocked by 7).
9. wallow-web site sweep with the five E2E scenarios (blocked by 8).
10. wallow-auth migration, including the auth and MFA endpoints answering problems (blocked by 2, 6).
11. Contract: delete the old helpers and retire the map's diagnosis (blocked by 9, 10).

The blast radius that forced expand → migrate → contract: `WallowError` in 50 files, `ErrorBanner` at 55 sites, `errorText` at 30.
