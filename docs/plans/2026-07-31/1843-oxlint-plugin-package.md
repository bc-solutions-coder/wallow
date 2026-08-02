# Design — `@bc-solutions-coder/lint`, a first-class oxlint plugin package

**status: active**

Wallow's four custom lint rules live in `tools/oxlint/wallow-lint-plugin.js`: one 435-line
untyped, untested file, loaded by relative path from two app configs. Authoring a fifth rule
means hand-writing an AST visitor into that file and verifying it by eye. This design moves the
rules into a real workspace package with types, a fixture test harness, and a generator — and
then uses that capability to retire ~1,300 lines of hand-rolled guard specs that only existed
because writing a rule was hard.

> **Amended after approval.** Implementation was scoped down to **`zone-dag` only** — see
> `1912-oxlint-plugin-package-plan.md`. `server-only-naming`, `feature-barrels`,
> `client-navigation`, the generator, the registry and the suppression census keep their analysis
> below but are deferred. One substantive reversal: `zone-dag.test.ts`'s `shared/` subdirectory
> allowlist is **not** preserved — `shared/` is no longer shape-locked to a fixed set of
> subdirectories, so that assertion is deleted rather than kept as a spec, and both copies of the
> file retire in full.

Two halves, approved separately:

- **Section A — packaging.** Where the plugin lives, how it resolves in-repo, how it publishes,
  how a rule is authored and tested.
- **Section B — conversion.** Which existing vitest guard specs become rules, which must not,
  and the one-sentence boundary between them.

Everything asserted below about oxlint's behaviour was verified against the real binary
(oxlint 1.74.0, `@oxlint/plugins` 1.76.0) in the worktree `Wallow-lint-spike` on branch
`spike/oxlint-plugin-package`. Claims are marked **[verified]** where a probe proved them.

---

## Section A — packaging

### Home and identity

`packages/lint`, published as `@bc-solutions-coder/lint`. A genuine package, not an internal
file: the plugin should be usable by a fork or by another repo, which rules out keeping it under
`tools/`.

`@oxlint/plugins` is a **runtime `dependencies`** entry, not a devDependency — a consumer that
installs the published package needs `definePlugin`/`defineRule` at load time.

### Resolution: source in-repo, built for publish

The `packages/query` pattern (established by `584adc3b`), which this repo already runs:

```json
"exports":       { ".": { "types": "./src/index.ts",   "import": "./src/index.ts" } },
"publishConfig": { "exports": { ".": { "types": "./dist/index.d.ts", "import": "./dist/index.js" } } }
```

In-repo, oxlint loads `src/index.ts` directly — Node 24 strips the types **[verified]**, and a
bare specifier resolves from a nested app config **[verified: the rule fired on
`apps/wallow-web/src/spike-check.tsx`]**. On publish, `publishConfig` swaps the map to `dist/`.

Two constraints this settles, both of which cost real time in the spike:

- **Relative imports inside `src/` must carry the `.ts` extension** — `./rules/no-tinted-text.ts`,
  not `./rules/no-tinted-text`. oxlint loads the plugin as plain Node ESM, which rejects
  extensionless relative specifiers with `ERR_MODULE_NOT_FOUND` while typechecking clean. This is
  the exact trap `packages/config/CLAUDE.md` already documents.
- `allowImportingTsExtensions` normally requires `noEmit`. The publish build resolves that with
  **`rewriteRelativeImportExtensions`** (TypeScript 7.0.2 in this repo), which rewrites `.ts` →
  `.js` on emit.

**No bundle is generated into the repo, and nothing regenerates one in a hook.** A committed or
hook-regenerated bundle *creates* the staleness it claims to prevent: `dist/` is gitignored with
zero committed dist files, `pnpm check` runs lint **before** build, and `.lintstagedrc.mjs`
already runs `oxlint --fix` on staged files — which loads the source plugin and would race a
regeneration step in the same commit.

### Registration — the load-bearing constraint

Rules are registered in the **nested app configs only**, never the repo-root `.oxlintrc.json`,
using the alias form:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
```

**Why the root is off-limits.** `packages/sdk/src/oxlint-guardrails.test.ts` proves the root
config's import bans by copying that config to a temp directory and running the real binary
there. Any `jsPlugins` entry makes the copy unloadable (`Failed to parse oxlint configuration
file`), the spec's `JSON.parse` throws, and **0 tests run** — a silent, total loss of coverage.
This is inherited from the current `tools/` layout, survives the move unchanged, and belongs in
the package's own `CLAUDE.md`.

Second inherited constraint: a nested config must **restate** the root's `apps/<app>/**` override
block and `ignorePatterns`, because oxlint matches both relative to the config's own directory.

### Authoring a rule

- **`createOnce` + `eslintCompatPlugin`** **[verified: `createOnce+before ok, before ran 1x`]**.
  `createOnce` is oxlint's forward-compatible API — called once per process rather than per file,
  with `before()`/`after()` hooks. `eslintCompatPlugin(...)` adds a delegating `create` so the
  same plugin runs under ESLint 8+, which is what makes publishing worthwhile.
- **Messages via `meta.messages` + `messageId`**, not inline strings. Wallow's rule messages are
  long and prescriptive (they name the replacement API); a central catalog keeps them editable
  without touching visitor code.
- **A static rule registry**, generated by a `sync` command and checked by `sync --check` in
  `pnpm check`, so `index.ts` cannot drift from `src/rules/`.
- **Generator**: `new-rule <name>` — a plain Node script, no new dependency, scaffolding rule +
  fixtures + registry entry. Paired with a Skill so the agent-facing path is "invoke the skill",
  not "recall the file layout". This is the piece that makes rule-writing cheap enough for
  Section B to be worth doing.

### Testing rules

On-disk fixtures with `expect-error` annotations, driven by **one generic spec** — verified
working at **5 tests / 238 ms**, and verified to *fail* correctly, naming the exact
`file:line rule` when an unannotated diagnostic appears.

`fixtures/<rule>/` holds `valid.tsx` (reports nothing) and `invalid.tsx` (every expected
diagnostic marked by `// expect-error: wallow/<rule>` on the preceding line). The real binary runs
once per fixture directory with an explicit `-c`, and the reported `(file, line, rule)` multiset
must equal the annotated one **exactly** — an unannotated diagnostic and an annotation nothing
fired on both fail.

Measured facts the harness depends on, all of which cost a debugging cycle to find:

- `--format=json` prints one JSON object but prefixes it with a bare `No files found to lint.`
  line when nothing matched, so `JSON.parse(stdout)` throws. Parse from the first `{`, then assert
  `number_of_files` — otherwise "matched no files" reads as "the valid fixture is clean".
- A diagnostic's rule id is `code`, spelled `wallow(no-tinted-text)`; its line is
  `labels[0].span.line`, 1-based.
- A **syntax** error carries no `code` and suppresses every lint diagnostic in that file. A
  missing `code` must therefore throw loudly, not be skipped.
- `-c` replaces the root config outright, but oxlint's default `correctness` category is still on
  unless the config turns it off.

The harness lives **inside `packages/lint`** with `vitest` as a devDependency. There is no
root-level vitest in this workspace — it is a per-package devDependency, and a harness at the repo
root has nothing to run it.

---

## Section B — conversion

### The finding that reframes this

Four guard specs carry an explicit "**why this is a spec and not an oxlint rule**" header. All
four name the same reason; `zone-dag.test.ts` states it plainest:

> `no-restricted-imports` globs the specifier STRING, and the rule here is about where a path
> RESOLVES.

That is true of the **built-in** rule and void for a **custom** one. Two probes settle it:

- `context.filename` is an **absolute path** **[verified]**, so a rule can resolve a specifier
  against the importer's real directory.
- `createOnce` can do filesystem I/O **once per process** **[verified: `setup=1`, having read
  `apps/wallow-web/tsconfig.json` and recovered `@app/*,@features/*,@shared/*`]**, so a rule can
  read the zone alias map from the same single declaration the spec reads.

The headers are not wrong. They were written when the only available tool was config.

### Tier 1 — retires onto rules (1,324 lines, 7 files, 2 apps)

| Spec | ×2? | Lines | Becomes |
| --- | --- | --- | --- |
| `zone-dag.test.ts` | yes | 748 | `wallow/zone-dag` — reads `tsconfig` `paths` in `createOnce`, resolves each specifier against the importer's real directory, judges the edge. Both apps' copies collapse to one rule with options. |
| `server-only-naming.test.ts` | yes | 252 | `wallow/server-only-naming` — filename plus import specifier, both already in hand. `SERVER_ONLY_SPECIFIERS` becomes a rule option. |
| `client-navigation.test.ts` | web only | 204 | `wallow/no-client-route-anchor` — string-literal `href` classification is pure AST. |
| `feature-barrels.test.ts` | yes | 120 | **splits** — see below. |

`feature-barrels` draws the boundary sharply. "A feature module may not import
`@features/<own name>`" is a rule: filename plus specifier. "Every feature directory contains an
`index.ts`" is **not** — there is no file to attach a diagnostic to. A missing barrel is an
absence, and oxlint only speaks about files it visits. Roughly 15 lines stay a spec; the rest goes.

`client-navigation` gains something in the move. Today it keeps a hand-maintained list of excepted
files and re-checks that each still explains itself in its header, "so an exception cannot be
inherited silently by a later edit that deleted the reasoning." That bookkeeping is replaced by
the ledger below, written once instead of once per spec.

### Tier 2 — stays a test

Type-aware rules are unsupported by oxlint, but nothing in this repo is blocked by that. The real
boundary is narrower and more useful:

> **A rule sees one JS/TS file at a time, and only files oxlint lints.**

Everything here fails *that*, not the type checker:

- **Not a JS/TS file** — `docker-workspace-copies` (Dockerfile ↔ manifest), `oxlint-guardrails`
  (the lint config itself), `query-rule-docs` (markdown), `generated-query-surface` (the OpenAPI
  snapshot), `vitest-projects`, `brand-assets` (a Vite plugin's `config()` hook), `browser-deps`.
- **A relationship between files, or an absence** — `devtools-gating` (a dependency *edge* across
  manifests and the import graph), `web-shell-removal` (already cut to its irreducible
  workspace-level core).
- **Needs a real browser** — `sidebar-surface`, `heading-scale`, the `packages/ui` stories.
  Computed colour and font-size are runtime facts; a class string is not. `cn()` merges a caller's
  `className` over the recipe, so `text-xl` can be present while the element paints something else.
- **Needs runtime or compile-time identity** — `packages/ui/src/index.test.ts` pins the barrel's
  actual exported values plus a compile-time type pin. No linter sees either.

**The one thing conversion loses.** `ignorePatterns` (`dist`, `generated`, `routeTree.gen.ts`,
`.output`) makes a rule **silent** where a disk sweep is loud. Harmless for all of Tier 1 — the
generated route tree is exempt from the zone DAG anyway — but it is a real difference in kind and
belongs in the rule-vs-test guidance rather than being rediscovered later.

### Suppression policy

`// oxlint-disable-next-line wallow/zone-dag` works. A guard spec has no such escape hatch, so
conversion hands every rule an inline opt-out it did not previously have.

**Decision: allow it, require a reason string, and pin the census.** One ~40-line spec greps every
`oxlint-disable*` naming a `wallow/*` rule and asserts the exact `(file, rule, reason)` set.
Adding a suppression then requires editing that list — a review event, not silent inheritance.

The rejected alternative was banning suppression outright (a rule reporting any `wallow/*` disable
comment). Cleaner to state, but the first legitimate exception forces the rule off in config for a
whole glob, which is strictly worse than a one-line ledgered opt-out.

### Net effect

~1,300 lines of hand-rolled path resolution become four rules plus a ~40-line census. The four
rules and their fixtures add back a few hundred lines, so the honest figure is a **net reduction
of roughly 900–1,000 lines** — and the "why this is a spec and not a rule" headers stop being true.

Not counted above: the `query-facade.test.ts` consolidation across three apps (plus the deleted
`packages/forms/src/core/query-facade.test.ts`) was already in flight in the working tree when this
design was written, and is separate work.

---

## Out of scope

- Any change to the repo-root `.oxlintrc.json` beyond what already exists. See the registration
  constraint above.
- Tier 2 specs.
- Publishing the package to a registry. `publishConfig` is wired so it *can* be published; whether
  it is, is a later decision.
