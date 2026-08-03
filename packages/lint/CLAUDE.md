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

## Where this is registered

The plugin is registered in **six configs**: the repo-root `.oxlintrc.json` plus five nested
configs — `apps/wallow-web`, `apps/wallow-auth`, `packages/ui`, `packages/forms` and
`packages/navigation`:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
```

**The root registers AND enables exactly one rule repo-wide: `wallow/no-source-tests`** (it is
option-free and self-gates on `*.test.*` filenames, so one repo-wide `"error"` is correct; its
doctrine-blessed exemptions are a single override block in the root config, each file with its
reason). The other five `wallow/*` rules stay **enabled per-tree by the nested configs**, because
their options genuinely differ between trees (`text-heading-variant`) or are inert without
per-app inputs (`zone-dag`), and an oxlint override REPLACES options rather than merging. The
nested configs keep their own `jsPlugins` registration too — redundant with the root's but
harmless, and removing it is a separate soak-tested decision (there is a bead).

Every config that registers the plugin names `@bc-solutions-coder/lint` as a `workspace:*`
devDependency — including the **root** `package.json` — because a `jsPlugins` specifier is
documented to resolve from the config file's own directory, and a config that registers without
depending cannot rely on loading.

### Why root registration is possible (it used to be off-limits)

`packages/sdk/src/oxlint-guardrails.test.ts` proves the root config's import bans by **copying
it** and running the real binary over a mirror tree. That mirror used to live in `os.tmpdir()`,
where no `node_modules` is reachable, and a root `jsPlugins` entry made the copy unloadable —
which is why the plugin was long registered only from the nested configs.

Two things changed, measured on oxlint 1.74.0 under pnpm:

- The mirror now lives **inside the repo**, under gitignored `.lint-mirror/` (also in the root
  config's `ignorePatterns`), so the copied config resolves the bare specifier through the
  workspace `node_modules` by ordinary walk-up. Do not move it back to a temp directory — the
  spec's own comments say why.
- As installed, oxlint also probes its **own install location**, which reaches pnpm's hidden
  hoist store (`node_modules/.pnpm/node_modules`), so even a temp-dir copy loads today. That is
  an implementation detail of the installed toolchain, NOT the documented config-relative
  resolution — the in-repo mirror and the root devDependency are what this repo relies on.

**This constrains `jsPlugins` only, not `plugins`.** The root config's `plugins` array names
oxlint's **built-in Rust** plugins (`typescript`, `unicorn`, `oxc`, `react`, `import`) — they are
compiled into the binary and resolve nothing off disk.

**A config that does not register the plugin lints its subtree with every `wallow/*` rule
vacuously passing, and nothing fails.** That is how the three package configs sat unprotected
after the shell extraction moved the code these rules police out of `apps/wallow-web` and into
`packages/navigation`: `pnpm lint`'s roots are `apps packages`, so their files WERE scanned, with
the rules unloaded. Adding a nested config for a directory that renders UI means adding the
`jsPlugins` entry and the devDependency with it.

The reasons behind each registration and each scoped exemption are written as `//` comments in
the config beside the entry — oxlint parses its config as **JSONC**, and since root registration
the ROOT config carries comments too. One spec reads these configs back —
`packages/sdk/src/oxlint-guardrails.test.ts`, which parses the root config and walks every
nested config asserting each one `extends` the root and restates no `categories`/`plugins` — and
it strips line comments before `JSON.parse`, for the root exactly as for the nested ones. (`packages/forms/src/core/package-scaffold.test.ts` was a second reader; it
is gone with the source-reading guards.) A future reader must strip them the same way.

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

| Config                | Enables                                                                                                  |
| --------------------- | -------------------------------------------------------------------------------------------------------- |
| `apps/wallow-web`     | `no-sidebar-inversion`, `no-source-tests`, `no-tinted-text`, `zone-dag`                                  |
| `apps/wallow-auth`    | `no-hand-rolled-mutation`, `no-sidebar-inversion`, `no-source-tests`, `text-heading-variant`, `zone-dag` |
| `packages/navigation` | all six                                                                                                  |
| `packages/ui`         | all six, minus the drawer indent recipe (see below)                                                      |
| `packages/forms`      | all six, minus `use-app-form.ts` (see below)                                                             |

**`no-source-tests` is enabled at the ROOT for the whole repo** (the five nested enablements are
now redundant restatements, kept until the registration-cleanup bead lands), and exempted only by
the root's doctrine block. It is the only rule here that applies exclusively to `*.test.*` files —
it bans `node:fs` in a spec, and self-gates on `context.filename` — so it is the one `wallow/*`
entry that must **not** appear in a config's `*.test.*` override block. Every config carries a comment saying
so beside `zone-dag`'s, because the two are silent for opposite reasons and both look omittable.
The doctrine it enforces is `.claude/rules/TESTING.md`'s: a spec asserts behaviour, and a
constraint on how code is written is either a rule or (more often) nothing at all.

The three packages take **all six** because none of them has a reason to opt out of a rule
wholesale — `zone-dag` is inert there (it derives its zones from a `tsconfig.json` `paths` map and
a package declares none), and the rest apply to any file that renders or any spec that runs. Where
a package has a genuine counter-example it is a **scoped override naming the one file**, so
everything around it stays judged:

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
drawer scrim, because a translucent surface is categorically not tinted text. Where a rule gates on
the FILENAME, the valid side needs a file of the other kind — `no-source-tests`' fixture directory
carries a plain `valid.ts` that imports `node:fs` and must draw nothing, which is the only thing
that proves the gate exists.

## The rule-vs-test boundary

> **A rule sees one JS/TS file at a time, and only files oxlint lints.**

That is the whole test, and it decides one thing only: whether a constraint **can** be a rule.
Anything that fails it — a relationship _between_ files, an **absence** (a missing barrel has no
file to attach a diagnostic to), a non-JS input (Dockerfiles, markdown, the lint config itself) —
does **not** thereby become a vitest spec. It becomes a question: is the constraint worth keeping
at all? Under `.claude/rules/TESTING.md` the answer is usually no, and the sweep is deleted rather
than relocated. Seventy-seven source-reading specs went that way in one pass (`Wallow-xg9t.1`);
what replaced most of them is nothing, because a real regression in what they policed fails a
build, `pnpm check:exports`, or an `e2e/` run without their help.

What genuinely stays a spec is behaviour a rule cannot see: a computed style that only exists at
runtime, and runtime/compile-time identity. **`wallow/no-source-tests` now enforces the rest** — a
spec cannot import `node:fs` under any of the five configured trees, so "convert it or delete it"
is no longer advice.

Type-awareness is not the boundary — oxlint has none, but nothing in this repo is blocked by that.

**The corollary that bites:** `ignorePatterns` (`dist`, `generated`, `routeTree.gen.ts`, `.output`)
makes a rule **silent** where a disk sweep is **loud**. When converting a sweep, confirm the paths
it covered are paths oxlint actually lints — the difference between "exempt" and "silently
unchecked" is invisible in a green run. This cuts the other way too, and it is why the doctrine
above prefers deletion: a sweep that reads files oxlint never lints was reporting on code no rule
can police, which is a reason to ask what it was worth, not a reason to keep it.
