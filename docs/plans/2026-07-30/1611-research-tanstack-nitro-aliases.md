**status: active**

# TanStack Start / Nitro / Vitest path-alias research

Research brief: does TanStack Start have first-class alias support, does Nitro resolve
independently of Vite, and what does TanStack actually recommend? Answers below are sourced
from official docs, installed package source, and **executed experiments** against a freshly
scaffolded Start app.

## Source discipline

Evidence tiers used throughout:

- `[AUTHORITATIVE: ...]` — official docs, installed package source, official examples/scaffolder output.
- `[EXPERIMENT: ...]` — something I ran and observed in this session. Primary evidence.
- `[CORROBORATING ONLY: ...]` — non-authoritative. **I did not need to cite any blog post or
  Stack Overflow answer for this report.** Every claim below rests on tier 1 or 2.

### Reading environment caveat

The research worktree `/Users/traveler/Repos/Wallow-alias-research` has **no `node_modules`
installed**. All package source was read from the main repo's pnpm store,
`/Users/traveler/Repos/Wallow/node_modules/.pnpm/...`, which is driven by the same
`pnpm-lock.yaml`. Versions read: `@tanstack/start-plugin-core@1.171.24`,
`@tanstack/router-plugin@1.168.23`, `@tanstack/router-generator@1.167.21`,
`nitro@3.0.260610-beta`, `vite@8.1.4`, `vitest@4.1.10`.

Line numbers below refer to each package's shipped **`src/`** directory (these packages ship
source alongside `dist/`), so they are directly readable.

### Experiment rig

Several answers rest on a live experiment rather than on reading alone. I scaffolded a real app
with the current official scaffolder and built it:

```
npx create-start-app@latest my-app --template file-router --package-manager npm --no-git
# create-start-app 0.59.41; resolved vite 8.2.0, vitest 4.1.10
```

I then added `src/shared/marker.ts` exporting a unique sentinel string
(`ALIAS_RESOLVED_IN_SSR_BUNDLE_9F3A`), imported it from a route through the alias, ran
`npm run build`, and grepped `.output/server/` and `.output/public/` for the sentinel. Presence
of the sentinel in the emitted bundle is proof the alias resolved in that environment; a
leftover `"@/shared/marker"` string would be proof it did not.

---

## Q1 — Does TanStack Start have first-class alias support?

**Yes — and it is documented, first-class, and delegates entirely to the bundler's tsconfig
`paths` reader. Neither the Start plugin nor the router codegen reads `tsconfig.json` itself.**

TanStack Start ships an official **Path Aliases** guide. It prescribes exactly one mechanism:
declare `paths` in `tsconfig.json`, then tell the bundler to honour them.

> "By default, TanStack Start does not include path aliases. However, you can easily add them to
> your project by updating your `tsconfig.json` [...] After updating your `tsconfig.json` file,
> configure your build tool so it resolves the same path aliases."
>
> "**Vite 8** — Vite 8+ has built-in support for path aliases, which is disabled by default. To
> enable it, simply add [`resolve: { tsconfigPaths: true }`]."
>
> "**Vite 7 and earlier** — install the `vite-tsconfig-paths` plugin."

`[AUTHORITATIVE: https://tanstack.com/start/latest/docs/framework/react/guide/path-aliases — and the identical solid-framework page]`

### Does the Start plugin read tsconfig `paths`?

**No.** I grepped the entire shipped source of `start-plugin-core`, `router-plugin`, and
`router-generator` for `tsconfig`. There are exactly two hits in all three packages, and neither
is a `paths` reader:

1. `start-plugin-core/src/vite/import-protection-plugin/plugin.ts:2184` — a **comment** naming
   `vite-tsconfig-paths` as an example of a third-party resolver whose caches must not leak
   across environments.
2. `router-generator/src/filesystem/virtual/loadConfigFile.ts:6` — `createJiti(filePath, {
   interopDefault: false, tsconfigPaths: true })`. This is jiti loading a **virtual route config
   file** (the `virtualRouteConfig` feature). It affects how that one config module is loaded,
   not how application imports resolve.

`[AUTHORITATIVE: node_modules/@tanstack/start-plugin-core/src/vite/import-protection-plugin/plugin.ts:2184; node_modules/@tanstack/router-generator/src/filesystem/virtual/loadConfigFile.ts:6]`

The Start options schema confirms it from the other direction: there is **no alias, paths, or
tsconfig option anywhere** in `tanstackStartOptionsObjectSchema`. The full surface is
`srcDirectory`, `start`, `router`, `client`, `server`, `serverFns`, `pages`, `sitemap`,
`prerender`, `dev`, `spa`, `importProtection`.
`[AUTHORITATIVE: node_modules/@tanstack/start-plugin-core/src/schema.ts:206-299]`

Resolution is therefore 100% the bundler's job. Start is a consumer of Vite's resolver, not a
participant in it.

### Does route codegen emit relative or aliased imports? Is it configurable?

**Relative by default. Configurable to filesystem-absolute — never to aliased.**

The generator builds every route-tree import with `path.relative(path.dirname(generatedRouteTree),
path.resolve(routesDirectory, node.filePath))` and prefixes `./`:

```ts
// router-generator/src/utils.ts:1287-1300
export function getImportForRouteNode(node, config, generatedRouteTreePath, root) {
  let source = ''
  if (config.importRoutesUsingAbsolutePaths) {
    source = replaceBackslash(removeExt(
      path.resolve(root, config.routesDirectory, node.filePath), config.addExtensions))
  } else {
    source = `./${getImportPath(node, config, generatedRouteTreePath)}`
  }
  ...
}
```

`[AUTHORITATIVE: node_modules/@tanstack/router-generator/src/utils.ts:1287-1300, and the lazy/component import sites at generator.ts:714-716, 743-753, 776-786, 838-845]`

The single knob is `importRoutesUsingAbsolutePaths`, default `false`.
`[AUTHORITATIVE: node_modules/@tanstack/router-generator/src/config.ts:81]` Note that "absolute"
here means **absolute filesystem path**, not "alias" — `path.resolve(root, ...)`. There is no
option that makes codegen emit `@/routes/...`.

It is reachable from Start config: `schema.ts:6-8` builds `tsrConfig` as the generator's full
`configSchema` minus `autoCodeSplitting`/`target`, and splices it into the `router` option, so
`tanstackStart({ router: { importRoutesUsingAbsolutePaths: true } })` type-checks. Start's own
internal client-tree plugin has the option **present but commented out**:

```ts
// start-plugin-core/src/vite/start-router-plugin/plugin.ts:131-138
const buildResult = generatorInstance.buildRouteTree({
  ...crawlingResult, acc,
  config: {
    // importRoutesUsingAbsolutePaths: true,
    // addExtensions: true,
    disableTypes: true,
    ...
```

`[AUTHORITATIVE: node_modules/@tanstack/start-plugin-core/src/vite/start-router-plugin/plugin.ts:131-138]`

I would not touch this option. It is off by default, deliberately disabled inside Start, and
buys nothing an alias strategy needs.

---

## Q2 — Does route discovery / the generated tree interact with aliases?

**No. The two are orthogonal, including with a nested `srcDirectory`.**

Route *discovery* is a filesystem crawl rooted at `routesDirectory`; route *tree imports* are
relative paths computed by `path.relative` (Q1). Neither consults nor emits a module specifier
that an alias could affect. Aliases only ever appear in the **hand-written** code inside route
files, which Vite resolves like any other import.

The repo's existing comment in `apps/wallow-web/vite.config.ts` about `srcDirectory` is
**correct**. `parseStartConfig` resolves both route paths under `srcDirectory`:

```ts
// start-plugin-core/src/schema.ts:69-79
const routesDirectory = path.resolve(root, srcDirectory,
  rawRouterOptions.routesDirectory ?? 'routes')
const generatedRouteTree = path.resolve(root, srcDirectory,
  rawRouterOptions.generatedRouteTree ?? 'routeTree.gen.ts')
```

`[AUTHORITATIVE: node_modules/@tanstack/start-plugin-core/src/schema.ts:67-79]` So
`routesDirectory: "src/app/routes"` alongside `srcDirectory: "src/app"` would indeed resolve to
`src/app/src/app/routes` — the repo's stated reason for using `srcDirectory` as the single knob
holds.

**Verified end-to-end against Wallow's exact shape.** I restructured the scaffolded app to
`srcDirectory: 'src/app'` with `importProtection: { include: ['src/**'] }`, moved routes to
`src/app/routes/`, rewrote their imports to `@/components/...` and `@/app/styles.css?url`, and
kept `src/shared/marker.ts` **outside** the srcDirectory, imported via alias. Result: build
succeeded; the sentinel landed in both `.output/server/_ssr/routes-DNg4SjU5.mjs` and
`.output/public/assets/routes-DnQ9zpz3.js`; and `src/app/routeTree.gen.ts` still emitted plain
relative imports (`import { Route as rootRouteImport } from './routes/__root'`).
`[EXPERIMENT: scaffolded app, srcDirectory 'src/app', vite 8.2.0 + nitro 3 beta]`

**No known breakage found.** I searched TanStack/router for issues on this and found none; I am
reporting absence of evidence, not evidence of absence.

**Maintainer guidance on repo layout: I could not find any.** See Q5.

---

## Q3 — Nitro's resolution layer

**Under `nitro/vite`, Nitro does NOT resolve independently. There is one Vite resolver, and
Nitro *contributes* aliases to it rather than maintaining a competing set. A Vite-only alias
solution cannot silently break the production server bundle.**

This was the sharpest risk in the brief, so I answered it twice — from source and by experiment.

### From source

Nitro has two distinct code paths, and only one of them is a separate resolver:

**(a) Standalone builder (`nitro build`, no Vite).** Here Nitro installs its own rollup/rolldown
alias plugin:

```js
// nitro/dist/vite.mjs:51-53
const alias = (await import("./_libs/plugin-alias.mjs").then((n) => n.dist_exports)).default;
const rollupConfig = defu({
  plugins: [inject(base.env.inject), alias({ entries: base.aliases })], ...
```

**(b) The Vite plugin path this repo uses.** Here Nitro's `config` hook *returns* its aliases as
ordinary Vite config, which Vite merges with the user's:

```js
// nitro/dist/vite.mjs:313-325 (plugin "nitro:main")
async config(userConfig, _configEnv) {
  return {
    appType: userConfig.appType || "custom",
    resolve: { alias: ctx.bundlerConfig.base.aliases },
    builder: { sharedConfigBuild: true },
    ...
```

`[AUTHORITATIVE: node_modules/nitro/dist/vite.mjs:51-53 and :313-325]`

A Vite plugin `config` hook return value is *merged into* the resolved config, not substituted
for it. So under `nitro/vite` the user's `resolve.alias` and Nitro's own aliases coexist in a
single `resolve.alias`, consumed by a single resolver. There is no second resolution pass over
the server bundle to fall out of sync with.

Nitro does have a documented top-level `alias` config option ("Path aliases for module
resolution") for the standalone builder.
`[AUTHORITATIVE: node_modules/nitro/dist/docs/2.config/0.index/index.md:925-940]` It is not
needed under `nitro/vite`.

### By experiment

I built the same app three ways and grepped the emitted Nitro server bundle for the sentinel:

| Alias mechanism in `vite.config.ts` | `.output/server/` | `.output/public/` |
| --- | --- | --- |
| `resolve: { tsconfigPaths: true }` | sentinel present | sentinel present |
| `resolve: { alias: [...] }` (Vite-only, manual, no tsconfig involvement) | sentinel present | sentinel present |
| `resolve: { tsconfigPaths: true }`, nested `srcDirectory` (Q2) | sentinel present | sentinel present |

In every case `grep -rn "['\"][@#]/shared/marker" .output/` found **nothing** — no unresolved
specifier leaked into either bundle.
`[EXPERIMENT: three full `npm run build` runs, nitro 3.0.x beta via nitro/vite]`

**Conclusion: the premise behind the question — that a Vite-only alias might silently break the
Nitro production server bundle — does not hold for `nitro/vite`.** It would be a real concern
only for the standalone `nitro build` path, which this repo does not use.

### Nitro's own docs endorse the Vite-native mechanism

Nitro ships its docs inside the package. Its import-alias example uses **Vite 8's native
option**, plus Node `imports` subpaths:

```ts [vite.config.ts]
export default defineConfig({ plugins: [nitro()], resolve: { tsconfigPaths: true } });
```

`[AUTHORITATIVE: node_modules/nitro/dist/docs/3.examples/11.import-alias.md]`

And Nitro ships a **TanStack Start + Nitro + Vite** example — this repo's exact stack — whose
prose reads: *"Use `viteTsConfigPaths()` to enable path aliases like `~/` from tsconfig."*
`[AUTHORITATIVE: node_modules/nitro/dist/docs/3.examples/28.vite-ssr-tss-react.md]` That page is
slightly behind (it uses the plugin, not the native option); its `tsconfig.json` uses
`"~/*": ["./src/*"]`. Treat the mechanism as confirmed and the *plugin* spelling as stale — see
Q4.

---

## Q4 — What does TanStack actually recommend / do?

**Unambiguous and uniform: `tsconfig.json` `paths` as the single source of truth, plus
`resolve: { tsconfigPaths: true }` in `vite.config.ts`. No `vite-tsconfig-paths`. No manual
`resolve.alias`. Ever, in any current official Start example.**

### The official examples

Every `examples/react/start-*` config I read is identical in mechanism:

```jsonc
// tsconfig.json — start-basic, start-large, start-bare, start-tailwind-v4
"paths": { "~/*": ["./src/*"] }
```

```ts
// vite.config.ts — same four
export default defineConfig({
  server: { port: 3000 },
  resolve: { tsconfigPaths: true },
  plugins: [tailwindcss(), tanstackStart({ srcDirectory: 'src' }), viteReact(), nitro()],
})
```

`[AUTHORITATIVE: github.com/TanStack/router/blob/main/examples/react/{start-basic,start-large,start-bare,start-tailwind-v4}/{tsconfig.json,vite.config.ts}]`

Note `start-basic` is `tanstackStart({ srcDirectory: 'src' }) + nitro()` — structurally the same
shape as `apps/wallow-web`.

Route files really do use the alias, including for asset URLs:

```tsx
// examples/react/start-basic/src/routes/__root.tsx
import { DefaultCatchBoundary } from '~/components/DefaultCatchBoundary'
import appCss from '~/styles/app.css?url'
import { seo } from '~/utils/seo'
```

`[AUTHORITATIVE: examples/react/start-basic/src/routes/__root.tsx]`

And `routeTree.gen.ts` in those same examples uses relative imports (`from './routes/__root'`),
independently confirming the Q1 codegen finding from the shipped artifact rather than the source.
`[AUTHORITATIVE: examples/react/start-large/src/routeTree.gen.ts]`

### Scale of adoption inside TanStack/router

- `tsconfigPaths` appears in **109 files** in TanStack/router — all `examples/**` and `e2e/**`
  Start fixtures across react, solid, and vue, plus the docs page.
- `vite-tsconfig-paths` appears in **3 files**, and **zero of them are config files**:
  `docs/.../path-aliases.md` (the "Vite 7 and earlier" fallback section),
  `docs/.../tailwind-integration.md`, and the import-protection plugin comment from Q1.

`[AUTHORITATIVE: GitHub code search via authenticated gh CLI, repo:TanStack/router, 2026-07-30]`

That is a completed migration, not a split ecosystem.

### Vite itself actively pushes you off the plugin

Vite 8 detects the plugin and warns:

```js
// vite/dist/node/chunks/node.js:35599-35600
const tsconfigPathsPlugin = userPlugins.find((p) =>
  p.name === "vite-tsconfig-paths" || p.name === "vite-plugin-tsconfig-paths");
if (tsconfigPathsPlugin) logger.warnOnce(yellow(
  `The plugin ${JSON.stringify(tsconfigPathsPlugin.name)} is detected. Vite now supports tsconfig
   paths resolution natively via the resolve.tsconfigPaths option. You can remove the plugin and
   set resolve.tsconfigPaths: true in your Vite config instead.`));
```

`[AUTHORITATIVE: node_modules/vite/dist/node/chunks/node.js:35599-35600]`

The option is `boolean`, **default `false`**, marked `@experimental`, and lives on `ResolveOptions`
(top-level `resolve`) rather than `EnvironmentResolveOptions` — so it is one setting shared by all
environments, not a per-environment knob.
`[AUTHORITATIVE: node_modules/vite/dist/node/index.d.ts:1991-2003]` It is threaded straight into
the resolver used by every environment via `perEnvironmentOrWorkerPlugin`.
`[AUTHORITATIVE: node_modules/vite/dist/node/chunks/node.js:32031-32061]`

Official Vite docs: *"Enables the tsconfig paths resolution feature. `paths` option in
`tsconfig.json` will be used to resolve imports."*
`[AUTHORITATIVE: https://vite.dev/config/shared-options#resolve-tsconfigpaths]`

### What the current scaffolder emits

I ran `create-start-app@latest` (**0.59.41**) rather than trusting recall. Verbatim output:

```jsonc
// tsconfig.json  — note: NO baseUrl
"paths": {
  "#/*": ["./src/*"],
  "@/*": ["./src/*"]
}
```

```ts
// vite.config.ts
const config = defineConfig({
  resolve: { tsconfigPaths: true },
  plugins: [devtools(), nitro({ ... }), tailwindcss(), tanstackStart(), viteReact()],
})
```

`[EXPERIMENT / AUTHORITATIVE: create-start-app 0.59.41 output, run 2026-07-30]`

Two useful details. First, the scaffolder emits **two** alias spellings, `@/` and `#/`, both
mapping to `./src/*` (`#/` matching Node's `imports` subpath convention). Second, **no `baseUrl`** —
consistent with `moduleResolution: "bundler"`, and matching what `apps/wallow-web/tsconfig.json`
already does.

### Spelling: be careful, it is NOT standardized

| Source | Spelling |
| --- | --- |
| Start docs + all official `start-*` examples + Nitro's Start example | `~/*` |
| `create-start-app` 0.59.41 (current scaffolder) | `@/*` **and** `#/*` |
| `@tanstack/cta-templates-react-cra` (Router-only, non-Start) | `@/*`, with a **manual** `resolve.alias` |

The **mechanism** is universal; the **prefix** is not. I would not spend any effort matching
upstream's prefix. (The CRA template is also the one place I found a manual `resolve.alias` — but
it self-excludes when Start is enabled via `<% if (addOnEnabled.start) { ignoreFile() } %>`, and
that whole templates package is Vinxi-era `app.config.ts` vintage, so it is not evidence about
current Start.)
`[AUTHORITATIVE: @tanstack/cta-templates-react-cra@0.10.0-alpha.18, project/base/{tsconfig.json.ejs,vite.config.js.ejs}]`

### Honest limit on this answer

**The official examples do not demonstrate a scaling pattern, and I want to be explicit about
that.** Every one of them declares exactly **one** alias pointing at `./src/*`. `start-large` is
"large" in the sense of *many routes* — it is a codegen and type-performance stress test whose
`src/` is flat (`routes/`, `router.tsx`, `styles.css`, `typePrimitives.tsx`, `createRoutes.mjs`).
It is **not** a feature-sliced architecture demo.
`[AUTHORITATIVE: examples/react/start-large/src/ listing]`

So: upstream authoritatively answers *"how should aliases be wired?"* It does **not**
authoritatively answer *"should a large app have three zone aliases or one?"* Wallow's
three-alias `@app`/`@features`/`@shared` design is neither endorsed nor contradicted by upstream —
it is simply outside what upstream demonstrates. Anyone claiming TanStack endorses (or forbids)
multi-zone aliases is overreading the examples.

---

## Q5 — Feature-sliced / zone layouts, and how `importProtection` matches

### Official layout guidance for large apps

**I could not find any.** I searched the Start and Router docs and the TanStack/router repo. What
exists is file-based-routing mechanics — flat vs. directory routes, route-file colocation,
`routeFileIgnorePattern`, encapsulating a route's files into a directory — all of which concern
the *routes* tree only, and say nothing about organising non-route application code into feature
or zone folders.
`[AUTHORITATIVE: https://tanstack.com/router/latest/docs/routing/file-based-routing]`

I found **no maintainer statement** on feature-folder or zone layout. Rather than fill this gap
with plausible-sounding recall, I am reporting it as a genuine gap: **on repo layout for large
Start apps, upstream is silent.** Wallow's zone design has to be justified on its own merits.

### How `importProtection` matches — precisely

This is the part of the brief where the repo's existing comment needed verifying, and it is
**correct**. Two separate matching surfaces, and they behave differently:

**Importer scope — matched on the RESOLVED file path, relativized to `root`.**

```ts
// start-plugin-core/src/import-protection/adapterUtils.ts:74-90
const relativePath = relativizePath(normalizedImporter, config.root)
let result: boolean
if ((config.excludeMatchers.length > 0 && matchesAny(relativePath, config.excludeMatchers)) ||
    (config.ignoreImporterMatchers.length > 0 && matchesAny(relativePath, config.ignoreImporterMatchers))) {
  result = false
} else if (config.includeMatchers.length > 0) {
  result = !!matchesAny(relativePath, config.includeMatchers)
} else if (config.srcDirectory) {
  result = isInsideDirectory(normalizedImporter, config.srcDirectory)   // <-- the fallback
} else {
  result = true
}
```

`[AUTHORITATIVE: node_modules/@tanstack/start-plugin-core/src/import-protection/adapterUtils.ts:53-94]`

**The repo's claim is confirmed by the source.** When `include` is empty, the importer scope
falls back to `srcDirectory` itself (line 86-87). So narrowing `srcDirectory` to `src/app`
without an `include` would silently stop checking every importer under `src/features/**` and
`src/shared/**`. The `importProtection: { include: ["src/**"] }` in both apps' vite configs is
load-bearing, exactly as the comment states. (The comment cites the compiled
`adapterUtils.js:23`; the source location is `adapterUtils.ts:86-87`.)

Because this surface matches on the **resolved** path, aliases are completely transparent to it —
`include: ["src/**"]` works no matter how the importer was spelled.

**Denial rules — two surfaces, matched differently:**

- `specifiers` rules test the **raw, pre-resolution specifier string**:
  `const specifierMatch = matchesAny(source, matchers.specifiers)`
  `[AUTHORITATIVE: .../vite/import-protection-plugin/plugin.ts:1848]` — and this check runs
  *before* `this.resolve()` is called at line 1890ff.
- `files` rules test the **resolved path**, relativized to root:
  `const relativePath = getRelativePath(resolved)` then
  `matchers.files.find((matcher) => matcher.test(relativePath))`
  `[AUTHORITATIVE: .../vite/import-protection-plugin/plugin.ts:1915, 1936]`

**The alias-relevant consequence:** a `files` rule (e.g. the default `**/*.server.*`) is
alias-proof — it matches after Vite resolves, so `import x from "@shared/thing.server"` is caught
normally. A `specifiers` rule is **literal string matching on what you typed**. If you were to
write a rule blocking `"@shared/secrets"` it would match that exact spelling only, and a relative
import of the same file would slip past; conversely a rule blocking a bare package name like
`"redis"` matches the bare specifier and is unaffected by any alias config. Keep specifier rules
for bare package names and rely on `files` rules for path-based boundaries.

Resolution itself goes through `this.resolve(source, importer, resolveOptions)`
`[AUTHORITATIVE: plugin.ts:1712]` — i.e. the full Vite resolver pipeline including
`resolve.alias` and `resolve.tsconfigPaths`. Import protection inherits whatever alias mechanism
you configure; it never needs to know about it.

One thing worth flagging while I was in here: the **default** rules are narrower than one might
assume. `client.specifiers` is only the three `@tanstack/{react,solid,vue}-start/server` entries,
and `server.specifiers` is **empty**; the real work is done by the file conventions
`client.files: ['**/*.server.*']` and `server.files: ['**/*.client.*']`, both excluding
`**/node_modules/**`.
`[AUTHORITATIVE: node_modules/@tanstack/start-plugin-core/src/import-protection/defaults.ts:17-34]`
So a bare `redis` import is stopped by import protection **only** if the importing file follows
the `.server.` naming convention or a custom rule names it. Worth a look by whoever audits the
apps' actual configs — it is adjacent to, but not part of, the alias question.

---

## Q6 — Vitest + Start: can these apps have ONE config file?

**No — not without losing the two-project split. But the duplication can still be cut from three
declarations to two, and that is the win worth taking.**

### What Vitest documents

> "If you are using Vite and have a `vite.config` file, Vitest will read it [...] Create
> `vitest.config.ts`, which will have the higher priority and will **override** the configuration
> from `vite.config.ts` — it means all options in your `vite.config` will be **ignored**."

`[AUTHORITATIVE: https://vitest.dev/config/]`

The documented ways to share resolution between the two are: `mergeConfig(viteConfig, defineConfig({...}))`
in `vitest.config.ts`; branching inside `vite.config.ts` on `process.env.VITEST` / `mode`; or
`--config`. Same page.

### What actually blocks a single config here

Wallow's `vitest.config.ts` is not a thin override. It carries `test.projects` with a node/browser
split from `createVitestProjects`, per-project `optimizeDeps` pre-bundling, `ssr.noExternal` for
the linked workspace packages, and a browser-only `node:async_hooks` shim aliased in for the
browser project only. Folding that into `vite.config.ts` would mean every `vite dev` and every
production build parsing and carrying test-only configuration, and the `node:async_hooks` shim
alias would have to be conditionalised so it never reaches a real build. `mergeConfig` is the
sanctioned way to get `vite.config.ts`'s resolution *into* `vitest.config.ts`, but the Start
plugin, `nitro()`, and `wallowStyles()` would come along with it — which is not obviously
desirable in a test run.

**My recommendation: keep two config files.** One config is not the prize here. Eliminating
`aliases.ts` and the hand-mirrored `paths` block is.

### The important, non-obvious finding

`resolve.tsconfigPaths` set at the **root** of `vitest.config.ts` is **NOT inherited by
`test.projects` entries.** I verified both directions:

| `vitest.config.ts` shape | Result |
| --- | --- |
| root `resolve: { tsconfigPaths: true }`, **no** `projects` | **passes** |
| root `resolve: { tsconfigPaths: true }`, **with** `projects` | **fails** — `Error: Cannot find package '@/shared/marker' imported from src/alias.test.ts` |
| `resolve: { tsconfigPaths: true }` **inside** the project entry | **passes** |

`[EXPERIMENT: vitest 4.1.10 + vite 8.2.0, three runs against the scaffolded app]`

This mirrors Vitest's general rule that projects do not inherit root config — and it means the
migration shape for Wallow is a drop-in substitution at exactly the place the aliases already
live. Each project entry currently reads `{ ...node, resolve: { alias: resolveAlias } }`; it
would become `{ ...node, resolve: { tsconfigPaths: true } }`, and the browser project keeps its
one genuine alias:

```ts
{ ...browser, resolve: { tsconfigPaths: true, alias: { "node:async_hooks": nodeAsyncHooksShim } } }
```

### Version compatibility — checked, not assumed

`vitest@4.1.10` declares `vite: "^6.0.0 || ^7.0.0 || ^8.0.0"` (both `dependencies` and
`peerDependencies`), and in this workspace its `node_modules/vite` symlinks to the single store
copy, **`vite@8.1.4`**. So Vitest here runs on a Vite that has the native option.
`[AUTHORITATIVE: node_modules/.pnpm/vitest@4.1.10_.../node_modules/vitest/package.json and its vite symlink]`
This is worth stating explicitly because on a Vite 6/7 workspace the same Vitest would silently
lack `resolve.tsconfigPaths`.

---

## Practical implications for Wallow

Stated as findings, not as a decision — the alias strategy is the coordinator's call.

1. **Three declarations can become one.** `tsconfig.json` `paths` is the single source of truth;
   `resolve: { tsconfigPaths: true }` in `vite.config.ts` and in **each vitest project entry**
   makes both toolchains read it. `aliases.ts` and `src/alias-map.test.ts` (the guard that pins
   tsconfig to the map) both become unnecessary — the drift they exist to prevent stops being
   possible. This is exactly what every official Start example does.

2. **Keep `paths` in each app's own `tsconfig.json`. Do not hoist them to `tsconfig.base.json`.**
   I tested this specifically because Wallow's tsconfigs use `extends`. Relative `paths` entries
   resolve relative to *the config file that contains them*. With `paths` in a base config **one
   directory up**, the build **fails**:
   `Error: [vite]: Rolldown failed to resolve import "@/shared/marker" from ".../src/routes/index.tsx?tsr-split=component"`.
   With `paths` in a base config in the **same** directory, or in the app's own tsconfig, it
   works. `[EXPERIMENT: three build runs varying tsconfig extends topology]` The good news is
   that this fails **loudly at build time**, not silently at runtime.

3. **The `use-sync-external-store` regex aliases must stay in `resolve.alias`.** They are not
   tsconfig paths — they are runtime module substitutions with anchored regexes, and
   `tsconfigPaths` cannot express them. `resolve.alias` and `resolve.tsconfigPaths` coexist fine.
   Same for the browser project's `node:async_hooks` shim.

4. **Nitro is not a risk here.** Under `nitro/vite` there is one resolver; verified by building
   and grepping the emitted server bundle three ways (Q3). Whatever alias mechanism is chosen,
   the production Nitro bundle sees it.

5. **`importProtection: { include: ["src/**"] }` must stay** if `srcDirectory` stays narrowed —
   confirmed against `adapterUtils.ts:86-87` (Q5). It matches on resolved paths, so it is
   unaffected by any alias change.

6. **Cost to weigh:** Vite's own docs note `resolve.tsconfigPaths` "has a performance cost" and
   link TypeScript's caution against using `paths` to steer external tools. The option is also
   still marked `@experimental` in Vite 8.1.4's types. Given that every official TanStack Start
   example ships it, I read the experimental marker as API-stability hedging rather than a
   reliability warning — but it is the one honest argument for keeping explicit `resolve.alias`.
   `[AUTHORITATIVE: https://vite.dev/guide/features#paths; node_modules/vite/dist/node/index.d.ts:1997-2003]`

## Gaps — stated rather than filled

- **No official/maintainer guidance exists on feature-folder or zone layout for large Start apps.**
  Searched docs and repo; found routing mechanics only. Wallow's three-zone design is not
  supported *or* opposed by upstream.
- **No maintainer issue/PR quotes.** GitHub's code-search API is authenticated-only here and I
  used `gh` for code search successfully, but I did not locate a maintainer statement on aliases
  or layout worth quoting. I would rather report this than paraphrase something I did not read.
- **`start-large` is not an architecture reference** — it is a route-count/type-perf fixture. Do
  not cite it as evidence about how to structure a large codebase.
- Nitro's shipped Start example (`28.vite-ssr-tss-react.md`) still recommends the
  `vite-tsconfig-paths` **plugin**. It is stale relative to TanStack's own docs and examples and
  to Vite 8's deprecation warning. Mechanism confirmed; plugin spelling outdated.
