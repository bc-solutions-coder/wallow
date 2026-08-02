# No source tests — lint owns structure, tests own behaviour

**status: active**

Epic: `Wallow-xg9t` (fix the guards that do not guard). Supersedes the approach recorded in
`Wallow-e6gz`, whose acceptance criteria call for recovering and hoisting a comment scanner.
We are deleting the scanner's reason to exist instead.

## The problem

Sixteen spec files read application source off disk as text and assert against the string. Every
one of them strips comments first, and every one hand-rolls the same two-pass stripper:

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

The inventory has been re-derived three times and landed on 23, then 18, then 16. Files leave
when a sweep is deleted; files arrive because **nothing forbids writing a new one**. Two of the
current sixteen — `packages/navigation/src/shell-source.test.ts` and
`packages/logger/src/charter.test.ts` — landed *hours after* `Wallow-e6gz` was filed to fix them.

There is no rule against source tests. `doctrine` and `shape pin` appear in zero markdown files
in this repo; the phrase exists only in bead prose. The nearest written guidance is one bullet at
`packages/testing/CLAUDE.md:186` ("assert a feature seam by identity, not presence"), and it
points the right way but constrains nothing.

So the sweeps were never violating anything. That is the actual defect. Hoisting a better scanner
would make sixteen unsound specs sound and leave the seventeenth free to be written next week.

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

To be added to `.claude/rules/TESTING.md`:

> - **No source tests.** A test never reads application source, README prose, or directory layout
>   off disk — no `readFileSync` over `src/`, no comment stripping, no assertions about file
>   anatomy, imports, or class strings. Constraining how code is *written* is a **linter's** job: a
>   `wallow/*` AST rule fires in the editor, names the offending line, and cannot be defeated by
>   formatting (`packages/lint/CLAUDE.md`). A test calls a function or renders a component and
>   asserts what happens. Most structural sweeps should not become rules either — a spec pinning
>   file counts or README wording makes the codebase rigid without making it correct; prefer
>   deleting the constraint to relocating it. `wallow/no-source-tests` enforces this. The single
>   exception is `@bc-solutions-coder/testing`'s `./browser-styles-wiring`, which reads a
>   consumer's build config to prove the browser project has a stylesheet attached — build wiring,
>   not application source, and it has no behavioural equivalent short of rendering everything and
>   noticing it is unstyled.

Two things follow that are worth stating outright, because they are the point rather than a side
effect:

1. **Deleting a constraint is the preferred outcome, not the fallback.** A sweep that mandates a
   five-file folder anatomy or forbids an example app from growing a query is rigidity without
   correctness. Most of the sixteen die with nothing replacing them.
2. **Coverage genuinely lost is recorded here** rather than discovered later. `Wallow-succ` exists
   because a correct deletion had no successor and nobody wrote that down. See
   *Deliberate holes*.

## Disposition

Every assertion across the sixteen files falls into one of three buckets.

### Bucket A — delete, no successor

The constraint is not worth enforcing in any tool.

| File | What goes |
| --- | --- |
| `apps/examples/minimal-app/src/sdk-wiring.test.ts` | **whole file.** Every `it` regex-matches a file it read. Its own header concedes "there is nothing left to unit-test". Includes two README assertions (`expect(readme).toMatch(/frontend-state\.md/u)` and a broken-link checker), an `existsSync(...) === false` pinning a file deleted in a past refactor, and "adds no live queries" — a spec that fails when a developer improves the example app. |
| `packages/navigation/src/shell-source.test.ts` | **whole file.** Two of its seven `it`s test the spec's *own regex helpers* against inline string literals, asserting nothing about the app. Plus `/\bnavControlClass\b/` — a word-presence check — and "asks for the outline variant at both call sites". |
| 5× `packages/ui/src/components/*/…composition.test.ts` | "ships the five-file catalog anatomy" (`existsSync` × 5 per component); the class-string pins ("hard-codes no layout or colour utility in its JSX", "carries no opacity-suffixed colour in its recipe", "derives the row's test id" via `toMatch(/-item/u)`). |
| `apps/wallow-auth/src/features-api-seam.test.ts` | "owns a co-located `api.test.ts`", "names a surface". |
| `apps/wallow-auth/src/shared-current-user.test.ts` | the archaeological half: "declares no `queryFn` of its own", "no second current-user probe survives anywhere under `src/`". |
| 3× `features/*/api.test.ts` | "never adopts the generated mutation factory", "has no bare operation on the generated entry to prefer instead". |
| all files | the vacuity meta-guards — "finds the features it is meant to cover", "finds seams to check at all", "is scanning a source tree that still has the route in it". They exist only to prove the sweep is not scanning an empty list, so they die with it. |

### Bucket B — delete, becomes a lint rule

| Rule | Absorbs |
| --- | --- |
| `wallow/module-imports` — options `{ allow, allowRelative, exportStar }` | `feature-barrels.test.ts` ×2 ("barrel is re-exports only", "reaches only its own modules"); `features-api-seam.test.ts` ("`api.ts` is a re-export and nothing else", "reaches only the SDK's entries"); `packages/env` charter's "imports nothing at all" (`allow: []`) |
| `wallow/no-source-tests` | the enforcer — bans `readFileSync` / `readdirSync` / `existsSync` in `**/*.test.{ts,tsx}` |

One options-driven rule rather than three narrow ones, following `text-heading-variant`'s
precedent. `packages/env`'s "reads no environment of its own" does **not** get a rule: `process.env`
already fails to typecheck under the `compilerOptions.types: []` the same charter asserts
separately, and `import.meta.env` alone does not justify a rule. If it proves to matter, it is a
five-line addition later.

`wallow/no-source-tests` needs exactly two overrides, and **each must re-declare the root's
options rather than extend them** — oxlint override entries *replace*:

- `packages/lint/**` — rule specs shell out to the real oxlint binary and read fixture files.
- `packages/testing/src/browser-styles-wiring.ts` — the sanctioned exception above.

### Bucket C — keep, already behaviour

- **Identity re-exports** — `expect(api.forgotPassword).toBe(sdkForgotPassword)` in the three
  `features/*/api.test.ts`. Per `packages/testing/CLAUDE.md:186`, this is the one construct a call
  site cannot observe: a hand-written look-alike carries the same name, shape and type, passes
  every behavioural spec, and reaches an undocumented endpoint.
- **Barrel dynamic imports** — the ui composition specs' `typeof barrel.listRowRecipe === "function"`.
- **`shared-current-user.test.ts`'s async specs** — "hands this app the current-user hook", "holds a
  resolved user long enough that a re-mounted probe is a cache read". Real behaviour.
- **The `.oxlintrc.json` wiring assertions** in the `env` and `logger` charters — "keys an override
  on this package's source", "restates every ban the root config makes", "bans every other
  workspace package". These read *config*, not source, through `readJsonc` (line-comments-only,
  JSONC), which never touches the defective stripper.

That last group is the keystone and gets **more** load-bearing under this plan, not less. If lint
becomes the primary guard, the specs verifying lint is actually wired on are the only thing
standing between us and a silently inert plugin. `Wallow-mlc3` — closed 2026-08-02 — was exactly
that failure: `wallow/*` rules inert over three packages, passing everything.

The remaining charter assertions ("publishes the same subpaths it resolves from source", "gives
every subpath a lib entry", "points the published conditions at the built files") are duplicates of
`pnpm check:exports` (publint + `@arethetypeswrong/cli` over real `dist/`) and go with Bucket A.

### Net effect

Two files deleted outright. Fourteen survive substantially thinner. Two new lint rules. `grep -rln
"replaceAll(/\/\*" apps packages` over test files returns zero — by deletion, not by hoisting. No
185-line scanner, no fixture tree, no thirteenth subpath export on `packages/testing`.

## Phases

Ordering is a real dependency chain, not a preference.

| Phase | Bead | Work | Why here |
| --- | --- | --- | --- |
| **0** | new | `.claude/rules/TESTING.md` rule + `wallow/no-source-tests` (registered, overrides in place) | Gate. Stops a seventeenth sweep landing mid-project — which is how this backlog grew in the first place. The rule will be red against the existing sweeps; that is expected and is what Phases 4a/4b clear. |
| **1** | `Wallow-tkyq` | Fix the real navigation escaping into the vitest iframe from `app-shell.toggle.test.tsx` | `packages/navigation` browser runs must be trustworthy before Phase 3 adds a spec there. Today the leak tears down the runner and takes 4 sibling files (28 tests) with it. |
| **2** | `Wallow-h9k1` | Export the before-mount pointer park from `@bc-solutions-coder/testing` on a browser-only subpath | Any computed-colour spec must park the pointer first, or it measures a hover state left by whichever file ran before it. Prerequisite for Phase 3. |
| **3** | `Wallow-succ` | Nav scrim computed-colour spec in `packages/navigation` | Establishes the replacement technique for every deleted colour pin: render, locate by testid, `getComputedStyle`, normalise through a canvas 2d context, assert actual paint. Template for Phase 4b. |
| **4a** | `Wallow-e6gz` | Write and register `wallow/module-imports`, with fixture specs, **while the sweeps still run** | Rules prove they fire before anything is deleted. Overlap is deliberate. |
| **4b** | `Wallow-e6gz` | Delete Buckets A and B; thin the survivors to Bucket C | The sweep. |
| **5** | `Wallow-uhef` | Decide the browser project's `testTimeout` on a quiet machine | **Last.** This is a measurement bead, and Phase 4b removes node specs while Phase 3 adds browser specs. Measuring before the suite settles measures a suite that will not exist. |

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

## Bead impact

- **`Wallow-e6gz`** — description and acceptance criteria need rewriting. Current criteria demand
  "a single left-to-right comment scanner, recovered from `5641bcf2^` … exported from
  `packages/testing` on a node-safe subpath", which this plan deliberately does not do. New
  criterion: the grep returns zero by deletion; `wallow/module-imports` and `wallow/no-source-tests`
  ship and are registered; Bucket C survives intact; `pnpm check` green.
- **`Wallow-xg9t`** (epic) — description still says the epic "starts empty by construction" and
  asks for reparenting as its first action. All five children are already parented. Update.
- **Phase 0 needs a new bead** under `Wallow-xg9t`, blocking `Wallow-e6gz`.
- **`Wallow-mlc3`** is closed and stays closed; it is cited here only as evidence for keeping the
  Bucket C lint-wiring assertions.

## Quality gate

`pnpm check` (format:check + lint + lint:tests + typecheck + test + build + check:exports) green at
the end of every phase, not only at the end. Phase 0 is the exception: `wallow/no-source-tests`
fires against the sweeps it exists to remove, so Phase 0 lands with the overrides that keep `pnpm
check` green and Phase 4b removes them.
