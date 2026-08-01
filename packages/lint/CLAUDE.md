# packages/lint — @bc-solutions-coder/lint Agent Guide

Wallow's own oxlint JS-plugin rules: the ones with no native equivalent. oxlint has no rule that
reads Tailwind class strings, and `no-restricted-syntax` (which an ESLint config would reach for)
is **not implemented** — naming it makes oxlint refuse to parse the whole config. A JS plugin is
the only mechanism that can judge a class name.

The package is private today but shaped to publish: `exports` points at `src/` in-repo, and
`publishConfig.exports` swaps to `dist/` on publish (the `packages/query` pattern). Every rule is
written with `createOnce` and the plugin is wrapped in `eslintCompatPlugin`, so it runs under
ESLint 8+ as well as oxlint. `@oxlint/plugins` is therefore a **runtime `dependencies` entry**,
not a devDependency — a consumer installing this package needs `definePlugin`/`defineRule` at
plugin-load time.

## Where this is registered — the load-bearing constraint

Rules are registered in the **nested app configs only** — `apps/wallow-web/.oxlintrc.json` and
`apps/wallow-auth/.oxlintrc.json`, the same configs that switch them on:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
```

**It is not registered from the repo-root `.oxlintrc.json`, and it must not be moved there.**

### Why the root config is off-limits

`packages/sdk/src/oxlint-guardrails.test.ts` proves the root config's import bans by **copying it
to a temp directory** and running the real binary there. Any `jsPlugins` entry makes that copy
unloadable: oxlint answers `Failed to parse oxlint configuration file`, the spec's `JSON.parse`
throws on it, and the whole file fails to collect — **0 tests run**, an unrelated suite silently
down. Both root placements were tried and both fail this way: a top-level `jsPlugins`, and a
`jsPlugins` scoped to the root's `apps/wallow-web` override block (the schema does permit it
there).

**No specifier form rescues that** — measured on oxlint 1.74.0, not assumed. A `jsPlugins`
specifier resolves from the **config file's own directory**, whichever form it takes:

- **Relative**: the temp-dir copy resolves the path against `<tmp>/` rather than the repo and
  reports `Cannot find module`.
- **Bare**: `jsPlugins: ["oxfmt"]` resolves from a config at the repo root — where
  `node_modules/oxfmt` is reachable — and reports `Cannot find module 'oxfmt'` from a config in a
  temp directory, with the cwd at the repo root either way. (`["oxlint"]` resolves from anywhere,
  but only because oxlint can reach itself.)

So the temp copy cannot load **any** form — relative or bare, loose file or workspace package: it
resolves from a directory with no `node_modules` in it at all. Keeping the entry in the nested
configs keeps it out of the file that gets copied.

**This constrains `jsPlugins` only, not `plugins`.** The root config's `plugins` array names
oxlint's **built-in Rust** plugins (`typescript`, `unicorn`, `oxc`, `react`, `import`) — they are
compiled into the binary and resolve nothing off disk, so the temp-dir copy loads them fine. Only
`jsPlugins`, whose entries are real module specifiers, breaks that copy.

The nesting is load-bearing a second time: `packages/ui` legitimately paints an animated backdrop
with a bare `bg-foreground`, so `no-sidebar-inversion` must never reach the catalog. Living under
`apps/<app>/` makes that structural rather than a per-glob exemption that can rot.

### The cost of nesting

Because oxlint matches an override's globs **and** `ignorePatterns` relative to the config's own
directory, each nested config has to **restate** the root's `apps/<app>/**` override block and the
root's `ignorePatterns` — their repo-rooted prefixes match nothing once matching starts at
`apps/<app>/`. A future edit to a root app block therefore silently stops applying to that app.
**Do not delete the restatement.**

## Both apps are gated, not identically

`apps/wallow-auth/.oxlintrc.json` carries the same rules plus `button` on the `react/forbid-elements`
list and the `text-heading-variant` levels. Do not converge the two:

- wallow-web's `bff-demo` route deliberately keeps four raw `<button>`s as the un-catalogued
  control of the BFF demo, so the button ban cannot be lifted to both.
- That same route deliberately takes `Text`'s **derived** scale (no `variant` anywhere), which is
  why `text-heading-variant` is wallow-auth's alone. wallow-web's headings are not one shape:
  `LandingPage` runs `display`/`title`/`h3`, the detail routes run `title`. Do not switch the rule
  on there without first deciding what those should be.

## Authoring a rule

- **`.ts` extensions on every relative import inside `src/` are mandatory** —
  `./rules/no-tinted-text.ts`, not `./rules/no-tinted-text`. oxlint loads the plugin as plain Node
  ESM, which rejects extensionless relative specifiers with `ERR_MODULE_NOT_FOUND`, while
  TypeScript typechecks either spelling clean. The failure only appears at lint time. Same trap
  `packages/config/CLAUDE.md` documents.
- **`createOnce` runs once per PROCESS, but `context.options` are per FILE.** Read options inside
  a visitor, never at `createOnce` setup time — `text-heading-variant` is configured differently by
  different override blocks, and hoisting the read freezes whichever file linted first. This is
  proven, not theoretical: two files under different override options draw different diagnostics
  from the same rule. Per-file mutable state belongs in a `before()` hook for the same reason.
- **`before()` is not guaranteed to run on every file.** oxlint intends to skip rules whose
  interesting node types are absent from a file. Code that must run for every file goes in a
  `Program` visitor.
- The root `.oxlintrc.json` relaxes `no-magic-numbers` to `ignore: [0, 1]` for
  `packages/lint/src/**/*.ts`. `pnpm lint` runs `--deny-warnings`, so without that entry ordinary
  index arithmetic fails the gate.

## Testing rules

`src/fixtures.test.ts` is **one generic spec that drives every rule** — there is no per-rule spec.
`fixtures/<rule>/` holds the cases; the harness runs the real oxlint binary once per fixture
directory and asserts the reported `(file, line, rule)` multiset equals the annotated one
**exactly**, so an unannotated diagnostic and an annotation nothing fired on both fail. See the
spec's own header for the measured oxlint behaviours it depends on.

`fixtures/**` is excluded from `pnpm lint` and `oxfmt` at the repo root — the files contain
deliberate violations, and formatting them would move `expect-error` annotations off their target
lines. `tsconfig.json` includes `src` only, for the same reason.

A valid fixture that merely omits violations proves nothing about a rule's boundary. Include the
cases the rule must **not** report: `no-tinted-text`'s `valid.tsx` carries `bg-foreground/40`, the
drawer scrim, because a translucent surface is categorically not tinted text.

## The rule-vs-test boundary

> **A rule sees one JS/TS file at a time, and only files oxlint lints.**

That is the whole test. Anything failing it stays a vitest spec: a relationship _between_ files, an
**absence** (a missing barrel has no file to attach a diagnostic to), a non-JS input (Dockerfiles,
markdown, the lint config itself), a computed style that only exists at runtime, or runtime/
compile-time identity.

Type-awareness is not the boundary — oxlint has none, but nothing in this repo is blocked by that.

**The corollary that bites:** `ignorePatterns` (`dist`, `generated`, `routeTree.gen.ts`, `.output`)
makes a rule **silent** where a disk sweep is **loud**. When converting a sweep, confirm the paths
it covered are paths oxlint actually lints — the difference between "exempt" and "silently
unchecked" is invisible in a green run.

Three further guard specs — `apps/*/src/server-only-naming.test.ts`, `feature-barrels.test.ts` and
wallow-web's `client-navigation.test.ts` — pass that test and are convertible, but have not been
converted. Each still carries a "why this is a spec and not an oxlint rule" header naming a reason
that is true of the **built-in** `no-restricted-imports` and false for a custom rule: `context.filename`
is an absolute path, and `createOnce` can read a `tsconfig.json` off disk once per process. Treat
those headers as stale, not as a decision.
