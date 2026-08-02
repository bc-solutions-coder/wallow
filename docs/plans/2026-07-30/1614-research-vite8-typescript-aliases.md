**status: active**

# Research: Vite 8 / TypeScript path aliases for the Wallow monorepo

Role: VITE/TYPESCRIPT RESEARCHER. Worktree read: `/Users/traveler/Repos/Wallow-alias-research`.
Installed source read from `/Users/traveler/Repos/Wallow/node_modules` (same pnpm lockfile; the
worktree has no `node_modules`).

Every claim below carries a source tag. `[AUTHORITATIVE: …]` = official docs, installed package
source, named maintainer statement, or an experiment I ran myself against the installed toolchain.
`[CORROBORATING ONLY: …]` = blog/SO/third-party. Where I found nothing, I say so.

## Executive summary — three findings that change the question

1. **Vite 8 reads `tsconfig.json` `paths` natively** via `resolve.tsconfigPaths: true`, and ships a
   runtime warning telling you to delete `vite-tsconfig-paths`. The premise "Vite doesn't read
   tsconfig paths" was true through Vite 7 and is **false for Vite 8**.
2. **The repo is not on TypeScript 7.** `tsconfig.base.json`'s "TypeScript 7 / Project Corsa"
   comment is factually wrong — every workspace member resolves **5.9.3**. TS 6 and TS 7 *are*
   released and installable; adopting them changes nothing about this decision (I verified with
   TS 7.0.2's native `tsc`).
3. **The `aliases.ts` doc comment is wrong.** Vite string aliases match *exact-or-path-segment*
   prefix, not arbitrary string prefix. `@app` would **not** swallow `@application`. The trailing
   slash is load-bearing for a different reason than the comment states.

---

## Q1 — Vite 8 `resolve.alias` semantics

### The matcher

Vite 8 inlines `@rollup/plugin-alias@6.0.0` into its dist bundle (the region comment at
`dist/node/chunks/node.js:5060` names the source path). The matcher is:

```js
function matches$1(pattern, importee) {
	if (pattern instanceof RegExp) return pattern.test(importee);
	if (importee.length < pattern.length) return false;
	if (importee === pattern) return true;
	return importee.startsWith(pattern + "/");
}
```

[AUTHORITATIVE: node_modules/.pnpm/vite@8.1.4_*/node_modules/vite/dist/node/chunks/node.js:5071]

So for a **string** `find`: match iff `importee === find` OR `importee` starts with `find + "/"`.
**This is not arbitrary string-prefix matching.** A `find` of `@app` matches `@app` and
`@app/anything`; it does **not** match `@application`.

For a **RegExp** `find`: plain `pattern.test(importee)` — unanchored unless you anchor it.
[AUTHORITATIVE: same line]

### Substitution and ordering

```js
resolveId(importee, importer, resolveOptions) {
    const matchedEntry = entries.find((entry) => matches$1(entry.find, importee));
    if (!matchedEntry) return null;
    const updatedId = importee.replace(matchedEntry.find, matchedEntry.replacement);
```

[AUTHORITATIVE: .../vite/dist/node/chunks/node.js:5105]

- **First match wins** (`Array.prototype.find`) — array order is evaluation order. Object form is
  converted to an array in key order, so put narrower entries first.
- Substitution is a single `String.prototype.replace` with the find as-is — for a string find that
  means "replace the first occurrence", for a regex it means capture groups (`$1`) work.

### The trailing-slash normalizer

```js
function normalizeSingleAlias({ find, replacement, customResolver }) {
	if (typeof find === "string" && find.endsWith("/") && replacement.endsWith("/")) {
		find = find.slice(0, find.length - 1);
		replacement = replacement.slice(0, replacement.length - 1);
	}
```

[AUTHORITATIVE: .../vite/dist/node/chunks/node.js:2885]

Vite strips the trailing slash from **both** find and replacement — but **only if both have it**.
This is why the repo's `"@app/": "/abs/src/app/"` works: it is normalized back to
`"@app" → "/abs/src/app"` before it ever reaches the matcher.

### Empirical verification (three real `vite build` runs)

I built a scratch project against the installed Vite 8.1.4 with three alias spellings:

| Spelling | Result |
| --- | --- |
| `{"@app/": "<abs>/src/app/"}` (repo's form) | builds |
| `{"@app": "<abs>/src/app"}` (bare, both sides) | **builds** — and `@application` resolved separately, NOT swallowed |
| `{"@app/": "<abs>/src/app"}` (slash on find only) | **hard failure**: `Rolldown failed to resolve import "@app/thing"` |

[AUTHORITATIVE: experiment run against node_modules/.pnpm/vite@8.1.4, scratchpad `alias-probe/`]

The third row is the real hazard the trailing-slash convention protects against: an asymmetric
trailing slash survives normalization and then the matcher requires `@app/` + `/`, i.e. `@app//…`.

A separate probe confirmed segment-swallowing **is** real: `{"pkg": "<abs>/src/pkg.ts"}` plus
`import "pkg/sub"` errored with "Not a directory", because `pkg/sub` matched (segment prefix) and
rewrote to `<abs>/src/pkg.ts/sub`. [AUTHORITATIVE: same probe]

### Consequences for the repo's current code

- `apps/*/aliases.ts` comment — *"a bare `@app` key would also swallow a future `@application`"* —
  is **factually wrong**. Disproved by build. The trailing-slash form is still fine (it normalizes to
  the same thing), but its stated justification is not.
- `apps/wallow-web/vite.config.ts`'s comment about a string alias swallowing
  `use-sync-external-store/shim/with-selector` **is correct** — that one *is* a path-segment
  extension, exactly what the matcher catches. The two anchored regexes there are correctly placed
  before the zone aliases (first-match-wins).

### Per-environment? No.

```ts
interface EnvironmentResolveOptions {
  mainFields?; conditions?; externalConditions?; extensions?; dedupe?;
  noExternal?; external?; builtins?;
}                                    // <- no alias, no tsconfigPaths
interface ResolveOptions extends EnvironmentResolveOptions {
  preserveSymlinks?: boolean;
  tsconfigPaths?: boolean;
}
type AllResolveOptions = ResolveOptions & { alias?: AliasOptions };
interface SharedEnvironmentOptions { resolve?: EnvironmentResolveOptions; … }
```

[AUTHORITATIVE: .../vite/dist/node/index.d.ts:1963-2003]

`alias` and `tsconfigPaths` live only on the **top-level** `ResolveOptions`/`AllResolveOptions`.
`SharedEnvironmentOptions.resolve` is typed `EnvironmentResolveOptions`, which has neither.
**Aliases cannot be set per-environment in the Environment API; they apply uniformly.** The wiring
in `resolvePlugins` reads `config.resolve.alias` (top level) and swaps in rolldown's native
`viteAliasPlugin` per environment when `environment.config.isBundled`, but the *data* is still the
one top-level list. [AUTHORITATIVE: .../vite/dist/node/chunks/node.js:29708-29722]

### What changed in Vite 8 vs 7 (alias/resolve)

- **New:** `resolve.tsconfigPaths` (see Q2).
- **Deprecated:** `resolve.alias[].customResolver` (#21476).
- Resolution now runs through rolldown/oxc (`oxcResolvePlugin` / `viteResolvePlugin` imported from
  `rolldown/experimental` at `.../vite/dist/node/chunks/node.js:23`), not esbuild — esbuild is an
  optional peer in Vite 8.
- I found **no** change to the alias matching algorithm itself between v5–v8; the
  `@rollup/plugin-alias` matcher is unchanged. [AUTHORITATIVE: installed source comparison]

---

## Q2 — Does Vite read `tsconfig.json` `paths` natively?

**Yes, as of Vite 8.** `resolve.tsconfigPaths: boolean`, default `false`.

Installed type declaration:

```ts
/**
 * Enable tsconfig paths resolution
 * @default false
 * @experimental
 */
tsconfigPaths?: boolean;
```

[AUTHORITATIVE: .../vite/dist/node/index.d.ts:1997-2003]

Vite 8 actively tells you to remove the community plugin:

```js
const tsconfigPathsPlugin = userPlugins.find(
  (p) => p.name === "vite-tsconfig-paths" || p.name === "vite-plugin-tsconfig-paths");
if (tsconfigPathsPlugin) logger.warnOnce(yellow(
  `The plugin ${JSON.stringify(tsconfigPathsPlugin.name)} is detected. Vite now supports tsconfig `
+ `paths resolution natively via the resolve.tsconfigPaths option. You can remove the plugin and `
+ `set resolve.tsconfigPaths: true in your Vite config instead.`));
```

[AUTHORITATIVE: .../vite/dist/node/chunks/node.js:35599-35600]

Implementation is in Rust, in rolldown's resolver: `BindingViteResolvePluginResolveOptions`
carries `tsconfigPaths: boolean`, and `NapiResolveOptions` carries
`tsconfig?: 'auto' | { configFile: string; references?: 'auto' }`.
[AUTHORITATIVE: node_modules/.pnpm/rolldown@1.1.5/node_modules/rolldown/dist/shared/binding-D26QphWG.d.mts:1911]

### History and caveats

- Added in Vite 8.0.0. PR #22038 also made it resolve `#`-prefixed `paths` entries; PR #22775
  extended it to CSS/Sass `@import`. [AUTHORITATIVE: vitejs/vite changelog + PR numbers]
- **Documented performance cost** — resolution has to consult tsconfig on misses.
- **`paths` only applies to files matched by that tsconfig's `files`/`include`.** This is the
  subtle one for a monorepo: a file outside the tsconfig's `include` gets no mappings.
- **Does not work inside `.less`.**
  [AUTHORITATIVE: https://vite.dev/config/shared-options#resolve-tsconfigpaths]

### Discrepancy I could not reconcile — report as observed

The `main`-branch CHANGELOG records for 8.1.0-beta.0 (2026-06-15): *"remove `@experimental` from
`resolve.tsconfigPaths` JSDoc"* (#23006). **The installed 8.1.4 `dist/node/index.d.ts` still carries
`@experimental`.** I re-verified with `grep -n -B6 "tsconfigPaths?: boolean"`. Treat the option as
stable-in-intent but still marked experimental in the artifact you actually have installed.
[AUTHORITATIVE: installed dist/node/index.d.ts:1997-2003 vs vitejs/vite CHANGELOG.md]

### What else Vite reads from tsconfig

Vite has always read a small, fixed set — it does **not** consume tsconfig generally:

- `compilerOptions.target` (for esbuild/oxc transform target)
- `compilerOptions.useDefineForClassFields`
- `compilerOptions.jsx`, `jsxFactory`, `jsxFragmentFactory`, `jsxImportSource`
- `compilerOptions.experimentalDecorators`, `emitDecoratorMetadata`
- `compilerOptions.verbatimModuleSyntax` / `importsNotUsedAsValues` / `preserveValueImports`
- `compilerOptions.baseUrl` + `paths` — **only** when `resolve.tsconfigPaths` is on

[AUTHORITATIVE: https://vite.dev/guide/features#typescript-compiler-options and
`resolveTsconfig` imported from `rolldown/experimental`, .../vite/dist/node/chunks/node.js:23]

### Verified empirically

`resolve.tsconfigPaths: true` with `paths: {"@app/*": ["./src/app/*"]}` and **no** `resolve.alias`:
`vite build` succeeded and `vitest run` passed in both the node and the real-Chromium browser
project. [AUTHORITATIVE: experiment, scratchpad `tspaths-probe/`]

### Maintainer position

- **@bluwy** (vitejs/vite #6828) explains why tsconfig `paths` was historically *not* mapped onto
  `resolve.alias`: `paths` is a TS-only, JS/TS-module-only construct, whereas Vite's alias must
  apply to every resource kind; and he raises the resolution-performance concern.
- **@haoqunjiang** in the same thread makes the same point — *tsconfig `paths` only covers JS/TS
  modules while an alias has to cover CSS, assets, and everything else* — and twice recommends the
  alternative: *"How about subpath imports like `#dep`? It's natively supported by both Node.js and
  TypeScript 5.4+."*
- **@sapphi-red** closed #6828 on 2026-01-29: *"Closing as Vite 8 beta supports this… That said,
  note that the TypeScript team discourages this."* (citing the TS docs' own `paths` guidance).

[AUTHORITATIVE: maintainer @bluwy / @haoqunjiang / @sapphi-red, vitejs/vite issue #6828, retrieved
via `gh issue view 6828 --repo vitejs/vite --json comments`]

---

## Q3 — `vite-tsconfig-paths`

- Current version **6.1.1**, last modified 2026-03-29. `peerDependencies: { vite: "*" }`.
  [AUTHORITATIVE: npm registry metadata for `vite-tsconfig-paths`]
- **Not installed in this repo.** No workspace member depends on it.
  [AUTHORITATIVE: repo `pnpm-lock.yaml` / package.json survey]
- It is the de-facto community standard for Vite ≤ 7 and is used by numerous templates.
  [CORROBORATING ONLY: general ecosystem usage — I found no *official* Vite or TanStack template
  in an authoritative source that ships it by default; **no authoritative source found** for the
  "official template" part of the question.]

**Verdict: do not adopt it.** Vite 8 deprecates it by name at runtime (Q2). Adding a plugin that
the framework explicitly asks you to remove is negative-value, and it adds a plugin to the
TanStack Start + Nitro pipeline for a capability the bundler already has natively.

**The one thing the plugin still does better:** its `projects` option lets you point at specific
tsconfigs explicitly. The native option has no such knob — this is a **known open gap**:
vitejs/vite issue **#22112** (OPEN, labelled `p3-downstream-blocker`, related PR #22533) requests a
config object precisely because *"`resolve.tsconfigPaths` handles tsconfig selection internally via
unconfigurable `includes` detection."* [AUTHORITATIVE: vitejs/vite issue #22112]

For Wallow this gap is **not** binding: each app has exactly one `tsconfig.json` whose `include`
already covers `src/**`, which is the only place the zone aliases are used.

---

## Q4 — Node.js `imports` subpath imports (`#app/*`)

### Node semantics

- `"imports"` in package.json: added **Node v14.6.0 / v12.19.0**.
- Every entry **must** start with `#`. `#` and `#/` alone are reserved.
- `*` in a target is **pure string replacement, including `/`** — it is not a path-segment glob.
- Conditions apply: `node`, `import`, `require`, `module-sync`, `default`, plus community
  conditions (`types`, `browser`, `development`, `production`).
- *"Unlike the `"exports"` field, the `"imports"` field permits mapping to external packages."*
- Node **v25.4.0 / v24.14.0** additionally allow `#/`-prefixed subpath imports (no extra segment).

[AUTHORITATIVE: https://nodejs.org/api/packages.html#subpath-imports]

### TypeScript support

`package.json` `"imports"` is honoured under `moduleResolution` **`node16`**, **`nodenext`**, and
**`bundler`** — `bundler`'s documented feature list explicitly includes package.json
`"exports"`/`"imports"`/self-name resolution.
[AUTHORITATIVE: https://www.typescriptlang.org/tsconfig/#moduleResolution and
https://www.typescriptlang.org/docs/handbook/modules/reference.html#bundler]

TS 6.0 added support for the shorter `#/*` form: *"This is supported in newer Node.js 20 releases,
and so TypeScript now supports it under the options `nodenext` and `bundler` for the
`--moduleResolution` setting."*
[AUTHORITATIVE: https://www.typescriptlang.org/docs/handbook/release-notes/typescript-6-0.html]

And TypeScript's own `paths` documentation recommends it: *"Both libraries and apps can consider
package.json `"imports"` as a standard replacement for convenience `paths` aliases."*
[AUTHORITATIVE: https://www.typescriptlang.org/tsconfig/#paths]

### The convention that actually works — established empirically

This is the part that is easy to get wrong, and I nailed it down by experiment:

| Form | TS result |
| --- | --- |
| `"#shared/*": "./src/shared/*.js"` + `import "#shared/util"` | **works** — resolves `util.ts` **and** `util.tsx` |
| `"#shared/*": "./src/shared/*"` (extensionless target) + `import "#shared/util"` | **fails** TS2307 |
| `"#shared/*": "./src/shared/*.js"` + `import "#shared/util.js"` | **fails** |

So: **the `imports` target carries the `.js` extension; the specifier does not.** TS's extension
substitution (`.js` → `.ts`/`.tsx`/`.d.ts`/`.js`/`.jsx`) does the rest.
[AUTHORITATIVE: experiment against typescript@5.9.3 and typescript@7.0.2, scratchpad `imports-probe/`]

**Limitation found:** directory/index imports do **not** work. `#shared/dir` with
`src/shared/dir/index.ts` fails TS2307. Every import must name a file. [AUTHORITATIVE: same probe]

### Toolchain support — all verified, not recalled

| Tool | Result |
| --- | --- |
| **Vite 8.1.4** | Resolves `#` imports with **zero config**. `vite build` bundled both `.ts` and `.tsx` sources. Native handling at `.../vite/dist/node/chunks/node.js:32006, 32160` (`subpathImportsPrefix = "#"`, `resolveSubpathImports()`), and rolldown's `BindingViteResolvePluginConfig.resolveSubpathImports` hook. |
| **Vitest 4.1.10** | Resolves them with **zero config in BOTH the node project AND the real-Chromium browser project** (2 passed). |
| **oxlint 1.74.0** | Clean, including with `--import-plugin -D import/no-unresolved`. |
| **oxfmt** | Clean. |
| **TypeScript 5.9.3 and 7.0.2** | Both clean. |

[AUTHORITATIVE: installed source at the cited lines + experiments in scratchpad `imports-probe/`]

### Trade-offs vs tsconfig `paths`

**For:** zero plugins, zero build-config; one declaration site (`package.json`) understood by Node,
Vite, Vitest, TypeScript, oxlint and every editor without configuration; conditions
(`browser`/`node`, `development`/`production`) are available if ever needed; it is the mechanism
TypeScript's own docs point you at.

**Against:** the `#` sigil is mandatory and non-negotiable (`#app/*`, not `@app/*`) — this is a
visible, repo-wide rename; no directory/index imports; targets need the `.js` extension, which
reads oddly next to `.ts` sources; and it is scoped to the nearest `package.json`, so
`packages/*` each get their own copy of the convention rather than sharing one.

### **Q4 verdict: production-viable.**

Not "viable with caveats" — viable. Every layer of this specific toolchain (Vite 8, Vitest 4 in
both node and browser mode, TS 5.9 and TS 7, oxlint, oxfmt) resolved `#` specifiers with **no
configuration at all**, and I confirmed each by running it rather than by reading about it.

**The one risk I did not close:** my probes used plain Vite, **not** `tanstackStart()` + `nitro()`.
TanStack Start's `importProtection` plugin and the Nitro server bundle are the untested surface.
This is a one-afternoon spike on a real app, not a research question — but it must be run before
committing. **No authoritative source found** on `#` imports through the Nitro 3 beta pipeline.

---

## Q5 — TypeScript `paths` best practice, and the actual TS version

### Release status — factual

npm dist-tags for `typescript`, checked today:

```
latest: 7.0.2      rc: 7.0.1-rc      beta: 6.0.0-beta      next: 7.1.0-dev.20260730.1
```

[AUTHORITATIVE: `npm view typescript dist-tags`]

**TypeScript 6 and 7 are released and installable.** But **this repo runs TypeScript 5.9.3** —
every workspace member pins `"typescript": "^5.6.0"` and resolves to 5.9.3.
[AUTHORITATIVE: repo `package.json` files + per-package resolution check]

**`tsconfig.base.json`'s header comment — "Shared TypeScript 7 compiler semantics … TS7 (Project
Corsa) is a faithful native port of TS6" — is false as a description of what is installed.** Worth
fixing regardless of the alias outcome.

### Is `baseUrl` needed with `moduleResolution: "Bundler"`? No — and omitting it is now the
### officially correct form.

TS 6.0: *"In TypeScript 6.0, `baseUrl` is deprecated and will no longer be considered a look-up
root for module resolution."* … *"Developers who used `baseUrl` as a prefix for path-mapping
entries can simply remove `baseUrl` and add the prefix to their `paths` entries."*

The migration example in the TS 6.0 release notes is **character-for-character the repo's current
tsconfig**:

```json
{ "compilerOptions": { "paths": {
    "@app/*": ["./src/app/*"],
    "@lib/*": ["./src/lib/*"] } } }
```

[AUTHORITATIVE: https://www.typescriptlang.org/docs/handbook/release-notes/typescript-6-0.html]

**The repo's `paths`-without-`baseUrl` is not merely acceptable — it is exactly the shape TS 6
tells you to migrate to.** `paths` have not required `baseUrl` since TS 4.1.

Note also: TS 7.0 *will not support any deprecated options*, and `moduleResolution: classic` is
removed in 6.0. Neither affects this repo.

### Official `paths` guidance

- *"`paths` … does not affect emit"* — it is a type-resolution-time mapping only; the bundler must
  agree independently. This is the root cause of the repo's four-declaration-site problem.
- *"`paths` should not point to monorepo packages or node_modules packages"* — i.e. `paths` is for
  *intra-package* convenience aliases only. The repo complies (all three zones are inside the app).
- *"Both libraries and apps can consider package.json `"imports"` as a standard replacement for
  convenience `paths` aliases."*

[AUTHORITATIVE: https://www.typescriptlang.org/tsconfig/#paths]

### Monorepo + project references

Rolldown's resolver exposes `references?: 'auto'` on its tsconfig options, so tsconfig `references`
are followed when present. [AUTHORITATIVE: rolldown@1.1.5 binding-D26QphWG.d.mts]
This repo uses **no** project references today, so there is nothing to reconcile.

### Does TS 6/7 change any of this? No.

I installed **TypeScript 7.0.2** and ran its native `tsc` against both probes:

```
=== TS7 version ===                        Version 7.0.2
=== TS7 on tsconfig paths (no baseUrl) ===  (clean)
=== TS7 on package.json #imports probe ===  (clean)
```

[AUTHORITATIVE: experiment, scratchpad `ts7/`]

Both approaches survive TS 7 unchanged. **The TypeScript version is not a decision input here.**

---

## Q6 — Scaling comparison

Q6's candidate list was drafted before the Q2 finding, so candidate A splits in two. I score them
as six.

Current cost, measured: **4 declaration sites per app** (`aliases.ts`, `vite.config.ts`,
`vitest.config.ts` — the latter two import the first, so 1 real + 2 wiring — plus the hand-mirrored
`tsconfig.json` `paths`), pinned by a bespoke ~70-line `alias-map.test.ts` per app. Two apps carry
the full apparatus; `apps/examples/minimal-app` and all seven `packages/*` carry none.

### Scoring (1 = worst, 5 = best)

| | **A1** native `resolve.tsconfigPaths` | **A2** `vite-tsconfig-paths` plugin | **B** pkg.json `imports` (`#app/*`) | **C** shared build-config package | **D** no aliases | **E** zones as workspace packages |
| --- | :-: | :-: | :-: | :-: | :-: | :-: |
| Edit sites / new alias | **1** (tsconfig) | 1 | **1** (package.json) | 2 | n/a | many (new pkg) |
| Edit sites / new app | 1 line of Vite config | plugin + config | **0** (just the field) | dep + import | 0 | high |
| Drift risk | **none** | none | **none** | low, not zero | none | none |
| Pin test needed | **no** — delete `alias-map.test.ts` | no | **no** — delete it | still yes | n/a | no |
| IDE / go-to-def | 5 (TS's own field) | 5 | 5 (TS + Node both native) | 5 | 5 | 4 (needs build or `exports` to source) |
| Vitest parity (node + browser) | 4 — inherits app config; **per-project override untested** | 4 | **5 — verified passing in both projects** | 4 | 5 | 4 |
| Build / SSR parity | 4 — native, but `@experimental` in installed d.ts | 3 — deprecated by Vite | **5 — resolved by Vite with zero config** | 4 | 5 | 4 |
| Reuse in `packages/*` | 4 (each gets a tsconfig line) | 4 | 4 (each gets a package.json field) | 3 | 5 | 5 |
| Migration cost | **lowest** — keep `@app/*` spelling | low | medium — `@app` → `#app`, every import rewritten, targets need `.js` | low | very high | very high |
| Ecosystem trajectory | rising (Vite 8 native) | **falling (deprecated)** | rising (TS + Node + Vite all endorse) | flat | flat | flat |
| **Total** | **strong** | weak | **strongest** | middling | poor fit | poor fit |

### Notes on the losers

- **A2** — actively deprecated by Vite 8 at runtime. No reason to pick it over A1. Eliminate.
- **C** — keeps the duplication and merely relocates it; still needs a pin test; and the existing
  `alias-map.test.ts` comment already argues against it ("would couple every app to a build
  package"). It solves the symptom, not the cause.
- **D** — relative imports across `src/app` ↔ `src/features` ↔ `src/shared` is exactly the churn
  the three-zone layout exists to prevent, and it discards the import-DAG enforcement the recent
  commits built.
- **E** — real workspace packages are the right answer when zones need independent versioning or
  publishing. These are three folders in one app. Massive ceremony for an import-path problem.

### Recommendation

**Primary: B — `package.json` `"imports"` (`#app/*`, `#features/*`, `#shared/*`).**

It is the only candidate where **every** tool in this stack resolved the specifier with **zero
configuration**, and I verified that by running Vite 8 build, Vitest 4 in both the node and the
real-Chromium browser project, oxlint (with `import/no-unresolved`), oxfmt, and both TS 5.9.3 and
TS 7.0.2. It collapses 4 declaration sites to 1, deletes both `alias-map.test.ts` files and both
`aliases.ts` modules, removes the `resolve.alias` block from both `vite.config.ts` and both
`vitest.config.ts`, and it is what TypeScript's own docs and Vite maintainer @haoqunjiang both
recommend. It also scales to `packages/*` for free — a package.json field, no build config.

Its costs are real and should be accepted with open eyes: the `#` sigil is mandatory, index/directory
imports stop working, and targets carry a `.js` extension that will look wrong to every reviewer
until they learn why.

**Runner-up: A1 — `resolve.tsconfigPaths: true`.**

Meaningfully cheaper to adopt: keep the `@app/*` spelling and every existing import, delete
`aliases.ts` + `alias-map.test.ts` + both alias blocks, add one line of Vite config per app. Choose
this if the `#`-sigil rename is judged too invasive, or if the TanStack Start/Nitro spike (below)
turns up a problem with `#` imports. Its two open questions are the still-`@experimental` marking in
the installed 8.1.4 d.ts and the unconfigurable tsconfig selection (#22112) — neither binding here.

Both A1 and B eliminate the drift class entirely and make the pin tests obsolete. Either is a
strict improvement on today.

### Two risks I could not close — flagging honestly rather than assuming

1. **TanStack Start + Nitro.** My probes ran plain Vite, not `tanstackStart({importProtection})` +
   `nitro()`. Whether `#` specifiers (B) and `resolve.tsconfigPaths` (A1) survive the Start route
   generator, `importProtection`, and the Nitro server bundle is **unverified**. **No authoritative
   source found.** Spike one app before committing.
2. **Vitest per-project resolve.** For A1 specifically, `resolve.tsconfigPaths` is a top-level Vite
   option and the repo's `vitest.config.ts` sets `resolve.alias` **per project**. Whether the
   top-level setting reaches both projects in this repo's actual two-project split is **untested**.
   (B has no such question — it needs no `resolve` config at all, and I verified both projects
   passing.)

---

## Appendix — where the current code is wrong

1. `apps/wallow-web/aliases.ts` and `apps/wallow-auth/aliases.ts`: *"a bare `@app` key would also
   swallow a future `@application`"* — **false**, disproved by build. Vite matches
   exact-or-path-segment.
2. `tsconfig.base.json`: *"Shared TypeScript 7 compiler semantics"* / *"TS7 (Project Corsa)"* —
   **false**; the repo runs 5.9.3. TS 7.0.2 is available if the claim is meant to become true.
3. `apps/*/tsconfig.json`: *"`moduleResolution: Bundler` resolves these relative to this file, so no
   `baseUrl` is needed"` — **correct**, and now additionally correct because TS 6.0 deprecates
   `baseUrl` outright.
4. `apps/wallow-web/vite.config.ts`'s `use-sync-external-store/shim` comment — **correct**.
