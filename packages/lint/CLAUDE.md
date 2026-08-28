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

**The census, canonically — this file owns it and the other docs defer here.** The repo has
**8 `.oxlintrc.json` files: 1 root + 7 nested.** Six of the seven nested configs enable `wallow/*`
rules — `apps/wallow-auth`, `apps/wallow-web`, `apps/minimal-app`, `packages/ui`,
`packages/forms`, `packages/navigation`. The seventh is `scripts/fork-smoke`'s, which enables none:
it exists to keep the fork-smoke scaffold out of the repo's own rule set, and is documented in
`scripts/fork-smoke/README.md`. Count these before quoting a number anywhere else.

The plugin is registered in **exactly one config**, the repo-root `.oxlintrc.json`:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
```

**A nested config inherits that registration through `extends`.** This was verified empirically on
oxlint 1.74.0, not assumed: with the nested `jsPlugins` entries and devDependencies deleted, a
probe file planted in each nested tree still produced byte-identical `wallow/*`
diagnostics — including option-carrying ones (`text-heading-variant` fired at each tree's own
configured `levels`), while a rule a given tree does not enable stayed silent there. So a
nested config needs **no** `jsPlugins` entry and **no** `@bc-solutions-coder/lint` devDependency;
only the root `package.json` names it, because a `jsPlugins` specifier resolves from the
REGISTERING config's own directory.

**The root registers the plugin and enables three rules repo-wide: `wallow/no-source-tests`,
`wallow/module-lists-in-sync` and `wallow/logger-no-node-builtins`.** All three are option-free
and self-gate — the first on `*.test.*` filenames, the second on a `vite.config.ts` filename plus
a `defineLibraryConfig` call, the third on the `packages/logger/src/` path (its server-graph
allowlist and spec exemption live inside the rule) — so one repo-wide `"error"` each is correct;
`no-source-tests`' doctrine-blessed exemptions are a single override block in the root config,
each file with its reason, while the other two need no exemptions because what they must not
judge (a list `module-lists-in-sync` cannot enumerate, the logger's `./server` graph) is skipped
by the rule itself, not by config. The other five `wallow/*` rules stay
**enabled per-tree by the nested configs**, because their options genuinely differ between trees
(`text-heading-variant`) or are inert without per-app inputs (`zone-dag`), and an oxlint
override REPLACES options rather than merging.

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

**A subtree the registration does not reach lints with every `wallow/*` rule vacuously passing,
and nothing fails.** That is how the three package configs sat unprotected after the shell
extraction moved the code these rules police out of `apps/wallow-web` and into
`packages/navigation`: `pnpm lint`'s roots are `apps packages`, so their files WERE scanned, with
the rules unloaded. A new nested config now inherits the root's registration, so adding one means
adding only the rule ENABLEMENTS — but it also means the `extends` line is load-bearing, and a
config that omits it (or a pass that passes `-c`, see below) loses the plugin silently. The check
is a one-minute probe, not a reading of the config: plant a file with a known violation in the
tree and confirm `pnpm lint` names it.

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
nested-config lookup, dropping every nested config's per-tree `wallow/*` enablement along with
`packages/ui`'s and `packages/forms`' test relaxations.

**`import/no-cycle` is named explicitly** in the root config because it belongs to no category and is
off by default. Enabling the built-in `import` plugin for it also switches on its category rules, and
~7,500 of those diagnostics land on things this repo does deliberately: named-export-only
(`no-named-export`, `prefer-default-export`, `group-exports`, `exports-last`), `node:crypto` in the
SDK's server entry (`no-nodejs-modules`), and the namespace imports the seam specs use to assert
absence (`no-namespace`, `namespace`). Both passes run `--deny-warnings`, so all seven are switched
**off** by name beside `no-cycle`. Do not switch them on to tidy the config; do not delete them to
shorten it.

## Who enables what

| Config                | Enables                                             |
| --------------------- | --------------------------------------------------- |
| `apps/wallow-web`     | all six                                             |
| `apps/wallow-auth`    | all six                                             |
| `apps/minimal-app`    | all six, minus `zone-dag` (see below)               |
| `packages/navigation` | all six                                             |
| `packages/ui`         | all six, minus the drawer indent recipe (see below) |
| `packages/forms`      | all six, minus `use-app-form.ts` (see below)        |

"All six" in the table means the six rules that predate `module-lists-in-sync` — that rule and
`wallow/logger-no-node-builtins` are enabled at the root only and appear in **no** nested config:
their self-gates make them inert outside a library's `vite.config.ts` and outside
`packages/logger/src/` respectively, so a nested entry would restate nothing (and
`packages/logger`, the one tree the logger rule judges, has no nested config at all).

**`no-source-tests` is enabled at the ROOT for the whole repo** and exempted only by the root's
doctrine block. The six nested enablements are redundant restatements of it, kept because each
sits beside a comment explaining why it must never appear in that config's `*.test.*` override
block. It is the only rule here that applies exclusively to `*.test.*` files —
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

## The three apps are gated, not identically

**Every `wallow/*` rule is enabled everywhere it can decide anything.** No config
blanket-disables one, and none may start: a rule switched off across a whole tree is a rule that
silently stops holding the standard it was written for. The divergences below are the complete
list, and each is a rule that would decide **nothing** in that tree or a rule that would be
**wrong** there — not a rule someone found inconvenient.

**`zone-dag`, absent in `apps/minimal-app`.** The rule derives the prefixes it polices from the
app's `tsconfig.json` `paths` map, and minimal-app declares none: it is deliberately un-zoned
(`apps/CLAUDE.md`), the smallest wiring of the shared packages, with no `@app`/`@features`/`@shared`
DAG to judge. Enabled, it would open every file and decide nothing — the same reason it is inert
in the three packages, but stated as an absence here because an app is exactly where a reader
expects to find it. Give minimal-app a `paths` map and this becomes an omission; enable it in the
same edit.

**Raw `<button>`, forbidden in wallow-auth and minimal-app, not in wallow-web.** The
`react/forbid-elements` list is otherwise identical in all three (`p`, `span`, `legend`, `code`,
`h1`–`h6`); wallow-web simply omits the `button` entry. Five raw `<button>`s are deliberate there,
in two unrelated trees and for two different reasons, which is why there is no single scoped
override to write:

- Four in `src/app/routes/bff-demo.tsx` — the un-catalogued controls of the BFF demo. The point of
  that route is to show the flow working with no design system attached.
- One in `src/shared/components/SignOut.tsx` — the nav footer's sign-out. It is a `<button>` rather
  than a `Link` because it POSTs the BFF logout instead of routing, and it borrows the rail's
  geometry from `navRowClassName` rather than a catalog recipe. Its own header comment states this;
  it is permanent, not demo scaffolding.

So deleting the demo does **not** by itself let the `button` entry be added to wallow-web: the
sign-out would still need either a catalog component that renders a real button row, or a scoped
override naming that one file.

**`text-heading-variant`, enabled in all three at different levels.** Every level still has to
NAME a variant everywhere — what differs is which levels each app admits and at what step.

- **wallow-auth** pins `h1: false`. `AuthLayout` owns the page's one level-1 heading and it is
  `FocusOnNavigate`'s focus target, so no other file may open one; the layout itself is the one
  scoped override (`src/shared/components/auth-layout.tsx`, restating `h2: subheading`).
- **wallow-web** has no such layout, so a route may open its own `h1`. It names only
  `h2: subheading`. Its one scoped override is `LandingPage.tsx`, which runs a marketing scale
  (`h1: display`, `h2: title`, `h3: subheading`) one step above the card scale the rest of the app
  uses. An override's entry REPLACES the base one, so that block restates every level it wants.
- **minimal-app** takes wallow-web's shape and its reason: no layout owns the level-1 heading, so
  `h2: subheading` and nothing else. It carries no scoped override.

**`no-tinted-text` and `no-hand-rolled-mutation` are now on in all three.** Neither had a
principled reason to be missing — `no-tinted-text` was absent from wallow-auth and
`no-hand-rolled-mutation` from wallow-web only because each was written while looking at the other
app. Turning them on cost wallow-auth three `text-primary hover:text-primary/80` links, on the
not-found, access-request and error screens, which are now `Button variant="link"` rendering an
`<a>` — the recipe fix the rule exists to force. `no-hand-rolled-mutation` was **vacuous** in
wallow-web when enabled (it writes through the generated `{operation}Mutation()` factories
already); it is on so that staying that way is enforced rather than observed.

**The two package-level exemptions** (`drawer.styles.ts`, `use-app-form.ts`) are above, under
"Who enables what". Both name a single file, which is the shape any future exemption must take —
a `files` glob naming the one counter-example, never a rule dropped from a `rules` block.

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
  index arithmetic fails the gate. One block carries that relaxation for three package globs —
  `packages/testing/src/**` and `packages/env/src/**` are in it for the same reason — and the
  three apps repeat it beside their `unicorn/filename-case` setting. Every other number stays
  named: the relaxation buys `slice(0, n)`, not an unexplained `255`.
- **A hex literal cannot satisfy both halves of the toolchain.** `oxfmt` rewrites hex digits to
  LOWER case and `unicorn/number-literal-case` (on via `categories.style`) demands UPPER, so
  `pnpm format` and `pnpm lint` will undo each other forever. Two ways out, both in the tree:
  derive the value instead of writing it — `packages/env/src/client-address.ts` builds its octet
  and group masks from the bit widths, which says what they are — or, where the literals ARE the
  specification, a file-level `oxlint-disable` naming the rule and why, as
  `apps/wallow-auth/e2e/totp.ts` does for RFC 6238's masks.

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
spec cannot import `node:fs` **anywhere in the repo**. The root config enables it at top level,
outside any override, so this reaches every spec — including packages with no nested config of
their own. "Convert it or delete it" is no longer advice.

Type-awareness is not the boundary — oxlint has none, but nothing in this repo is blocked by that.

**The corollary that bites:** `ignorePatterns` (`dist`, `generated`, `routeTree.gen.ts`, `.output`)
makes a rule **silent** where a disk sweep is **loud**. When converting a sweep, confirm the paths
it covered are paths oxlint actually lints — the difference between "exempt" and "silently
unchecked" is invisible in a green run. This cuts the other way too, and it is why the doctrine
above prefers deletion: a sweep that reads files oxlint never lints was reporting on code no rule
can police, which is a reason to ask what it was worth, not a reason to keep it.
