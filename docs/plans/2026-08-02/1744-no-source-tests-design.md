# No source tests — lint owns structure, tests own behaviour

**status: active**

Epic: `Wallow-xg9t` (fix the guards that do not guard). Supersedes the approach recorded in
`Wallow-e6gz`, whose acceptance criteria call for recovering and hoisting a comment scanner.
We are deleting the scanner's reason to exist instead.

## The problem

> **Scope note, recorded after the fact.** This document was written against an inventory of
> **sixteen** files. On review the user extended the decision to **every** source-reading spec in
> the repo — "we don't need any source tests at the moment so remove them all" — which made the
> real count **77**, explicitly overriding the `CLAUDE.md` guidance that blessed some of them.
> The analysis below is unchanged and still correct; the tables have been rewritten to the
> delivered scope. What survived the sweep is stated in *What was kept*.

Seventy-seven spec files read application source off disk as text and assert against the string.
Every one of them strips comments first, and every one hand-rolls the same two-pass stripper:

```ts
source.replaceAll(/\/\*[\s\S]*?\*\//gu, "").replaceAll(/\/\/[^\n]*/gu, "")
```

Block comments are stripped **before** line comments, so a `/*` inside a `//` comment — a route
glob written as `// /v1/**` — opens a block comment that runs to the file's next block-comment
close and deletes everything between. Measured in `wallow-auth`: spans of 2871, 2884 and 4041
characters of real source silently removed from what the guards read. The assertions checked
still passed, because the swallowed spans sit after the imports. That is line-position luck, not
soundness.

On a negative scan — `packages/env`'s charter asserts `process.env` does **not** appear in the
shipped modules — over-deletion is indistinguishable from a clean module. The spec goes green
over code it never saw.

The obvious fix is a real left-to-right scanner shared from one place. That fix was written once
(185 lines, 11 adversarial fixtures), lived at `apps/wallow-web/src/shared/testing/strip-comments.ts`,
and was deleted in `5641bcf2` when two sweeps became lint rules. It is recoverable. We are not
recovering it.

### Why the count keeps moving

The inventory has been re-derived three times and landed on 23, then 18, then 16 — each of those
counting only the sweeps a bead had already flagged, which is why the true figure turned out to be
77. Files leave when a sweep is deleted; files arrive because **nothing forbids writing a new
one**. Two of the flagged sixteen — `packages/navigation/src/shell-source.test.ts` and
`packages/logger/src/charter.test.ts` — landed *hours after* `Wallow-e6gz` was filed to fix them.

There is no rule against source tests. `doctrine` and `shape pin` appear in zero markdown files
in this repo; the phrase exists only in bead prose. The nearest written guidance is one bullet at
`packages/testing/CLAUDE.md:186` ("assert a feature seam by identity, not presence"), and it
points the right way but constrains nothing.

So the sweeps were never violating anything. That is the actual defect. Hoisting a better scanner
would make every one of them sound and leave the next one free to be written next week.

### Precedent

This conversion has already been done here once, and it worked. From
`packages/lint/src/rules/text-heading-variant.ts`:

> Why the sweep is not simply kept: it read files off disk with a regex and a hand-rolled comment
> stripper, so it judged the app once per `pnpm test` and could be defeated by any spelling its
> regex did not model. A rule reads the parsed JSX, fires in the editor, and cannot be out-run by
> formatting.

That replaced a 523-line disk sweep with one AST rule. Five `wallow/*` rules ship today. The
machinery, the fixture-based rule-testing pattern (`packages/lint/src/fixtures.test.ts` shells out
to the real oxlint binary), and the registration constraints are all in place.

## The rule

Added to `.claude/rules/TESTING.md` (shipped text; read the file for the authority):

> - **No source tests.** A test never reads application source, README prose, or directory layout
>   off disk — no `readFileSync` over `src/`, no comment stripping, no assertions about file
>   anatomy, imports, or class strings. Constraining how code is *written* is a **linter's** job: a
>   `wallow/*` AST rule fires in the editor, names the offending line, and cannot be defeated by
>   formatting (`packages/lint/CLAUDE.md`). A test calls a function or renders a component and
>   asserts what happens. Most structural sweeps should not become rules either — a spec pinning
>   file counts or README wording makes the codebase rigid without making it correct; **prefer
>   deleting the constraint to relocating it.** `wallow/no-source-tests` enforces this by banning
>   `node:fs` in a spec, and reaches every spec under the five trees that register the plugin.
>   […] Parsing a committed data contract (`packages/sdk/openapi/v1.json`,
>   `packages/styles/styles.css`) and checking it against the runtime modules generated from it is
>   also fine: an artifact is not source.

Two things follow that are worth stating outright, because they are the point rather than a side
effect:

1. **Deleting a constraint is the preferred outcome, not the fallback.** A sweep that mandates a
   five-file folder anatomy or forbids an example app from growing a query is rigidity without
   correctness. Most of the 77 died with nothing replacing them.
2. **Coverage genuinely lost is recorded here** rather than discovered later. `Wallow-succ` exists
   because a correct deletion had no successor and nobody wrote that down. See
   *Deliberate holes*.
3. **A successor is a behaviour spec by default, and a computed-style read only when the property
   is one a user can be harmed by.** The line is WCAG contrast, not appearance —
   `packages/testing/src/contrast.ts` exists because a spec pinned a recipe, stayed green, and
   shipped a 1.27:1 hover contrast defect. "Is this the right colour" is a screenshot, and this
   repo already runs visual regression from `__screenshots__` directories in eleven places.

## The boundary applied

Every assertion in the 77 files was judged by one line.

**Deleted** — assertions about file existence, folder layout, manifest or config *text*, doc prose,
or the *text* of a source file. This is the overwhelming majority, and most died with no successor
at all. Representative losses:

| Class | Examples |
| --- | --- |
| Folder anatomy | the ui catalog's five-file component shape (`existsSync` × 5 per component); `COMPONENT_FOLDERS`; `dist-structure.test.ts`'s built-tree walk |
| Manifest sweeps | the `env`/`logger`/`utils` charters' dependency and `exports`-map diffs — pnpm's strict `node_modules` fails an undeclared import at build, and `pnpm check:exports` checks the packed tarball harder than a config diff did |
| Doc prose | README link integrity, `docs-toc.test.ts`, `bff-pattern-docs.test.ts`, `query-rule-docs.test.ts`, `request-correlation-docs.test.ts` — documentation belongs to the docs build |
| Source text | `h3-free.test.ts`'s grep for the string `h3`; the seam specs' feature-directory walks; `shell-source.test.ts`; `shared-env.test.ts`'s cross-app resolver sweep; `devtools-gating.test.ts` |
| Vacuity meta-guards | "finds the features it is meant to cover", "finds seams to check at all" — they existed only to prove the sweep was not scanning an empty list, so they die with it |

**Kept** — anything that imports a module and asserts behaviour or export identity. Specifically:

- **Identity re-exports** — `expect(api.forgotPassword).toBe(sdkForgotPassword)`. The one construct
  a call site cannot observe: a hand-written look-alike carries the same name, shape and type,
  passes every behavioural spec, and reaches an undocumented endpoint.
- **Build configs read as OBJECTS.** Importing `vite.config.ts` and reading
  `environments.client.build.copyPublicDir` is not a source read. Several specs were rewritten from
  text-grep to object-read rather than deleted.
- **Committed data contracts.** `packages/sdk/openapi/v1.json` and `packages/styles/styles.css` are
  artifacts, not source; parsing one and checking it against the runtime modules generated from it
  stays.
- **The two specs that run the real oxlint binary** — `packages/lint/src/fixtures.test.ts` and
  `packages/sdk/src/oxlint-guardrails.test.ts`. They assert a *tool's* diagnostics, which is
  behaviour, and they are the only thing standing between us and a silently inert plugin.
  `Wallow-mlc3` was exactly that failure.

## The enforcement

`wallow/no-source-tests` bans **the `node:fs` import** in a `*.test.*` file — not `readFileSync` by
name. Keying on the call invites aliasing and misses `fs.readFileSync` member calls; the import is
the chokepoint. It self-gates on `context.filename`, so a config enables it once at the top level
and it stays inert over source.

**No overrides were written.** The design originally prescribed two (`packages/lint/**` and
`packages/testing/src/browser-styles-wiring.ts`); both are **unreachable** and would have been dead
config. `wallow/module-imports`, the second rule this document proposed, was **not written** — the
sweeps it was to absorb were deleted outright instead, which is the doctrine working as intended.

### Accept the holes

The plugin is registered by exactly five nested configs (both apps, `packages/ui`,
`packages/forms`, `packages/navigation`) and **cannot** move to the repo root:
`packages/sdk/src/oxlint-guardrails.test.ts` copies the root config to a temp directory, and any
`jsPlugins` entry makes that copy unloadable — 0 tests run, an unrelated suite silently down
(`packages/lint/CLAUDE.md` has the measurements). So the rule reaches specs under those five trees
and nowhere else. Seven `node:fs` specs remain outside its reach, all deliberate: the two oxlint
runners, `packages/query/src/index.test.ts`'s built-entry check, `packages/sdk`'s two generated-
surface/regen checks, and `packages/styles`' two data-contract readers. The doctrine holds
everywhere; it is enforced in five places. That was a decision, not an oversight.

> **Addendum (2026-08-03):** this section is superseded. The guardrail spec's mirror tree moved
> inside the repo (gitignored `.lint-mirror/`), which removed the temp-dir resolution failure,
> and the plugin is now ALSO registered from the repo-root `.oxlintrc.json` with
> `wallow/no-source-tests` enabled repo-wide — the seven deliberate `node:fs` specs are exempted
> by exact path in a root override block. See
> `docs/plans/2026-08-02/2252-arch-tooling-adoption.md` (Phase 3) and `packages/lint/CLAUDE.md`.

### Net effect

Seventy-seven files touched: most deleted outright, the rest thinned to what they can assert by
importing. One new lint rule. `grep -rln 'from "node:fs"' --include="*.test.ts" apps packages`
returns **zero under `apps/`** and seven under `packages/`, every one of them named above — by
deletion, not by hoisting. No 185-line scanner, no fixture tree, no thirteenth subpath export on
`packages/testing`.

## Phases

Ordering is a real dependency chain, not a preference.

| Phase | Bead | Work | Why here |
| --- | --- | --- | --- |
| **0** | `Wallow-xg9t.1` ✅ | `.claude/rules/TESTING.md` rule + `wallow/no-source-tests` + **the whole sweep** | Gate, and it absorbed Phase 4 outright. Once the scope became "remove them all" there was nothing left for a later phase to clear, so the rule shipped with zero violations and needed no grandfather overrides. |
| **1** | `Wallow-tkyq` | Fix the real navigation escaping into the vitest iframe from `app-shell.toggle.test.tsx` | `packages/navigation` browser runs must be trustworthy before Phase 2 adds a spec there. Today the leak tears down the runner and takes 4 sibling files (28 tests) with it. |
| **2** | `Wallow-succ` | Nav scrim spec in `packages/navigation` — **click the backdrop, assert the drawer closes** | Depends on Phase 1 only: it builds on `app-shell.toggle.test.tsx`'s router stub. Not a colour spec, so it does not wait on Phase 3. |
| **3** | `Wallow-h9k1` | Make the existing colour specs name their pointer state per assertion; delete the seven parks | Independent of everything else, and no longer a prerequisite. Ordered here because it is cleanup, not a gate. |
| ~~4a~~ | `Wallow-e6gz` | ~~Write and register `wallow/module-imports`~~ | **Dropped.** The sweeps it was to absorb are deleted, not converted. |
| ~~4b~~ | `Wallow-e6gz` | ~~Delete the buckets~~ | **Absorbed into Phase 0.** |
| **5** | `Wallow-uhef` | Decide the browser project's `testTimeout` on a quiet machine | **Last.** This is a measurement bead, and Phase 0 removed 44 node spec files outright while Phase 2 adds a browser spec. Measuring before the suite settles measures a suite that will not exist. |

### Phase 1 lead

`app-shell.toggle.test.tsx` already mocks `@tanstack/react-router`'s `Link` with a
`preventDefault` handler — but the handler is declared **before** `{...rest}` in the JSX, so an
`onClick` arriving through the spread wins. `AppNav` renders destinations as
`NavigationMenu.Link render={<Link … />}` (`app-nav.tsx:117`), and Base UI's `render` prop merges
its own handlers in. That plausibly clobbers the guard. Verify before fixing; the bead's prescribed
fix — observing the Navigation API `navigate` event and calling `preventDefault()` — is the more
robust guard either way, since `location` is `[Unforgeable]` in real Chromium and `vi.stubGlobal`
cannot shadow it.

## Deliberate holes

Recorded so these do not resurface as freshly-filed beads.

- **Every feature has a public barrel.** Dropped. `wallow/zone-dag` already bans deep imports into
  a feature, so a feature without a barrel is unreachable and fails the typecheck at the import
  site rather than in a sweep.
- **The catalog's five-file component anatomy.** Dropped with no successor. This is the rigidity
  the rule exists to remove.
- **minimal-app's README link integrity and `./query` mention.** Dropped. Documentation belongs to
  the docs build, not to `pnpm test`.
- **minimal-app ships no live query.** Dropped. The constraint was actively harmful — a red state
  meant someone improved the example.
- **The shell hand-rolls no button colour / no hover text colour.** Partly covered by the shipped
  `wallow/no-tinted-text`; the residue is dropped rather than converted.
- **The nav scrim's computed alpha.** Dropped. `Wallow-succ` was filed to assert it and now asserts
  the drawer-dismissal behaviour instead. The bead's own stated risk was "an opaque scrim breaks the
  drawer interaction" — an alpha read does not test that, and a click-and-assert does, at every
  alpha. If the scrim's appearance regresses that is a screenshot.
- **`PublicLayout`'s footer keeps `bg-sidebar`.** Dropped, and no longer folded into `Wallow-succ`.
  It was only there so the two colour assertions would land in one commit; with the scrim reduced to
  behaviour there is no shared commit left.

Widening the sweep from 16 files to all 77 dropped more than the design originally scoped. These
are the holes that opened in the second pass, listed so nobody re-derives them as regressions:

- **The SDK's server-handler import isolation.** `packages/sdk/src/server/passthrough.test.ts` read
  the handler sources and asserted no app-only import crossed into them. Nothing replaces the
  sweep; the sibling `web-standard-handlers.test.ts` calls each handler with a real `Request`, which
  proves the handlers run web-standard but says nothing about what they import.
- **The base-path trio.** Specs pinned the `Dockerfile`'s base-path build arg, the nitro/`vite.config`
  base wiring, and `router.basepath` against each other by reading all three files. That
  cross-file agreement is now unpinned — a base-path change in one of the three is caught at run
  time or not at all.
- **Brand assets.** The spec grepped `importProtection.include` for the asset entry and `statSync`ed
  a second copy of each asset to prove the two trees matched. Both halves are gone; a missing brand
  asset now surfaces as a 404.
- **The react-query single-copy pole, cross-package.** The root `no-restricted-imports` ban still
  holds every *import*, in both lint passes, and pnpm's strict `node_modules` still fails a build
  that imports an undeclared dependency. What is gone is the manifest sweep that read each
  `package.json` and asserted only `packages/query` names `@tanstack/react-query` — so a second
  *declaration* is no longer caught, only a second *import*.
- **wallow-auth's internal-href sweep.** It read the route sources for hard-coded absolute hrefs
  that should be router links. Not covered by a rule; review only.
- **`packages/env`'s cross-app resolver sweep.** It read both apps' sources to prove neither
  hand-built an origin the resolver already computes. Same status: review only.
- **The hand-rolled-button-utility half of `shell-source.test.ts`.** `wallow/no-tinted-text` covers
  the colour half. A control that paints itself instead of composing `buttonRecipe` is not covered.
- **The devtools-gating sweep.** It read app sources to prove devtools mounts sit behind a `DEV`
  guard. Shipping devtools into a production bundle is now a build-output question nothing asks.

### On the pointer park

Worth recording because it looked like infrastructure and was not. Playwright's pointer position
persists across spec *files* in a browser project, so a rest-state colour read can measure a hover
left by a previous file. Six specs answered this with a `beforeEach` park — in two non-equivalent
spellings, one of which hovers the component before parking — and a seventh renders a park it never
hovers. The park compensates for pointer state the spec never names, which is exactly why the
spellings drifted unnoticed.

The fix is not a shared park. It is `await userEvent.unhover(el)` before the rest half of a contrast
pair and `await userEvent.hover(el)` before the hover half: local, explicit, and loud when the
pointer is not where the spec claims. `@bc-solutions-coder/testing` gains nothing.

The scale matters to the judgement: only 6 specs call `getComputedStyle` at all. The hazard is
downstream of choosing to write rest-state colour specs, not a property of browser mode — a
behaviour spec never touches `:hover`.

## Bead impact

- **`Wallow-e6gz`** — needs re-scoping to near-empty, not rewriting. Its original criteria demanded
  "a single left-to-right comment scanner, recovered from `5641bcf2^` … exported from
  `packages/testing` on a node-safe subpath", which this plan deliberately does not do; and its
  remaining substance (Phase 4b's sweep) shipped inside `Wallow-xg9t.1`. `wallow/module-imports` was
  never written, because the sweeps it would have absorbed were deleted rather than converted. Close
  it, or reduce it to whatever is genuinely left after Phase 0.
- **`Wallow-xg9t`** (epic) — description still says the epic "starts empty by construction" and
  asks for reparenting as its first action. All five children are already parented. Update.
- **`Wallow-xg9t.1`** is the Phase 0 bead. It shipped the `.claude/rules/TESTING.md` rule, the
  `wallow/no-source-tests` rule with its three fixtures, registration in all five nested configs,
  the sweep over 74 source-reading specs — 44 files deleted outright and 30 thinned in place, with
  one of the deletions (`packages/sdk/src/server/h3-free.test.ts`) replaced by a behavioural spec —
  and the CLAUDE.md/doc rewrites that cited them. Three of the 77 inventoried were kept unchanged:
  the two oxlint-binary runners and `@bc-solutions-coder/testing`'s build-config reader.
- **`Wallow-succ`** — rewritten. Was "assert the scrim's computed alpha is < 1", now "click the
  backdrop, assert the drawer closes". The `PublicLayout` footer site is dropped from it. The
  `Wallow-h9k1` blocking edge is **removed**; it is still blocked by `Wallow-tkyq` (the router stub
  it builds on).
- **`Wallow-h9k1`** — rewritten. Was "hoist the pointer park into `@bc-solutions-coder/testing`",
  now "make colour specs name their pointer state per assertion; delete the seven parks". It adds
  nothing to `packages/testing` and no longer gates anything.
- **`Wallow-mlc3`** is closed and stays closed; it is cited here only as the evidence that lint
  wiring can be silently absent — the argument the JSONC-reading guards in
  `packages/sdk/src/oxlint-guardrails.test.ts` survived on.

## Quality gate

`pnpm check` (format:check + lint + lint:tests + typecheck + test + build + check:exports) green at
the end of every phase, not only at the end. The exception the original plan anticipated —
Phase 0 landing red against the sweeps and needing temporary overrides — did not arise: because
Phase 0 deleted the sweeps in the same pass, the rule shipped green with no overrides at all.
