# packages/lint — @bc-solutions-coder/lint Agent Guide

Wallow's own oxlint JS-plugin rules (`wallow/*`) — the constraints with no native equivalent.
oxlint does not implement `no-restricted-syntax` (naming it makes oxlint refuse to parse the
whole config), and no built-in rule reads Tailwind class strings; a JS plugin is the only
mechanism that can.

Private but shaped to publish: `exports` points at `src/` in-repo, `publishConfig.exports`
swaps to `dist/`. Every rule uses `createOnce` and the plugin is wrapped in
`eslintCompatPlugin`, so it runs under ESLint 8+ as well as oxlint — which is why
`@oxlint/plugins` is a runtime `dependencies` entry, not a devDependency.

## Registration

The plugin is registered in exactly ONE config, the repo-root `.oxlintrc.json`:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
```

- A nested config inherits the registration through `extends` — it needs no `jsPlugins` entry
  and no `@bc-solutions-coder/lint` devDependency. Only the root `package.json` names the
  package, because a `jsPlugins` specifier resolves from the REGISTERING config's directory.
- The root enables three option-free, self-gating rules repo-wide: `wallow/no-source-tests`
  (gates on `*.test.*` filenames; its doctrine-blessed exemptions are a single override block
  in the root config, each file with its reason), `wallow/module-lists-in-sync` (gates on a
  library `vite.config.ts`) and `wallow/logger-no-node-builtins` (gates on
  `packages/logger/src/`). The remaining `wallow/*` rules are enabled per-tree by nested
  configs, because their options genuinely differ between trees (`text-heading-variant`) or
  are inert without per-app inputs (`zone-dag`) — and an oxlint override REPLACES options
  rather than merging.
- `packages/sdk/src/oxlint-guardrails.test.ts` proves the root config by copying it and
  running the real binary over a mirror tree under gitignored `.lint-mirror/` INSIDE the repo —
  that placement lets the copied config resolve the plugin specifier through the workspace
  `node_modules`. Do not move the mirror to a temp directory; the spec's comments say why. The
  same spec walks every nested config asserting it `extends` the root and restates no
  `categories`/`plugins`.
- This constrains `jsPlugins` only. The `plugins` array names oxlint's built-in Rust plugins
  (`typescript`, `unicorn`, `oxc`, `react`, `import`), which resolve nothing off disk.

**A subtree the registration does not reach lints with every `wallow/*` rule vacuously
passing — nothing fails.** The `extends` line is load-bearing, and a config that omits it (or
a pass run with `-c`, below) loses the plugin silently. The check is a probe, not a reading of
the config: plant a file with a known violation and confirm `pnpm lint` names it.

Configs are JSONC — the reason for each registration and exemption is a `//` comment beside
the entry. Any spec that reads a config back must strip line comments before `JSON.parse`.

**The cost of nesting:** oxlint matches an override's globs AND `ignorePatterns` relative to
the config's own directory, so each nested config must RESTATE the root's `apps/<app>/**`
override block and the root's `ignorePatterns` — repo-rooted prefixes match nothing from a
nested directory. Do not delete the restatement, and remember an edit to a root app block does
not reach that app's nested config on its own.

## The root config around it

- **Two passes cover the tree exactly once.** `pnpm lint` takes source, excluding
  `**/*.test.*` and `**/*.stories.tsx`; `pnpm lint:tests` takes exactly those files with
  oxlint's vitest plugin on. After touching a rule's own specs, run both. The test pass
  enumerates its file list (oxlint expands no globs in path arguments and `ignorePatterns` has
  no `!` negation) — a wrong list lints ZERO files and exits 0, so `scripts/lint-tests.sh`
  prints its count and fails on zero. **Neither pass may pass `-c`:** an explicit config file
  disables nested-config lookup, dropping every per-tree `wallow/*` enablement and the test
  relaxations.
- **`import/no-cycle` is named explicitly** (it belongs to no category and is off by default).
  Enabling the `import` plugin for it also switches on that plugin's category rules, several of
  which condemn things this repo does deliberately — those are switched **off by name** beside
  `no-cycle` (`no-named-export`, `prefer-default-export`, `group-exports`, `exports-last`,
  `no-nodejs-modules`, `no-namespace`, `namespace`). Both passes run `--deny-warnings`. Do not
  switch them on to tidy the config; do not delete them to shorten it.
- The root relaxes `no-magic-numbers` to `ignore: [0, 1]` for `packages/lint/src/**`,
  `packages/testing/src/**` and `packages/env/src/**` (ordinary index arithmetic under
  `--deny-warnings`); the apps repeat it beside their `unicorn/filename-case` setting. Every
  other number stays named — the relaxation buys `slice(0, n)`, not an unexplained `255`.

## Who enables what

The nested configs (`apps/wallow-web`, `apps/wallow-auth`, `apps/minimal-app`, `packages/ui`,
`packages/forms`, `packages/navigation`) enable the per-tree `wallow/*` rules;
`scripts/fork-smoke`'s config enables none, keeping the scaffold out of the repo's rule set.
No config blanket-disables a rule, and none may start. The complete divergences — each a rule
that would decide nothing or be wrong in that tree:

- **`zone-dag` is absent in `apps/minimal-app`.** The rule derives its zones from the app's
  `tsconfig.json` `paths` map, and minimal-app is deliberately un-zoned with none. If
  minimal-app gains a `paths` map, enable the rule in the same edit.
- **Raw `<button>` is forbidden in wallow-auth and minimal-app but not wallow-web** — the
  `react/forbid-elements` lists are otherwise identical. wallow-web's raw buttons are
  deliberate: the un-catalogued controls of `src/app/routes/bff-demo.tsx`, and
  `src/shared/components/SignOut.tsx` (POSTs the BFF logout rather than routing; its header
  comment explains). Deleting the demo alone does not clear the way to add the entry.
- **`text-heading-variant` levels differ per app.** wallow-auth pins `h1: false`
  (`AuthLayout` owns the page's one `h1`; the layout file is its scoped override). wallow-web
  and minimal-app name only `h2: subheading`; wallow-web's one scoped override is
  `LandingPage.tsx`, which runs a marketing scale. An override's entry REPLACES the base one,
  so an override block restates every level it wants.
- **Two scoped package exemptions**, each naming ONE file — the shape any future exemption
  must take (a `files` glob naming the counter-example, never a rule dropped from a `rules`
  block): `packages/ui/src/components/drawer/drawer.styles.ts` relaxes `no-sidebar-inversion`
  (its recipe fades a bare `bg-foreground` in via opacity, which a baked-in alpha cannot
  animate from), and `packages/forms/src/form/use-app-form.ts` relaxes
  `no-hand-rolled-mutation` (its `mutationFn` is the no-mutation escape hatch itself).

`no-source-tests` self-gates on `*.test.*` filenames, so it must NOT appear in any config's
`*.test.*` override block; each config carries a comment beside `zone-dag`'s saying so.

## Authoring a rule

- **`.ts` extensions on every relative import inside `src/` are mandatory.** oxlint loads the
  plugin as plain Node ESM, which rejects extensionless relative specifiers with
  `ERR_MODULE_NOT_FOUND`; TypeScript typechecks either spelling, so the failure appears only
  at lint time.
- **`createOnce` runs once per PROCESS, but `context.options` are per FILE.** Read options
  inside a visitor, never at setup time — hoisting the read freezes whichever file linted
  first. Per-file mutable state belongs in a `before()` hook.
- **`before()` is not guaranteed to run on every file** — oxlint may skip rules whose
  interesting node types are absent. Code that must run for every file goes in a `Program`
  visitor.
- **A hex literal cannot satisfy both halves of the toolchain**: `oxfmt` lowercases hex digits
  and `unicorn/number-literal-case` demands upper, so `pnpm format` and `pnpm lint` undo each
  other forever. Either derive the value from bit widths (`packages/env/src/client-address.ts`)
  or, where the literals ARE the specification, add a file-level `oxlint-disable` naming the
  rule and why (`apps/wallow-auth/e2e/totp.ts`).

## Testing rules

`src/fixtures.test.ts` is one generic spec driving every rule — there are no per-rule specs.
`fixtures/<rule>/` holds the cases; the harness runs the real oxlint binary per fixture
directory and asserts the reported `(file, line, rule)` multiset equals the annotated one
EXACTLY, so an unannotated diagnostic and an annotation nothing fired on both fail. See the
spec's header for the oxlint behaviours it depends on.

- `fixtures/**` is excluded from `pnpm lint` and `oxfmt` at the repo root — the files contain
  deliberate violations, and formatting would move `expect-error` annotations off their target
  lines. `tsconfig.json` includes `src` only for the same reason.
- Include the cases a rule must NOT report: `no-tinted-text`'s `valid.tsx` carries
  `bg-foreground/40` because a translucent surface is not tinted text. A filename-gated rule
  needs a valid file of the other kind — `no-source-tests`' plain `valid.ts` imports `node:fs`
  and must draw nothing, which is the only thing that proves the gate exists.

## The rule-vs-test boundary

> **A rule sees one JS/TS file at a time, and only files oxlint lints.**

That decides one thing: whether a constraint CAN be a rule. What fails it — a relationship
between files, an absence, a non-JS input — does not thereby become a vitest spec; under
`.claude/rules/TESTING.md` the usual answer is to delete the constraint, because a real
regression fails a build, `pnpm check:exports`, or an `e2e/` run without it. What genuinely
stays a spec is behaviour a rule cannot see: computed styles that only exist at runtime, and
runtime/compile-time identity. `wallow/no-source-tests` enforces the rest — a spec cannot
import `node:fs` anywhere in the repo.

**The corollary that bites:** `ignorePatterns` (`dist`, `generated`, `routeTree.gen.ts`,
`.output`) makes a rule SILENT where a disk sweep is loud. When converting a sweep, confirm
the paths it covered are paths oxlint actually lints — "exempt" and "silently unchecked" look
identical in a green run.
