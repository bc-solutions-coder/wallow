# packages/api-errors — @bc-solutions-coder/api-errors Agent Guide

The **API failure model** every Wallow consumer shares: `ApiFailure`, the parsers that build
one, the message resolver, and the field-error split. **Zero runtime dependencies, no React,
one entry (`.`).** Published to GitHub Packages on `api-errors-v*` tags via
`package-publish.yml`; the SDK will depend on it (`workspace:^`), never the reverse.

## Charter (compiler-enforced)

- `tsconfig.json` drops DOM and every ambient `@types` package: nothing here may touch
  `window`, `process`, or `node:*`. The parser takes a **structural** `FailureResponse`
  (`status` + `headers.get`) so a real `Response` satisfies it on any runtime without the
  package picking one. Specs and configs typecheck through `tsconfig.node.json`.
- Root `.oxlintrc.json` bans `react`, `zustand` and every `@bc-solutions-coder/*` import
  from `src/**` (the bottom-of-graph override shared with env/logger/utils). The
  `x-request-id` header name is declared locally for that reason — do not import it from
  the SDK.

## The model (root `CONTEXT.md` § Errors is the authority)

- `ApiFailure extends Error`, branded on `Symbol.for("wallow.api-failure")` so
  `isApiFailure` recognises a failure from **another bundled copy** of this module — never
  use `instanceof`. `message` is `[<status> <code>] <title>`, for logs only. Raw transport
  text and an unrecognised body go on native `cause`, **never** on `detail`.
- `toApiFailure(input, context?)`: pass-through → thrown `Error` without a status is a
  transport fault (`Transport.Timeout` 504 for a `TimeoutError` or an undici timeout code
  on the error or its cause; `Transport.Aborted` 499 for an `AbortError`; else
  `Transport.NetworkError` 503) → plain object read as a body → anything else is
  `Client.UnrecognizedResponse` at `context.status` (500 when unknown).
- Bodies: problem+json is recognised by a **top-level string `code`** (the unified contract;
  no `extensions.code` probing). The **response status is authoritative**; the body's
  `status` stands in only when a bare object reaches `toApiFailure` with no status.
  `x-request-id` is the BFF tunnel's header, so a direct-to-API caller gets no `requestId`. An OAuth body (`{ error, error_description }`) becomes
  `OAuth.<PascalCase(error)>` with the token as `title` — a documented grammar, not an
  enumerated list. `Retry-After` (delta-seconds or IMF-fixdate) lands on `retryAfter`.
- `resolveFailureMessage` precedence is fixed and spec-pinned: call-site `messages` → app
  `registry` → shipped per-code copy → `detail` (4xx with a **non-client** code only) →
  shipped per-status copy (401/403/404/409/429/5xx) → `fallback` → one generic sentence.
  Transport, unrecognised and 5xx failures never surface `detail`; a non-failure input is
  classified first, so an `Error.message` is never echoed.
- `isSilentFailure` is true for `Transport.Aborted` only. The React binding
  (`FailureMessagesProvider`, `useFailureMessage`) lives in `ui`, not here.

## Generated code

`src/generated/` is emitted by `pnpm generate` (`openapi-ts.config.ts`) from the **SDK's**
committed snapshot `packages/sdk/openapi/v1.json`: typescript plugin only, `enums:
"javascript"`, every operation excluded and `schemas.include: ["ErrorCode"]`. The plugin also
emits `ClientOptions` from the document's `servers` entry unconditionally; it is not exported
from `src/index.ts`, but a server-URL change still regenerates this directory. Never hand-edit
it. `pnpm check:generated` (inside `pnpm check`) regenerates both packages and fails on a
diff; `openapi-autoregen.yml` regenerates this directory alongside the SDK's. Like the SDK
this package pins `typescript: catalog:tooling-tsc6` because the generator needs the JS
compiler API.

## Tests (vitest, node environment)

One spec per module; every parser branch, every resolver step, the dotted-key fold and the
silent predicate are covered, and `index.test.ts` pins the runtime export list. No spec reads
source or the manifest off disk.
