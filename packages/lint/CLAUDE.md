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

Rules are registered in **five nested configs**, each of which is also the config that switches
them on — `apps/wallow-web`, `apps/wallow-auth`, `packages/ui`, `packages/forms` and
`packages/navigation`:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
```

Each of the five names `@bc-solutions-coder/lint` as a `workspace:*` devDependency, because a
`jsPlugins` specifier resolves from the config file's own directory (see below) — a package that
registers the plugin without depending on it cannot load it.

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

**A config that does not register the plugin lints its subtree with every `wallow/*` rule
vacuously passing, and nothing fails.** That is how the three package configs sat unprotected
after the shell extraction moved the code these rules police out of `apps/wallow-web` and into
`packages/navigation`: `pnpm lint`'s roots are `apps packages`, so their files WERE scanned, with
the rules unloaded. Adding a nested config for a directory that renders UI means adding the
`jsPlugins` entry and the devDependency with it.

The reasons behind each registration and each scoped exemption are written as `//` comments in
the config beside the entry — oxlint parses its config as **JSONC**. Two specs read a nested
config back (`packages/sdk/src/oxlint-guardrails.test.ts`,
`packages/forms/src/core/package-scaffold.test.ts`) and both strip line comments before
`JSON.parse`; a third reader must do the same.

### The cost of nesting

Because oxlint matches an override's globs **and** `ignorePatterns` relative to the config's own
directory, each nested config has to **restate** the root's `apps/<app>/**` override block and the
root's `ignorePatterns` — their repo-rooted prefixes match nothing once matching starts at
`apps/<app>/`. A future edit to a root app block therefore silently stops applying to that app.
**Do not delete the restatement.**

## The root config around it

**Two passes cover the tree exactly once.** `pnpm lint` takes source, excluding `**/*.test.*` and
`**/*.stories.tsx`; `pnpm lint:tests` takes exactly those with oxlint's **vitest plugin** on. Lint by
hand after touching a rule's own specs and you need both. The test pass enumerates its file list
rather than globbing — oxlint expands no globs in path arguments and `ignorePatterns` has no `!`
negation, so a wrong list lints **zero** files and exits 0, which is why `scripts/lint-tests.sh`
prints its count and fails on zero. Neither pass may pass `-c`: an explicit config file disables
nested-config lookup, dropping the app configs that register this plugin along with `packages/ui`'s
and `packages/forms`' test relaxations.

**`import/no-cycle` is named explicitly** in the root config because it belongs to no category and is
off by default. Enabling the built-in `import` plugin for it also switches on its category rules, and
~7,500 of those diagnostics land on things this repo does deliberately: named-export-only
(`no-named-export`, `prefer-default-export`, `group-exports`, `exports-last`), `node:crypto` in the
SDK's server entry (`no-nodejs-modules`), and the namespace imports the seam specs use to assert
absence (`no-namespace`, `namespace`). Both passes run `--deny-warnings`, so all seven are switched
**off** by name beside `no-cycle`. Do not switch them on to tidy the config; do not delete them to
shorten it.

## Who enables what

| Config                | Enables                                                                               |
| --------------------- | ------------------------------------------------------------------------------------- |
| `apps/wallow-web`     | `no-sidebar-inversion`, `no-tinted-text`, `zone-dag`                                  |
| `apps/wallow-auth`    | `no-hand-rolled-mutation`, `no-sidebar-inversion`, `text-heading-variant`, `zone-dag` |
| `packages/navigation` | all five                                                                              |
| `packages/ui`         | all five, minus the drawer indent recipe (see below)                                  |
| `packages/forms`      | all five, minus `use-app-form.ts` (see below)                                         |

The three packages take **all five** because none of them has a reason to opt out of a rule
wholesale — `zone-dag` is inert there (it derives its zones from a `tsconfig.json` `paths` map and
a package declares none), and the other four apply to any file that renders. Where a package has a
genuine counter-example it is a **scoped override naming the one file**, so everything around it
stays judged:

- **`packages/ui/src/components/drawer/drawer.styles.ts`** relaxes `no-sidebar-inversion`.
  `drawerIndentBackgroundRecipe` fades a bare `bg-foreground` in behind the shrinking app UI
  (`opacity-0` → `data-[active]:opacity-100`), and a fixed alpha baked into the utility is not
  something a transition can animate from. It is the one bare `bg-foreground` in the repo that is
  not the retired inversion.
- **`packages/forms/src/form/use-app-form.ts`** relaxes `no-hand-rolled-mutation`. Its `mutationFn`
  states no request — it is the stand-in for the no-mutation escape hatch, there so exactly one
  `useMutation` runs on every path.

`packages/ui`'s `text-heading-variant` names `h2: subheading` but no `h1` entry, unlike
wallow-auth's `h1: false`: `PageHeader` is where the page's one level-1 heading legitimately
lives. Every level still has to NAME a variant.

`no-tinted-text` reaches `packages/ui` deliberately, and it found the `link` button variant's
`hover:text-primary/80`. That was **fixed, not exempted** — `link`'s hover is now the underline
alone. A recipe is the one place a colour decision gets written down, which is what makes a
fork-unreachable colour matter more there than at a call site.

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
