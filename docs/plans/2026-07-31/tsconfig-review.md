**status: active**

# tsconfig & build-orchestration review

Scope: `tsconfig.base.json`, every member `tsconfig*.json`, all `package.json` `scripts` and
`exports` maps, `pnpm-workspace.yaml`, `scripts/check-exports.sh`. Toolchain under review:
TypeScript **7.0.2** (native/Go, "Project Corsa") workspace-wide, **6.0.3** for `packages/sdk`,
Vite **8.1.4**, pnpm **10.20.0**, oxlint **1.74.0**.

Every claim below was executed against the working tree. The repo was restored after each
experiment (`git diff` on all config files: empty).

---

## 1. Empirical findings

### 1.1 The "must build first" claim is TRUE — but only for two of the three commands

Method: `mv packages/*/dist packages/*/dist.bak`, run the command, restore.

| Command | From zero `dist/` | Evidence |
| --- | --- | --- |
| `pnpm build` (`pnpm -r build`) | **PASSES**, exit 0 | topological order held: `query,sdk,styles` → `auth,testing` → `ui` → `forms` → apps |
| `pnpm typecheck` | **FAILS**, exit 1 | 19 × `TS2307` across `packages/auth` + `packages/testing` |
| `pnpm --filter …/wallow-web build` | **FAILS**, exit 1 | `ERR_MODULE_NOT_FOUND` loading `vite.config.ts` |

So the team-lead hypothesis that `pnpm -r build` is already topologically ordered is **confirmed**,
and a full `pnpm build` from a clean clone genuinely works today. The breakage is confined to
`typecheck` and to building *one* app without building the workspace first.

Real `typecheck` failure output (truncated):

```
packages/auth typecheck: src/current-user.ts(31,30): error TS2307: Cannot find module
  '@bc-solutions-coder/query' or its corresponding type declarations.
packages/auth typecheck: src/current-user.ts(37,8): error TS2307: Cannot find module
  '@bc-solutions-coder/sdk' or its corresponding type declarations.
packages/auth typecheck: src/current-user.ts(38,45): error TS2307: Cannot find module
  '@bc-solutions-coder/sdk/query' or its corresponding type declarations.
packages/testing typecheck: src/render-with-wallow.tsx(20,50): error TS2307: Cannot find module
  '@bc-solutions-coder/query' or its corresponding type declarations.
packages/testing typecheck: src/sdk-harness.ts(27,49): error TS2307: Cannot find module
  '@bc-solutions-coder/sdk' or its corresponding type declarations.
```

Real per-app `build` failure output:

```
vite.config.ts (1:339) [UNRESOLVED_IMPORT] Could not resolve
  '@bc-solutions-coder/styles/vite' in vite.config.ts
failed to load config from /Users/…/apps/wallow-web/vite.config.ts
Error [ERR_MODULE_NOT_FOUND]: Cannot find module
  '/Users/…/apps/wallow-web/node_modules/@bc-solutions-coder/styles/dist/vite.js'
```

Note this second failure is **not a TypeScript failure at all**. It is Node's ESM resolver, loading
the app's own `vite.config.ts`, failing to find `styles/dist/vite.js`. No amount of tsconfig work
fixes it; it is purely an `exports`-map fact.

### 1.2 `pnpm check` is broken on a clean clone

```
"check": "pnpm format:check && pnpm lint && pnpm typecheck && pnpm test && pnpm build && pnpm check:exports"
```

`typecheck` (3rd) and `test` (4th) both run **before** `build` (5th). On a fresh clone with no
`dist/`, the advertised one-command quality gate fails at step 3. It only appears to work because
every developer machine and CI cache already has a populated `dist/`. This is the single most
user-visible symptom of the diagnosis.

### 1.3 The cause is exactly the `exports` map — proven by inversion

I rewrote every `packages/*/package.json` `exports`/`main`/`module`/`types` from `./dist/*.js` +
`./dist/*.d.ts` to `./src/*.ts`, deleted all seven `dist/` directories, and re-ran:

```
=== TEST 1: pnpm typecheck ===
typecheck exit: 0
packages/query typecheck: Done      packages/ui typecheck: Done
packages/styles typecheck: Done     packages/forms typecheck: Done
packages/sdk typecheck: Done        apps/examples/minimal-app typecheck: Done
packages/auth typecheck: Done       apps/wallow-auth typecheck: Done
packages/testing typecheck: Done    apps/wallow-web typecheck: Done
```

**All ten projects typecheck clean with zero build artifacts.** The dist-only `exports` map is the
entire cause, and TS7 resolves `.ts` source through an `exports` map without complaint under
`moduleResolution: "Bundler"`.

### 1.4 The one genuine obstacle, and its documented fix

With src-pointing exports, `vite build` got *further* and then failed differently:

```
Error [ERR_MODULE_NOT_FOUND]: Cannot find module '/Users/…/packages/styles/src/assets'
  imported from /Users/…/packages/styles/src/vite.ts
```

Two things to read here. First, Node **did** load `packages/styles/src/vite.ts` — Node 24's
type-stripping handled the `.ts` file, and pnpm's symlink realpath puts it outside `node_modules`
so stripping was not disabled. Second, it then died on `import { brandAssetsDir } from "./assets"`
— an *extensionless relative* import, which `moduleResolution: "Bundler"` permits and Node's ESM
resolver rejects. There are ~1035 such specifiers across the workspace, so "add extensions" is not
a viable fix.

Vite documents this exact scenario (https://vite.dev/config/):

> By default, Vite uses `esbuild` to bundle the config into a temporary file and load it. **This may
> cause issues when importing TypeScript files in a monorepo.** If you encounter any issues with this
> approach, you can specify `--configLoader runner` to use the module runner instead, which will not
> create a temporary config and will transform any files on the fly.

Verified — src-pointing exports, zero `dist/`, `vite build --configLoader runner`:

```
=== vite build --configLoader runner ===
exit: 0
.output/server/_ssr/ssr.mjs   171.01 kB │ gzip: 43.61 kB
✓ built in 227ms
ℹ Generated .output/nitro.json
```

A complete `wallow-web` production build (SSR + Nitro bundle) from a clean tree with no package ever
built. **This is the proof that the requirement can be removed outright, not merely automated.**

### 1.5 Only two of seven packages are actually published

| package | `private` | published? |
| --- | --- | --- |
| auth, forms, query, testing, ui | `true` | never |
| **sdk**, **styles** | absent | yes (GitHub Packages) |

This shrinks the migration enormously: five packages can point `exports` at `src/` with **zero**
publish consequences. Only `sdk` and `styles` need a `publishConfig.exports` override.

pnpm's documented `publishConfig` override list (https://pnpm.io/package_json) explicitly includes
`exports`, alongside `main`, `module`, `types`, `browser`, `typesVersions`. The docs annotate only
the recent additions with version notes (`engines` v10.22.0, `name` v11.18.0); `exports` carries no
version note and predates 10.20, so it is available on the pinned pnpm 10.20.0.

### 1.6 TypeScript 7 fully supports `--build` / project references

From the `microsoft/typescript-go` feature matrix (upstream README):

| Feature | Status |
| --- | --- |
| Build mode / project references | **done** |
| Incremental build | **done** |
| Declaration emit | **done** |
| API | **not ready** |

Confirmed locally: `tsc --build --help` on 7.0.2 prints the full build-orchestrator help. So option
2 (project references + `composite`) is technically available — see §3 for why I still do not
recommend it.

The `API: not ready` row is the load-bearing one for §5: it is why `packages/sdk` is stuck on TS6.

### 1.7 `rootDir` is genuinely required under TS7 — not an artifact

The `tsconfig.build.json` comments claim TS7 requires an explicit `rootDir` when `outDir` is set.
Tested by deleting it from `packages/query/tsconfig.build.json`:

```
error TS5011: The common source directory of 'tsconfig.norootdir.json' is './src'.
The 'rootDir' setting must be explicitly set to this or another path to adjust your
output's file layout.
  Visit https://aka.ms/ts6 for migration information.
```

**The comments are accurate and `rootDir` must stay.** This is a real TS7 behaviour change (TS6
inferred the common source directory silently). Do not "clean this up".

### 1.8 `packages/sdk/tsconfig.json` is drift, not a deliberate pin

It is the only member that does not extend `tsconfig.base.json`. It silently runs weaker settings:
`target: ES2022` (vs `esnext`), `lib: ES2022` (vs `ESNext`), and **no** `isolatedModules`, **no**
`verbatimModuleSyntax`, no `jsx`, no `resolveJsonModule`, no `forceConsistentCasingInFileNames`.

Tested: `packages/sdk` compiled against full `tsconfig.base.json` semantics, using its own TS 6.0.3
binary — **exit 0, zero errors**. Nothing in the SDK depends on the weaker settings. This is
accumulated drift and should be collapsed into `extends: "../../tsconfig.base.json"`.

### 1.9 The seven `tsconfig.build.json` files are ~100% boilerplate

`compilerOptions` is **byte-identical** in all seven:

```json
{"declaration":true,"emitDeclarationOnly":true,"rootDir":"src","outDir":"dist"}
```

Comment-stripped hashes show only four distinct files, and the only real variation is
`include`/`exclude`: `auth` ≡ `query`, `forms` ≡ `ui`. `sdk` (4 entries), `testing` (5 entries) and
`styles` (3 entries) differ only because they hand-list more entry points.

The hand-listed `include` **is** still required — it is what keeps `*.test.ts` and root `*.config.ts`
out of the emit program so no stray `.d.ts` leaks into `dist/`. `packages/sdk`'s comment about
`server/passthrough.ts` needing an explicit listing (nothing imports it, so an unlisted entry emits
no `.d.ts`) is correct and load-bearing. Keep the `include` lists; extract only `compilerOptions`.

### 1.10 `scripts/check-exports.sh` audits three packages that are never published

It runs `publint --strict` and `attw --pack` over `packages/auth packages/query packages/sdk
packages/styles packages/testing`. Three of those (`auth`, `query`, `testing`) are `private: true`.
Linting the publish surface of a package that has no publish surface is wasted CI time and, worse,
constrains their `exports` maps for no reason — it is part of why the src-pointing option was never
taken. Meanwhile `forms` and `ui` carry a `files: ["dist"]` field that is inert (both private).

### 1.11 Stale claims inventory

| Location | Claim | Reality |
| --- | --- | --- |
| `packages/sdk/tsconfig.build.json:16` | "typescript devDependency tracks the rest of the workspace (**^5.6.0**)" | Workspace is **7.0.2**; sdk is pinned to **6.0.3** via `tooling-tsc6`. Both halves wrong. |
| `packages/sdk/tsconfig.build.json:17` | "openapi-ts declares peer `typescript: ^5.5.3`" | Installed 0.99.0 declares `">=5.5.3 \|\| >=6.0.0 \|\| 6.0.1-rc"` |
| `packages/sdk/tsconfig.build.json:19` | "need a **5.x** compiler" | 6.0.3 is what is actually installed and required |
| `packages/sdk/tsconfig.build.json:16` | "rather than carrying its own pin" | It *does* carry its own pin (`catalog:tooling-tsc6`) — the sentence asserts the opposite of the truth |
| `packages/sdk/vite.config.ts:14` | "stable programmatic API does not land until 7.1" | Still true as of 7.0.2 (upstream matrix: `API: not ready`) — **keep** |
| `packages/sdk/vite.config.ts:5,19` | references to "the previous tsup pipeline" | Historical, harmless, but tsup is long gone |

The `packages/sdk/tsconfig.build.json` header is the worst offender: four wrong facts in one
paragraph, all describing a TS5-era world.

---

## 2. Recommendation — ONE path

> **Point `exports` at `src/` for in-repo resolution; keep `dist/` for published consumers via
> `publishConfig.exports`; switch app config loading to `--configLoader runner`.**

This removes the requirement (option **a** — what the user actually asked for) rather than
automating it. After it lands, a fresh clone can run `pnpm typecheck`, `pnpm test`, and any single
app's `build` or `dev` with **no prior build step at all**, and `pnpm build` keeps working exactly
as it does today.

Why not the alternatives:

- **Project references + `composite` (option 2).** TS7 supports it (§1.6), and it *would* fix
  `typecheck`. But it fixes only TypeScript. It does nothing for §1.4 — Node still cannot load
  `vite.config.ts`'s `@bc-solutions-coder/styles/vite` import, because that resolution never goes
  through tsc. So project references leave the per-app build broken *and* add `composite: true`,
  `.tsbuildinfo` management, and a `references` array to all ten members. Strictly more config for
  strictly less benefit. Rejected.
- **Stay dist-only, make `pnpm build` reliable (option 3).** `pnpm -r build` is *already* reliable
  and topologically correct (§1.1). "Making it reliable" would mean reordering `pnpm check` to put
  `build` before `typecheck`/`test`, which is a one-line fix worth doing regardless — but it leaves
  the developer inner loop paying a full workspace build to typecheck one file. It is the fallback,
  not the goal.

Cost, honestly stated:

- ~14 files edited (7 `package.json`, 4 app/package scripts, 1 shared tsconfig, `.oxlintrc`,
  `check-exports.sh`). No source-code changes.
- One behavioural change to be aware of: app builds now compile package **source**, so a type error
  in `packages/ui` surfaces during an app build instead of being frozen into a stale `.d.ts`. This
  is a benefit, but it will surface latent errors on the first run.
- `publishConfig.exports` is only exercised at `pnpm publish`/`pnpm pack`. It must be verified once
  by `pnpm pack packages/sdk` and inspecting the packed `package.json` before the next `sdk-v*` tag.
  This is the one place the change could silently break a real consumer, so it gates on that check.
- `--configLoader runner` does not support CJS *in config files* (external CJS packages still work).
  All configs here are ESM/TS, so this is inert — but it is a real constraint if a fork adds a CJS
  config.

---

## 3. Target-state config

### 3.1 `packages/*/package.json` — private packages (auth, forms, query, testing, ui)

Point everything at source. No `publishConfig` needed; these never publish. Example — `packages/ui`:

```jsonc
{
  "name": "@bc-solutions-coder/ui",
  "private": true,
  "type": "module",
  // In-repo resolution reads SOURCE. This package is `private: true` and is never
  // published, so there is no dist-facing contract to preserve — apps, vitest and
  // tsc all resolve straight into src/ and nothing has to be built first.
  // `pnpm build` still emits dist/ (the app bundlers tree-shake from source; dist/
  // exists for `pnpm check:exports` parity and for forks that choose to publish).
  "main": "./src/index.ts",
  "module": "./src/index.ts",
  "types": "./src/index.ts",
  "exports": {
    ".": { "types": "./src/index.ts", "import": "./src/index.ts" },
    "./*": { "types": "./src/components/*/index.ts", "import": "./src/components/*/index.ts" },
    "./source.css": "./source.css"
  }
}
```

Drop the now-inert `"files": ["dist"]` from `forms` and `ui` (both private).

`packages/testing` keeps all five subpaths, rewritten the same way — note `render` and
`render-with-wallow` are `.tsx`:

```jsonc
  "exports": {
    ".":                    { "types": "./src/index.ts",               "import": "./src/index.ts" },
    "./render":             { "types": "./src/render.tsx",             "import": "./src/render.tsx" },
    "./sdk-harness":        { "types": "./src/sdk-harness.ts",         "import": "./src/sdk-harness.ts" },
    "./contrast":           { "types": "./src/contrast.ts",            "import": "./src/contrast.ts" },
    "./render-with-wallow": { "types": "./src/render-with-wallow.tsx", "import": "./src/render-with-wallow.tsx" }
  }
```

### 3.2 `packages/sdk` and `packages/styles` — published, so dual-map

```jsonc
{
  "name": "@bc-solutions-coder/sdk",
  "version": "0.2.0",
  "files": ["dist"],
  // The manifest's `exports` is the IN-REPO map: it points at src/ so that apps,
  // vitest and tsc resolve this package without it having been built. pnpm
  // rewrites `exports` from `publishConfig.exports` when packing, so the PUBLISHED
  // tarball ships the dist/ map below and no consumer ever sees a .ts specifier.
  // https://pnpm.io/package_json#publishconfig
  //
  // These two maps must stay key-for-key identical. `pnpm check:exports` packs the
  // package and therefore validates the PUBLISHED (dist) side; the in-repo side is
  // validated by `pnpm typecheck` passing from a clean tree.
  "main": "./src/index.ts",
  "module": "./src/index.ts",
  "types": "./src/index.ts",
  "exports": {
    ".":                    { "types": "./src/index.ts",              "import": "./src/index.ts" },
    "./server":             { "types": "./src/server/index.ts",       "import": "./src/server/index.ts" },
    "./server/passthrough": { "types": "./src/server/passthrough.ts", "import": "./src/server/passthrough.ts" },
    "./query":              { "types": "./src/query/index.ts",        "import": "./src/query/index.ts" }
  },
  "publishConfig": {
    "access": "restricted",
    "registry": "https://npm.pkg.github.com",
    "main": "./dist/index.js",
    "module": "./dist/index.js",
    "types": "./dist/index.d.ts",
    "exports": {
      ".":                    { "types": "./dist/index.d.ts",              "import": "./dist/index.js" },
      "./server":             { "types": "./dist/server/index.d.ts",       "import": "./dist/server/index.js" },
      "./server/passthrough": { "types": "./dist/server/passthrough.d.ts", "import": "./dist/server/passthrough.js" },
      "./query":              { "types": "./dist/query/index.d.ts",        "import": "./dist/query/index.js" }
    }
  }
}
```

`packages/styles` follows the same shape; its `"./styles.css": "./styles.css"` entry is a raw
passthrough and is identical on both sides.

### 3.3 New `tsconfig.build.base.json` (repo root)

Extracts the byte-identical block from §1.9. Members keep their own `include`/`exclude`.

```jsonc
{
  // Shared declaration-emit settings for every buildable workspace package.
  // The package `build` script is `vite build && tsc -p tsconfig.build.json`:
  // Vite 8 (Rolldown) emits the JS bundle but no declarations, so the native
  // `tsc --emitDeclarationOnly` CLI owns .d.ts.
  //
  // `rootDir` is explicit because TS7 REQUIRES it whenever `outDir` is set —
  // omitting it is `error TS5011` (TS6 inferred the common source directory).
  // Verified against 7.0.2; do not remove it.
  //
  // Members supply `include` (their public entry points) and `exclude`. That
  // narrowing is load-bearing, not boilerplate: it keeps *.test.ts and root
  // *.config.ts out of the emit program so no stray declarations reach dist/,
  // and an entry nobody imports (packages/sdk's server/passthrough.ts) emits
  // no .d.ts at all unless it is listed.
  "compilerOptions": {
    "declaration": true,
    "emitDeclarationOnly": true,
    "rootDir": "src",
    "outDir": "dist"
  }
}
```

Each member's `tsconfig.build.json` collapses to (example — `packages/query`):

```jsonc
{
  // Declaration-only build config; see ../../tsconfig.build.base.json for the
  // shared compiler settings and the rationale for `rootDir`.
  // `include` is the public entry point; tsc transitively pulls in every real
  // src file it imports.
  "extends": ["./tsconfig.json", "../../tsconfig.build.base.json"],
  "include": ["src/index.ts"],
  "exclude": ["**/*.test.ts", "**/*.config.ts"]
}
```

This removes ~90 lines of duplicated comment and 28 duplicated option lines across the seven files.
(TS supports an array `extends`, merged left-to-right, since 5.0.)

### 3.4 `packages/sdk/tsconfig.json` — adopt the base

Per §1.8 this passes clean today. Replace the whole redeclared block:

```jsonc
{
  // Follows the workspace TS baseline like every other member. NOTE: this package
  // still runs the TS 6.0.3 BINARY (catalog:tooling-tsc6, see pnpm-workspace.yaml)
  // because its OpenAPI codegen drives the JS compiler API, which the native 7.x
  // compiler does not ship. That splits which binary runs, NOT which options apply
  // — TS6 and TS7 run the same type-checking algorithms, and this package compiles
  // clean under the full base config (verified).
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "declaration": true,
    "outDir": "dist",
    "types": ["node"]
  },
  "include": ["src", "scripts"]
}
```

And rewrite the stale `tsconfig.build.json` header (§1.11) to state the real reason for the TS6 pin.

### 3.5 `package.json` scripts

Root:

```jsonc
{
  "scripts": {
    "backend": "dotnet run --project api/src/Wallow.AppHost",
    "backend:infra": "cd docker && docker compose up -d",
    "backend:infra:down": "cd docker && docker compose down",
    "dev": "pnpm --parallel --filter @bc-solutions-coder/wallow-web --filter @bc-solutions-coder/wallow-auth dev",

    "build": "pnpm -r build",
    "build:packages": "pnpm --filter './packages/*' build",
    "test": "pnpm -r test",
    "typecheck": "pnpm -r typecheck",

    // Source files only. Test and story files are linted by `lint:tests` against
    // .oxlintrc.tests.json, which layers the vitest plugin on top of the same base.
    "lint": "oxlint apps packages --ignore-pattern='**/*.test.ts' --ignore-pattern='**/*.test.tsx' --ignore-pattern='**/*.stories.tsx' --deny-warnings",
    "lint:tests": "./scripts/lint-tests.sh --deny-warnings",
    "lint:all": "pnpm lint && pnpm lint:tests",
    "lint:fix": "oxlint apps packages --fix",

    "format": "oxfmt --write apps packages tools package.json pnpm-workspace.yaml tsconfig.base.json tsconfig.build.base.json .oxlintrc.json .oxlintrc.tests.json .oxfmtrc.json",
    "format:check": "oxfmt --check apps packages tools package.json pnpm-workspace.yaml tsconfig.base.json tsconfig.build.base.json .oxlintrc.json .oxlintrc.tests.json .oxfmtrc.json",

    "check:exports": "./scripts/check-exports.sh",
    // typecheck/test no longer need a prior build (exports resolve to src/), but
    // check:exports packs the published tarballs and so still runs after build.
    "check": "pnpm format:check && pnpm lint:all && pnpm typecheck && pnpm test && pnpm build && pnpm check:exports",
    "prepare": "husky"
  }
}
```

Each app (`wallow-web`, `wallow-auth`, `examples/minimal-app`) — the `--configLoader runner` flag
from §1.4:

```jsonc
{
  "scripts": {
    // `--configLoader runner` loads vite.config.ts through Vite's module runner
    // instead of pre-bundling it to a temp file for Node to import. That is what
    // lets the config's `@bc-solutions-coder/styles/vite` import resolve into
    // package SOURCE: the module runner transforms .ts on the fly and applies
    // Vite's resolver (extensionless relative imports included), where Node's ESM
    // resolver cannot. Without it, `vite build` in a tree with no packages/*/dist
    // fails with ERR_MODULE_NOT_FOUND. See https://vite.dev/config/
    "dev": "vite dev --configLoader runner",
    "build": "vite build --configLoader runner",
    "start": "node .output/server/index.mjs",
    "typecheck": "tsc --noEmit",
    "test": "vitest run",
    "test:e2e": "playwright test"
  }
}
```

Package-level scripts are unchanged.

### 3.6 `scripts/check-exports.sh`

Narrow the package list to the two that actually publish, and note what `publishConfig` means for
the check:

```bash
# Only the packages that are actually published. auth/query/testing/ui/forms are
# `private: true` and have no publish surface to lint.
#
# Both tools PACK the package, so they see the `publishConfig.exports` rewrite —
# i.e. they validate the dist/ map, which is exactly what a consumer gets. The
# in-repo src/ map is validated instead by `pnpm typecheck` passing from a clean
# tree. `pnpm --filter './packages/*' build` must still have run first.
packages=(packages/sdk packages/styles)
```

---

## 4. Lint split

Verified against oxlint 1.74.0:

| Selection | Files |
| --- | --- |
| `oxlint apps packages` (today) | 836 |
| source only (`--ignore-pattern` × 3) | **437** |
| tests/stories only (explicit paths) | **399** |

437 + 399 = 836 — a clean partition, no double-linting and no gaps. Nearly half the current lint
surface is test code.

Two constraints found by testing, both of which shape the design:

1. **oxlint does not expand globs in path arguments.** `oxlint 'apps/**/*.test.ts'` lints 0 files.
   Paths must be pre-expanded by the shell or a script.
2. **`ignorePatterns` negation does not work.** `--ignore-pattern='**' --ignore-pattern='!**/*.test.ts'`
   lints 0 files, so the tidy "exclude everything, re-include tests" trick is unavailable.

Hence: the source side selects by *ignoring* tests (works, 437 files), and the test side selects by
*explicit paths* (works, 399 files) via a small script — matching the existing
`scripts/check-exports.sh` convention in this repo.

`.oxlintrc.json` keeps every rule it has today and stays the auto-discovered default (so editors
still lint test files). Only two things change: the existing `**/*.test.*` / `**/*.stories.tsx`
override block **moves out** into the tests config, and `oxlint` gains `extends` support usage.

New `.oxlintrc.tests.json`:

```jsonc
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  // Spec-file lint config. Inherits every rule, plugin and no-restricted-imports
  // policy from the base config, then (a) enables the vitest plugin, which the
  // source config has no use for, and (b) relaxes the rules that only ever fire
  // on test code. Driven by `pnpm lint:tests`; `pnpm lint` lints the other 437
  // files with these relaxations NOT applied.
  "extends": ["./.oxlintrc.json"],
  "plugins": ["typescript", "unicorn", "oxc", "react", "vitest"],
  "rules": {
    "no-await-in-loop": "off",
    "no-magic-numbers": "off",
    "sort-keys": "off",
    "require-unicode-regexp": "off",
    "require-await": "off",
    "no-shadow": "off",
    "prefer-named-capture-group": "off",
    "unicorn/prefer-response-static-json": "off",
    "unicorn/numeric-separators-style": "off",
    "unicorn/catch-error-name": "off",
    "unicorn/prefer-import-meta-properties": "off",
    "unicorn/no-useless-undefined": "off",
    "unicorn/consistent-function-scoping": "off",
    "unicorn/relative-url-style": "off",
    "typescript/consistent-type-imports": "off",
    "typescript/consistent-generic-constructors": "off",
    "typescript/array-type": "off",
    "react/jsx-props-no-spreading": "off"
  }
}
```

New `scripts/lint-tests.sh`:

```bash
#!/usr/bin/env bash
# Lints ONLY spec and story files, against .oxlintrc.tests.json.
#
# The paths are expanded here rather than passed as globs because oxlint does not
# expand globs in path arguments (verified: `oxlint 'apps/**/*.test.ts'` lints 0
# files), and its ignorePatterns do not support `!` negation, so the inverse
# "ignore everything except tests" selection is unavailable.
#
# Counterpart: `pnpm lint` lints everything EXCEPT these files. The two together
# cover the workspace exactly once.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

mapfile -t files < <(
  find apps packages \
    -type d -name node_modules -prune -o \
    -type f \( -name '*.test.ts' -o -name '*.test.tsx' -o -name '*.stories.tsx' \) -print
)

if [ "${#files[@]}" -eq 0 ]; then
  echo "no spec files found" >&2
  exit 1
fi

pnpm exec oxlint -c .oxlintrc.tests.json "$@" "${files[@]}"
```

Note `--vitest-plugin` is available and currently **not** enabled anywhere, so test files get no
vitest-specific linting today. Turning it on for the spec config is free coverage; expect it to
flag some existing specs on first run.

---

## 5. The TS6/TS7 split — containment

Today `packages/sdk` runs the **6.0.3** binary for `generate`, `build`, `typecheck` and `test`,
because `@hey-api/openapi-ts` constructs AST nodes via the JS compiler API that TS7 does not ship
(upstream matrix: `API: not ready`). The consequence is that the SDK — the package with the widest
blast radius in the workspace — is the one package typechecked by a *different compiler* than
everything that consumes it, which is precisely the failure mode the `tooling` catalog comment warns
about.

Upstream has **not** shipped a fix: openapi-ts 0.99.0's peer range
`">=5.5.3 || >=6.0.0 || 6.0.1-rc"` stops short of 7.x, and it still drives the JS API.

The requirement leaks from `generate` into the test suite because three specs import the generator
config directly:

- `packages/sdk/src/runtime-config.test.ts`
- `packages/sdk/src/openapi-generator-config.test.ts`
- `packages/sdk/src/generated-query-surface.test.ts`

each doing `import … from "../openapi-ts.config"`.

**Cleaner containment (recommended as a follow-up, not part of this migration):** move
`openapi-ts.config.ts`, `scripts/generate.ts` and those three specs into a private
`tools/sdk-codegen` workspace package that owns `typescript: catalog:tooling-tsc6`. `packages/sdk`
then moves to `catalog:tooling` like every other member, and the TS6 pin shrinks from "the whole SDK
and its test suite" to "the code generator". The generated output is a committed artifact
(`packages/sdk/openapi/v1.json` → generated client), so nothing in the normal build path would
touch TS6 at all.

This is a separate, larger change with its own risk (three specs move packages, CI drift check must
follow). Keep the current pin until then — but fix its four wrong comments (§1.11) now.

---

## 6. Migration order

Each step is independently verifiable; stop at any point and the tree still works.

1. **Reorder `pnpm check`** so `build` precedes `typecheck`/`test` — or land step 4 first, which
   makes the ordering moot. One line; fixes §1.2 immediately regardless of everything else.
2. **Fix the stale comments** (§1.11) in `packages/sdk/tsconfig.build.json`. Zero behaviour change,
   removes four wrong facts.
3. **Collapse `packages/sdk/tsconfig.json` onto `tsconfig.base.json`** (§3.4). Verify with
   `pnpm --filter @bc-solutions-coder/sdk typecheck` — proven clean (§1.8).
4. **The exports flip.** Add `tsconfig.build.base.json` (§3.3) and rewrite all seven `exports` maps
   (§3.1, §3.2), plus `--configLoader runner` on the three apps (§3.5).
   Verify: `rm -rf packages/*/dist && pnpm typecheck && pnpm --filter @bc-solutions-coder/wallow-web build`
   — both must pass with no build step. This is the payload; it is the step proven end-to-end in
   §1.3 and §1.4.
5. **Verify the publish side.** `pnpm pack packages/sdk` and `pnpm pack packages/styles`; unpack and
   confirm the packed `package.json` carries the **dist** exports map. This gates the next `sdk-v*`
   tag and is the only irreversible-facing risk in the whole plan.
6. **Narrow `scripts/check-exports.sh`** to `sdk` + `styles` (§3.6); drop the inert
   `files: ["dist"]` from `forms` and `ui`.
7. **Split the lint** (§4): add `.oxlintrc.tests.json` and `scripts/lint-tests.sh`, move the test
   override block out of `.oxlintrc.json`, update root scripts. Expect first-run findings from the
   newly enabled vitest plugin; fix or explicitly disable them in the same commit.
8. **Follow-up bead:** contain the TS6 pin in `tools/sdk-codegen` (§5).

Steps 1–3 are safe cleanups. Step 4 is the change the user asked for. Steps 5–7 are consequences.
