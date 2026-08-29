# packages/lint — @bc-solutions-coder/lint Agent Guide

Wallow's own oxlint JS-plugin rules (`wallow/*`). oxlint does not implement
`no-restricted-syntax` (naming it makes oxlint refuse to parse the whole config), and no
built-in rule reads Tailwind class strings. Every rule uses `createOnce`; the plugin is
wrapped in `eslintCompatPlugin` so it runs under ESLint 8+ too — which is why
`@oxlint/plugins` is a runtime `dependencies` entry, not a devDependency.

## Registration

The plugin is registered in exactly ONE config, the repo-root `.oxlintrc.json`:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
```

- A nested config inherits the registration through `extends` — it needs no `jsPlugins`
  entry and no `@bc-solutions-coder/lint` devDependency, because a `jsPlugins` specifier
  resolves from the REGISTERING config's directory.
- The root enables the three option-free, self-gating rules repo-wide (`no-source-tests`,
  `module-lists-in-sync`, `logger-no-node-builtins`); the rest are enabled per-tree by
  nested configs, because their options genuinely differ between trees or are inert without
  per-app inputs — and an oxlint override REPLACES options rather than merging.
- `packages/sdk/src/oxlint-guardrails.test.ts` proves the root config over a mirror tree
  under `.lint-mirror/` INSIDE the repo — the placement lets the copied config resolve the
  plugin; do not move it to a temp directory (the spec's comments say why).
- This constrains `jsPlugins` only; the `plugins` array names oxlint's built-in Rust
  plugins, which resolve nothing off disk.

**A subtree the registration does not reach lints with every `wallow/*` rule vacuously
passing — nothing fails.** A config omitting `extends`, or a pass run with `-c` (an
explicit config file disables nested-config lookup), loses the plugin silently. Verify with
a probe — plant a known violation and confirm `pnpm lint` names it — never by reading
configs. Configs are JSONC (a `//` comment beside each entry says why); a spec reading one
back must strip line comments before `JSON.parse`.

**The cost of nesting:** oxlint matches an override's globs AND `ignorePatterns` relative to
the config's own directory, so each nested config must RESTATE the root's `apps/<app>/**`
override block and the root's `ignorePatterns` — repo-rooted prefixes match nothing from a
nested directory. Do not delete the restatement; an edit to a root app block does not reach
that app's nested config on its own.

## The root config around it

- The test pass enumerates its file list (oxlint expands no globs in path arguments and
  `ignorePatterns` has no `!` negation) — a wrong list lints ZERO files and exits 0, so
  `scripts/lint-tests.sh` prints its count and fails on zero. **Neither pass may pass `-c`**
  (see above).
- **`import/no-cycle` is named explicitly** (no category, off by default). Enabling the
  `import` plugin for it also switches on category rules that condemn things this repo does
  deliberately — those are switched **off by name** beside `no-cycle` (`no-named-export`,
  `prefer-default-export`, `group-exports`, `exports-last`, `no-nodejs-modules`,
  `no-namespace`, `namespace`). Deliberate config that looks like cruft: do not switch them
  on to tidy, do not delete them to shorten.
- `no-magic-numbers` is relaxed to `ignore: [0, 1]` only for lint/testing `src/**` and the
  SDK's `client-address.ts` (the apps repeat it) — it buys `slice(0, n)`, not an unexplained `255`.

## Who enables what

Nested configs (the three apps, `packages/ui`, `packages/forms`, `packages/navigation`)
enable the per-tree `wallow/*` rules; `scripts/fork-smoke`'s enables none. No config
blanket-disables a rule, and none may start. The complete divergences:

- **`zone-dag` is absent in `apps/minimal-app`** — the rule derives its zones from the
  app's `tsconfig.json` `paths` map, and minimal-app is deliberately un-zoned with none. If
  it gains a `paths` map, enable the rule in the same edit.
- **Raw `<button>` is forbidden in wallow-auth and minimal-app but not wallow-web** —
  wallow-web's raw buttons are deliberate: `bff-demo.tsx`'s un-catalogued controls and
  `SignOut.tsx` (POSTs the BFF logout; its header comment explains). Deleting the demo
  alone does not clear the way to add the entry.
- **`text-heading-variant` levels differ per app.** wallow-auth pins `h1: false`
  (`AuthLayout` owns the page's one `h1`; the layout file is its scoped override);
  wallow-web and minimal-app name only `h2: subheading`; wallow-web's one scoped override is
  `LandingPage.tsx` (a marketing scale). An override's entry REPLACES the base one, so an
  override block restates every level it wants.
- **Two scoped package exemptions, each naming ONE file** — the shape any future exemption
  must take (a `files` glob naming the counter-example, never a rule dropped from a `rules`
  block): `packages/ui/.../drawer/drawer.styles.ts` relaxes `no-sidebar-inversion` (fades
  `bg-foreground` in via opacity, which a baked-in alpha cannot animate from);
  `packages/forms/src/form/use-app-form.ts` relaxes `no-hand-rolled-mutation` (its
  `mutationFn` IS the escape hatch).
- **`no-source-tests` self-gates on `*.test.*` filenames, so it must NOT appear in any
  config's `*.test.*` override block** — each config carries a comment saying so.

## Authoring a rule

- **`.ts` extensions on every relative import inside `src/` are mandatory** — oxlint loads
  the plugin as plain Node ESM, which rejects extensionless relative specifiers; TypeScript
  typechecks either spelling, so the failure appears only at lint time.
- **`createOnce` runs once per PROCESS, but `context.options` are per FILE.** Read options
  inside a visitor, never at setup time — hoisting the read freezes whichever file linted
  first. Per-file mutable state belongs in a `before()` hook.
- **`before()` is not guaranteed to run on every file** — oxlint may skip rules whose
  interesting node types are absent. Code that must run for every file goes in a `Program`
  visitor.
- **A hex literal cannot satisfy both halves of the toolchain**: `oxfmt` lowercases hex
  digits and `unicorn/number-literal-case` demands upper, so `pnpm format` and `pnpm lint`
  undo each other forever. Derive the value from bit widths
  (`packages/sdk/src/server/client-address.ts`) or, where the literals ARE the specification, add a
  file-level `oxlint-disable` naming the rule and why (`apps/wallow-auth/e2e/totp.ts`).

## Testing rules

`src/fixtures.test.ts` is one generic spec driving every rule THAT HAS a `fixtures/<rule>/`
directory — it enumerates fixture directories with `readdirSync`, so **a rule without one is
silently untested** (today `no-hand-rolled-mutation`, `no-sidebar-inversion` and
`text-heading-variant` have none). The harness runs the real oxlint binary per fixture
directory and asserts the reported `(file, line, rule)` multiset equals the annotated one
EXACTLY — an unannotated diagnostic and an annotation nothing fired on both fail.

- `fixtures/**` is excluded from `pnpm lint` and `oxfmt` at the repo root — the files
  contain deliberate violations, and formatting would move `expect-error` annotations off
  their target lines. `tsconfig.json` includes `src` only for the same reason.
- Include the cases a rule must NOT report: `no-tinted-text`'s `valid.tsx` carries
  `bg-foreground/40` because a translucent surface is not tinted text. A filename-gated rule
  needs a valid file of the other kind — `no-source-tests`' plain `valid.ts` imports
  `node:fs` and must draw nothing, the only proof the gate exists.

## The rule-vs-test boundary

A rule sees ONE JS/TS file at a time, and only files oxlint lints — that decides whether a
constraint CAN be a rule; what fails it usually gets deleted, not moved to a spec
(`.claude/rules/TESTING.md`). The corollary that bites: `ignorePatterns` makes a rule
SILENT where a disk sweep is loud — when converting a sweep, confirm its paths are paths
oxlint actually lints; "exempt" and "silently unchecked" look identical in a green run.
