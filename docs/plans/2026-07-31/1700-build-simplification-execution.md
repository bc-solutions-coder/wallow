**status: completed**

# Build simplification — execution plan

## Progress log

Tracked in beads under epic **Wallow-acr0**. Update this section AND the bead as each step lands.

| Step | State | Commit |
| --- | --- | --- |
| Baseline | done | recorded below |
| 0.1 check ordering | done | `de08d2a6` |
| 0.2 beads filed | done | `Wallow-uc2c`, `Wallow-luni` |
| 0.3 vacuous assertions | done | `218bf2a3` |
| 1.1 testing/browser-optimize-deps | done | `058b0631` |
| 1.2 forms/package-scaffold | done | `fdd8e1b1` |
| 1.3 ui/package-scaffold | done | `9595b299` |
| 1.4 sdk/build-config (deleted) | done | `3f5e6d3d` |
| 1.5 sdk/oxfmt-config (deleted) | done | `b3ee934f` |
| 1.6 ui/storybook-setup (13→2) | done | `86998313` |
| 1.7 testing/vitest-projects | done | `6afc22b3` |
| 1.8 styles/vite.test.ts | done | `48c80ff3` |
| 1.9 testing/sdk-seam-exports | done | `055d99bd` |
| 1.10 ui/list-row.composition | done | `f78a42f0` |
| **Phase 1 complete** | done | `pnpm check` exit 0 |
| 2.1 exports → src + publishConfig | done | `584adc3b` |
| 2.2 `--configLoader runner` | done | `584adc3b` |
| 2.3 sdk tsconfig drift | done | `584adc3b` |
| 2.4 shared tsconfig.build.base | done | `584adc3b` |
| **Phase 2 complete** | done | `pnpm check` exit 0 from zero `dist/` |
| (branding relocation) | done | `9ed5ca48` |
| 3.1 defineLibraryConfig | done | `1fb9f051` |
| 3.2 wallowApp preset | done | `5fdc6ae0` |
| 3.3 pre-bundle globs | done | `a34844b7` |
| 3.4 createVitestProjects | done | `8146b869` |
| **Phase 3 complete** | done | `pnpm check` exit 0; test counts unchanged in every package |
| **Phase 4 lint split** | done | `pnpm lint` 441 + `pnpm lint:tests` 399 = 840; both probes caught |
| 5.1 wallow-auth gate + catalog-adoption | done | `5641bcf2` |
| 5.2 wallow-web typography + chrome sweeps | done | `5641bcf2` (1,390 lines out, `pnpm check` exit 0) |
| Phase 5 remaining (query-facade, generated-mutations, features-api-seam, web-shell-removal) | **next** | |

**Phase 1 complete. `pnpm check` exits 0** (format:check · lint · build · typecheck · test ·
check:exports).

Test counts after 1.10: **5509 tests / 395 files** (baseline 5569 / 399) — 60 specs and 4
files retired, every step's delta matching its prediction exactly. Per package vs. baseline:
styles 103→96 · sdk 1134→1118 · testing 82→71 · ui 1515→1497 · forms 118→113 · wallow-web
1098→1095; query/auth/minimal-app/wallow-auth untouched.

> The "5523 after 1.6" figure previously recorded here was a transcription slip for **5522**;
> the per-package totals above reconcile to −60 exactly and no spec was lost unaccounted for.

**Phase 2 complete (`584adc3b`). `pnpm check` exit 0 run from a completely empty `dist/`** —
the plan's stated acceptance test, in full: `pnpm typecheck` 10/10 projects, a standalone
`wallow-web` build (SSR + Nitro), `pnpm build`, `pnpm test`, `pnpm check:exports`.

Test counts after Phase 2: **5505 tests** (5509 after Phase 1) — −4, matching the prediction
exactly: sdk −2, ui −1, forms −1. The tsconfig restructure was proved inert by diffing all
**250** emitted `.d.ts` paths across a from-scratch rebuild: identical, with no stray
`./dist` or `packages/dist`.

### Deviations from this plan, and why

1. **0.3, ui + forms `package-scaffold.test.ts`.** The plan named only the line
   `expect(deps.typescript).not.toBe("6.0.3")`. Removing just that line would leave an
   `it("does not copy packages/sdk's TS6 typescript pin")` that no longer checks any such
   thing, so the dead assertion went and the block was renamed
   `it("declares its own typescript")`. Those two packages therefore show **no test-count
   change** for 0.3.
2. **1.3 followed `guard-test-audit.md` item 3, not this file's one-line summary.** The audit
   is named authoritative here, and its list is broader: it also covers the `exports`-map
   exact-object specs, the `tsconfig.build.json` field specs and the exact `scripts.build`
   string. Those are exactly what Phases 2.1 and 2.4 rewrite, so they were deleted at 1.3
   rather than surfacing as reds mid-Phase-2.
3. **1.3 loosened rather than deleted** `resolves an installed @base-ui/react at 1.6.x` → the
   v1 major line. The install guard is real (pnpm actually linked it); only the minor pin
   blocked a routine bump.
4. **1.6 lint fix.** The rewritten `storybook-setup.test.ts` initially failed `pnpm lint` on
   two `unicorn/no-await-expression-member` warnings (`--deny-warnings`). Hoist the awaited
   value into a local before `.map`/`.find`.
5. **1.9 kept the `contrast` half of barrel purity.** The plan says "keep the barrel-purity
   spec"; that spec named only `render-with-wallow`, while the `./contrast` assertion was
   embedded in a plumbing describe slated for deletion. `guard-test-audit.md` line 61 defines
   the keeper as "`src/index.ts` does not contain `render-with-wallow` **or** `contrast`", so
   the contrast assertion was folded into the surviving spec rather than dropped. Both entries
   are browser-only for the same reason, and the barrel loads in plain Node.
6. **1.8 deleted a dependency-bucket spec.** Step 1.2 said "keep the dependency-bucket specs",
   but 1.8 says "delete the last two describes" and `guard-test-audit.md` line 50 says
   "delete these four specs" naming the Tailwind `dependencies`-not-`devDependencies` one
   explicitly. Followed the audit.

7. **2.4 put the shared config at the REPO ROOT, not under `packages/`.** Prompted by a
   review question ("any reason we can't have a `tsconfig.build.json` at the top?"). The first
   attempt was `packages/tsconfig.build.base.json` carrying only `declaration` +
   `emitDeclarationOnly`, because a plain `outDir` in a base resolves against the *base's own*
   directory — measured, not assumed: it built `packages/query` into `packages/dist` and
   produced no `packages/query/dist` at all. **`${configDir}`** (TypeScript 5.5+) resolves
   against the config doing the extending, which fixes that and makes the file location-free.
   So the final version sits at the repo root beside `tsconfig.base.json` and hoists all four
   `compilerOptions` **plus** the shared `exclude` list — strictly more than the plan's
   "keep `rootDir` per-package". `include` still stays per-package (declaration-program
   scoping). Also added the new file to the root `format`/`format:check` script lists, which
   name `tsconfig.base.json` explicitly.
8. **Four Phase-2 reds the guard-test audit never catalogued.** The audit is authoritative but
   not exhaustive on exports shape-locks. All four are the same class the plan exists to
   remove — an exact-object assertion on an `exports` entry — and all were loosened to the
   invariant the spec's own comment states, never reverted:
   - `packages/styles/src/assets.test.ts` — `./assets` equalled an exact dist pair → now
     asserts it is defined and **not equal to** the main entry (the separation is the point:
     that module reads `node:url`).
   - `packages/sdk/src/server/build.test.ts` — two specs equalling exact dist pairs for `.`
     and `./server`, **deleted**: the nested-not-flat layout they cared about is already
     asserted against the emitted files in the same file, and resolution is attw's job. The
     passthrough spec was loosened to "declared, and not an alias of `./server`". Its
     describe was also renamed off "tsup", a bundler this package has not used in releases.
   - `packages/ui` + `packages/forms` `package-scaffold.test.ts` — both asserted
     `expect(pkg).not.toHaveProperty("publishConfig")` for a private package. Plan §2.1
     explicitly adds `publishConfig` **uniformly**, private members included, so a package
     that later drops `private` cannot publish a manifest pointing at TypeScript sources.
     `private: true` is what the spec should have been asserting, and now is.
9. **Two further consequences of resolving from source, neither in the plan.**
   - **Five more `--configLoader runner` sites.** The plan named only the three apps' Vite
     builds. Any `vitest.config.ts` importing `@bc-solutions-coder/testing` hits the identical
     failure, so `packages/{ui,forms}` and all three apps needed it on `test`/`test:watch`
     too. The two `package-scaffold` specs pinning `scripts.test` to the exact string
     `"vitest run"` were loosened to containment — which runner runs is the contract, its
     flags are not.
   - **`styles/src/branding.ts` needs `with { type: "json" }`.** It imports
     `api/branding.json`, previously inlined by the prebuilt bundle. Storybook evaluating
     `packages/ui/.storybook/main.ts` reaches this file through `styles/vite` under plain Node
     ESM, which rejects a JSON import with no attribute (`ERR_IMPORT_ATTRIBUTE_MISSING`).
10. **One ui spec deleted, not loosened.** `dist-structure.test.ts`'s
    `it("is importable by subpath from a consuming app")` spawned Node from `apps/wallow-web`
    to `import('@bc-solutions-coder/ui/button')`. In-repo that can no longer be true *by
    design* — the wildcard resolves to a `.tsx` with extensionless relative imports, which
    Vite and tsc read and Node does not. The contract is still covered: `pnpm build` and
    `pnpm typecheck` resolve the same wildcard from both apps, the five surviving specs still
    assert the emitted per-component files, and `check:exports` covers Node-style resolution
    for the two packages that are actually published. This package is private.
11. **`scripts/check-exports.sh` had to change for the gate to pass at all.** `attw --pack`
    shells out to **npm** pack, and npm does not apply `publishConfig.exports` — so attw
    resolved the `src/` map against a tarball containing only `dist/` and reported all four
    sdk entrypoints unresolvable. Diagnosed by packing with `pnpm pack` and reading the
    tarball's own manifest, which correctly carried the `dist/` map. `packages/auth` and
    `packages/query` "passed" throughout only because they declare no `files` field, so `src/`
    ships in their tarballs — passing for the wrong reason. The script now packs each package
    with `pnpm pack` into a `mktemp -d` (EXIT-trapped) and hands attw the tarball path.
12. **`branding.json` moved `api/` → `packages/styles/`, ahead of 3.1.** Prompted by a review
    question, then verified rather than assumed: `api/` has zero references to the filename,
    zero to a `BrandingOptions` type, and no appsettings/csproj/props/targets mention. It was
    frontend-only the whole time — it sat under `api/` because Blazor once served from there.
    Both app Dockerfiles already `COPY packages/styles`, so their `COPY api/branding.json` line
    was simply removed. Also updated: `docker/docs/Dockerfile`, `scripts/docs-serve.sh`,
    `.github/workflows/docs.yml` (call site + both `paths:` filters) and ~40 prose files.
    Not done: converting it to CSS, also floated. It stays JSON — `generate-docs-theme.mjs`
    and the `ForkBranding` type both read it as data, and a fork edits one file either way.
13. **3.1 uses `build.rolldownOptions`, not `build.rollupOptions`.** The research artifact
    `vite-review.md` §297-298 says not to rename it and that `rollupOptions` "is not on Vite
    8's deprecation list". The installed types disagree and are the authority:
    `node_modules/vite/dist/node/index.d.ts:858` marks `rollupOptions` `@deprecated Use
    rolldownOptions instead.`, with `rolldownOptions` at :875 taking the same `RolldownOptions`
    type. Rolldown ships **as part of** vite 8 — no separate dependency, no `rolldown-vite`
    alias, no override. Output is unaffected by the rename (verified byte-identical).
14. **3.1 needed three supporting changes the plan does not mention.**
    - **`vite` is now a root devDependency.** `tools/` has no `package.json`, so
      `tools/vite/library.ts` resolves `vite` upward to the root `node_modules` — which did
      not contain it, since vite was declared only per package. TS2307 until added.
    - **All seven package `tsconfig.json` files dropped `declaration` + `outDir`.** Dead since
      2.4 moved emit to `tsconfig.build.json`, but not inert: naming `outDir` in a `--noEmit`
      config makes TypeScript *infer* a rootDir spanning every input, so a `vite.config.ts`
      importing `tools/` fails TS6059. Documented once in `tsconfig.base.json`.
    - **`pnpm lint` now covers `tools/`**, with the same `no-magic-numbers` 0/1 allowance the
      three app overrides carry. Note `.oxlintrc.json` must stay **strict JSON**:
      `packages/query/src/web-shell-removal.test.ts` `JSON.parse()`s it and silently treats a
      parse failure as "no exemption", so one `//` comment reds four unrelated assertions.
15. **3.1 verification went beyond the plan's `dist-structure.test.ts`.** That spec checks
    shape, not bytes. Every one of the **588** built files was hashed before and after: 588/588
    identical after the refactor, and again after the `rolldownOptions` rename. `assetFileNames`
    is now set for all seven rather than styles alone — Wallow-do5e is repo-wide policy and no
    other package emits an asset, so this is uniformity, not a behaviour change.

16. **3.2's first attempt was rejected by the user and reverted whole.** The plan says the
    preset absorbs the app config; the obvious reading is *all* of it, plugins included. That
    version forced four new ROOT devDependencies — `@bc-solutions-coder/styles`,
    `@tanstack/react-start`, `@vitejs/plugin-react`, `nitro` — because `tools/` has no
    manifest and resolves upward into the root `node_modules`. Verdict: *"I don't think I want
    to have this app.ts and package.json update as it brings up too much."* Reverted
    completely (`rm tools/vite/app.ts`, `git checkout --` on three configs and
    `package.json`, `pnpm install`) rather than patched. The pinned `@tanstack/react-start`
    was the sharpest objection: the whole point of the `start` catalog is that the version is
    edited in one place, and a root devDependency reintroduces a second.

17. **The preset is deliberately plugin-free, and stays that way even now that it is a
    package.** Chosen by the user from an options list: `wallowAppConfig()` returns only
    `server.port`, the two `use-sync-external-store` aliases, `ssr.noExternal` and
    `environments.client.copyPublicDir` — the half nothing in the test suite covers. Each app
    keeps a visible, local `plugins` array. Beyond "less magic", there is a hard reason it
    cannot move: `packages/styles` builds *with* this package, so making `wallowStyles()` a
    dependency of it is a workspace cycle. `base` also stays app-local — only wallow-auth has
    one. `resolve.dedupe` was deleted, not relocated, exactly as the plan called for.

18. **`tools/vite/` became a workspace package, `@bc-solutions-coder/config`.** The user's
    question — *"Would this make more sense to just be in a package?"* — is the actual fix for
    what deviation 16 ran into: a package carries its own manifest, so `vite` moved OUT of the
    root `package.json` and nothing outside a package imports it any more. Three things this
    forced that are worth remembering:
    - **No barrel.** `src/vite/index.ts` re-exporting `./library` typechecked clean and failed
      every `packages/*` build with `ERR_MODULE_NOT_FOUND`. A consumer's Vite config resolves
      this package as an ordinary external dependency, so **plain Node ESM** loads it, and Node
      rejects the extensionless relative specifier that `moduleResolution: "Bundler"` permits.
      Two subpaths pointing straight at two files have no relative imports at all. Recorded in
      `packages/config/CLAUDE.md`; the alternative fixes (add `--configLoader runner` to seven
      more build scripts, or `allowImportingTsExtensions`) were both worse.
    - **Never built, never published.** No `build` script, no `dist/`, no `publishConfig`;
      `exports` points at `src/` permanently. A config every build imports cannot be a thing
      every build must build first.
    - **The `.oxlintrc.json` `tools/**` override is NOT vestigial.** Tested by deleting it
      after the move: `pnpm lint` failed with three `no-magic-numbers` warnings in
      `tools/oxlint/wallow-lint-plugin.js`. Restored. Incidental finding — extending lint to
      `tools/` in 3.1 earns its keep on its own; that plugin had never been linted.

    Verification carried over from the rejected attempt unchanged, since both produced the
    same artifacts: all three `.output` trees byte-compared against a pre-refactor baseline
    (`git stash` → build → hash 90/109/23 files → pop → rebuild), differing only in
    `.output/server/index.mjs` and `.output/nitro.json`. Those two were **proven**
    nondeterministic rather than assumed — rebuilding wallow-web twice with the *same* config
    still differs, because the entry embeds a public-asset manifest carrying `mtime`s and
    unstable key order and `nitro.json` carries a build `date`. The seven package builds were
    shown unaffected the cheap way: `git diff --cached -M` on the moved `library.ts` shows the
    only change since `1fb9f051` is a comment. Boot check per the plan's §3.2 clause:
    `/login` 200 @ 15056 chars, `/bff-demo` 200 @ 8702, minimal-app `/` 200 @ 6774, zero
    "Invalid hook call" / "No QueryClient set" in any server log. minimal-app's duplicate
    react-query server chunks (`_libs/react+tanstack__react-query.mjs`,
    `_libs/tanstack__query-core.mjs`, `_libs/@tanstack/react-router-ssr-query+*.mjs`) are gone
    — Wallow-uc2c confirmed by artifact, not assertion.

19. **3.2 was committed twice — the first commit was incomplete, and the repair is worth
    remembering.** To check whether the root `CLAUDE.md` was already unformatted before my
    edit I ran `git stash -q; oxfmt --check CLAUDE.md; git stash pop -q`. **`git stash pop`
    does not restore the index** (that needs `--index`), so 20 of the 27 staged paths silently
    unstaged themselves and `f7367267` landed with 7 files: it added `packages/config` but
    rewired none of the ten consumers, deleted no `tools/vite/library.ts`, and updated no
    lockfile — `pnpm install --frozen-lockfile` would have failed on it in CI. The tell was in
    the hook output (`.lintstagedrc.mjs — 7 files`) and I read past it. Caught later when
    `git diff --cached --stat` during 3.3 listed paths 3.3 had never touched.

    Repaired by committing the full verified tree as a throwaway safety net (`fbe5af78`),
    `git reset --soft f7367267~1`, reverting the 3.3-only hunks in the three files both steps
    touch (`packages/testing/package.json`, `packages/testing/vite.config.ts`,
    `packages/ui/package.json`), regenerating the lockfile at that state, and committing 3.2 as
    `5fdc6ae0`. The pre-commit hook typechecks the whole WORKING TREE, not the index, so the
    unstaged and untracked 3.3 files had to be parked on disk for that commit to be honest
    rather than waved through with `--no-verify`. Both commits were then proven lossless the
    only way that means anything: **`git diff fbe5af78 HEAD` is empty**.

    (Two incidental traps: `cp a/x.json b/x.json c/y.ts DIR/` flattens and silently overwrites
    same-named files — the first backup lost `packages/testing/package.json` under
    `packages/ui/package.json`, recovered from the safety-net commit. And zsh does **not**
    word-split unquoted parameters, so `for f in $LIST` iterates once over the whole string;
    use an array.)

20. **3.3 removed a guard test the plan did not mention, and declared a dependency where the
    plan said delete.** Two deviations, both from measuring rather than assuming:
    - `apps/wallow-web/src/query-facade.test.ts` had an `it` pinning `@tanstack/react-query` in
      the browser project's `optimizeDeps.include`, commented "react-query is still the module
      Vite pre-bundles, one facade hop away". The measurement disproves it: wallow-web declares
      neither package, so that entry logged `Failed to resolve dependency` and pre-bundled
      nothing. The `it` was deleted (a spec pinning a dead entry is worse than no spec) and
      replaced with a comment; the sibling `it` pinning the live facade/auth entries stays.
      This is a **third** exemption from Rule 6 beyond the guard relocation — flagged rather
      than folded in silently.
    - The plan says remove `vitest-browser-react` "from the shared baseline". It is dead only
      in `packages/ui` and **live** in the four packages that declare it, so removing it from
      the baseline would strip protection from all five to silence one warning. Declared it in
      `packages/ui`'s devDependencies instead — the pattern forms and minimal-app already use
      (both declare it without importing it, purely for pre-bundling), and what the relocated
      guard's own "declares every package its list names" assertion demands.

    Two mechanical costs worth knowing before moving code out of a `*.test.ts`: the root
    `.oxlintrc.json` gives `no-magic-numbers` an `ignore: [0, 1]` override for `tools/**` and
    the three app globs but **not** for `packages/**`, so test code that lints clean fails
    immediately as a source file (4 warnings here, plus one
    `unicorn/no-array-callback-reference`). And a new `packages/testing` entry needs THREE
    edits, not two — `package.json` `exports` + `publishConfig.exports`, `vite.config.ts`
    `entries`, and `tsconfig.build.json` `include`; vite emits the `.js` but `tsc` emits the
    `.d.ts`, and `check:exports` (publint) is what catches the omission.

21. **3.4 measured `extends: true` merge semantics before writing them down, re-pointed four
    guards at the config VALUE, and deleted two tests it had just written.**
    - I first wrote in a comment that a project-level `resolve` REPLACES the inherited one.
      That was a guess, and it was wrong. Measured both directions: dropping `tsconfigPaths`
      from wallow-web's browser project and running an alias-heavy spec
      (`routes/bff-demo.test.tsx`, 10 tests) is **green** — the root's setting survives a
      project that declares only an alias — and the control (delete the root `resolve`) is
      **red** with `Failed to resolve import "@app/router"`. The comment now states the
      measured fact and the experiment that produced it.
    - Four guards regexed `vitest.config.ts` for a `const extraBrowserOptimizeDeps = [...]`
      declaration and went red the moment the list became an inline property. The stated
      reason for reading text — "importing the config would boot a second browser provider
      just to read a list of strings" — is false: `playwright()` returns a descriptor, nothing
      launches until vitest runs the project, and the sibling `src/browser-deps.test.ts` has
      imported the same config from the same node project all along. All four now read
      `config.test.projects[browser].optimizeDeps.include`, which is what Vite actually
      receives and is immune to formatting. That is §9's config-text guard layer coming down
      one file at a time, not a rewrite-to-green.
    - I added a `browser styling pass-through` describe (2 tests) for the new `browserPlugins`
      / `browserSetupFiles` options, then deleted it under Rule 6 before committing. Breaking
      either option is LOUD, not silent — with no stylesheet a ui control measures 0x0 and
      every spec that clicks it hangs to Playwright's actionability timeout — and both apps'
      `shared/testing/browser-styles-wiring.test.ts` already name the wiring at the consumer
      end. A comment in `vitest-projects.test.ts` records the reasoning so the next person does
      not re-add them. `packages/testing` therefore shows **no test-count change** for 3.4.
    - `nodeProjectOverrides` now has **zero consumers** (wallow-web's `openid-client` alias and
      inlined SDK went away long before this plan). Kept — it is a tested public option of the
      preset — but `docs/development/frontend-setup.md` was pointing at it as wallow-web's
      example, which had been stale for months.

### Known future red — RESOLVED in 2.4

`packages/forms/src/core/package-scaffold.test.ts`'s
`it("provides a declaration-only tsconfig.build.json narrowed to the entry")` went red exactly
as predicted when the shared `tsconfig.build.base.json` landed (`guard-test-audit.md` line 118
mapped it). Deleted as part of 2.4, replaced by a comment pointing at the build-output
describe. This was the only Phase-2 red the audit had mapped in advance; the four in
deviation 8 were not.

### Repo state caveat

`main` is **57 commits ahead of `origin/main`** (39 pre-existing + 18 from this work). Per
`CLAUDE.md` → Session Completion, that needs `git pull --rebase && bd dolt push && git push`.


Step-by-step execution of the decisions in `1655-build-simplification-plan.md` (findings and
evidence) and the four source reports (`tsconfig-review.md`, `vite-review.md`,
`vitest-review.md`, `guard-test-audit.md`). Read those for *why*; this file is *how*.

## Rules for this work

1. **Serial, not parallel.** Four agents sharing one tree caused two false failures during the
   review. One phase at a time, one agent at a time.
2. **One commit per step**, using the step's stated conventional-commit message. Steps are sized
   to be individually revertable.
3. **`rm -rf <pkg>/node_modules/.vite` before any cold vitest measurement.** A config-hash change
   is NOT a cold start. This invalidated a review result once already.
4. **Phase 1 gates Phases 2 and 3.** Ten specs assert config source text by regex; touching
   config before retiring them produces red tests that say nothing about correctness.
5. Do not start a phase while the working tree has unrelated uncommitted changes.
6. **Write no new tests.** This is the most important rule in the file.

## Rule 6, expanded: verification is not test-writing

Every "Verify" block in this plan is a command to **run once and read**. It is not a spec to
author. Do not convert any of them into a test file, a new `describe`, or an added assertion in
an existing spec.

The reason is the whole point of this work. The 54 guard specs being retired here were created
exactly this way: someone changed the build, wanted confidence the change was right, and encoded
that confidence as an assertion about config shape. Each one was reasonable in isolation. Together
they made the build unchangeable — 10 of them now have to be deleted before the config can be
touched at all, and at least 5 pass for reasons that no longer exist.

The distinction to hold onto:

| | Question | Answer |
| --- | --- | --- |
| **Verification** | "Did *this* change work?" | Run the command. Read the output. Done. |
| **Test** | "Will this stay true forever?" | Only justified if breaking it is silent AND likely |

Almost everything in this plan is the first kind. `pnpm typecheck` passing with no `dist/`
is a fact about a migration that happens once. Encoding it as a spec that reads `package.json`
and asserts the `exports` map equals an exact object gives you `package-scaffold.test.ts` again.

**If a step's verification fails, fix the code — do not add a test that documents the failure.**
**If a step's verification passes, move on — do not add a test that records the pass.**

### The one permitted test change, and why it is not an exception

Phase 3.3 **moves** `packages/forms/src/core/browser-deps.test.ts` into the shared preset so it
covers every package rather than only forms. That is relocating an existing, load-bearing test to
a wider surface — not authoring a new one. It earns its place under the rule above: an
unresolvable `optimizeDeps` entry is a **warning** in Vite, it silently pre-bundles nothing, and
the dropped entry never reaches the dep-cache hash, so the failure is both silent and
intermittent. Three prior false-greens are documented in its header. That is the narrow case
where a permanent assertion is correct.

Nothing else in this plan gets a new test. If a step seems to need one, that is a signal the
step's verification command is wrong — fix the command.

## Preconditions

- [ ] Working tree clean (`git status`). At time of writing there are in-flight edits from a
      concurrent comment-cleanup/restyle agent — land or stash those first.
- [ ] `pnpm install` current.
- [ ] Baseline recorded: `pnpm build && pnpm test` green, and the timings noted below, so every
      later claim of "no regression" has something to compare against.

Baseline recorded 2026-07-31 (clean tree at `dd49f14a`, warm `dist/`):

| Metric | Value |
| --- | --- |
| `pnpm build` wall time | 6.1s |
| `pnpm test` wall time | 43.4s, exit 0 |
| `pnpm test` file/test counts per package | styles 6/103 · query 4/88 · sdk 43/1134 · auth 4/37 · testing 7/82 · ui 119/1515 · minimal-app 5/27 · forms 17/118 · wallow-auth 66/1367 · wallow-web 128/1098 — **399 files / 5569 tests** |
| Cold `packages/ui` browser+storybook run (after `rm -rf packages/ui/node_modules/.vite`) | 11.4s wall, 119 files / 1515 tests, Duration 9.78s |

---

## Phase 0 — Independent fixes

No prerequisites. Correct regardless of what else is adopted.

### 0.1 Fix the `pnpm check` ordering

**File:** `package.json` (root)

`check` currently runs `format:check && lint && typecheck && test && build && check:exports`.
`typecheck` and `test` both depend on built packages, so the gate fails on a clean clone — and
it silently disarms all six `skipIf(distIsMissing)` specs in
`packages/ui/src/core/dist-structure.test.ts`.

Move `build` ahead of `typecheck`:

```
"check": "pnpm format:check && pnpm lint && pnpm build && pnpm typecheck && pnpm test && pnpm check:exports"
```

**Verify:**
```bash
find . -maxdepth 3 -type d -name dist -not -path "*/node_modules/*" -exec mv {} {}.bak \;
pnpm check          # must reach typecheck/test with packages built
for d in $(find . -maxdepth 3 -type d -name "dist.bak" -not -path "*/node_modules/*"); do mv "$d" "${d%.bak}"; done
```
Confirm the six `dist-structure` specs now RUN rather than skip.

**Note:** Phase 2 removes the prebuilt-`dist` requirement for `typecheck`, but this ordering is
still correct afterwards — `dist-structure.test.ts` needs a real build either way.

**Commit:** `fix(build): run build before typecheck and test in the check gate`

### 0.2 File beads for the two latent bugs

Neither is caused by this work; both were found during it.

- **`apps/examples/minimal-app` duplicate QueryClient.** Same SSR externalization split as the
  two big apps, but unguarded — two react-query graphs in its build. Survives only because
  `sdk-wiring.test.ts:98` forbids `useQuery` in SSR entries. Phase 3.2 fixes it as a side
  effect; the bead records it so the fix is attributable and the guard gap is closed.
- **`use-sync-external-store/shim/with-selector` in the server bundle.** Even with the alias in
  place it emits `__require("react")`, called from 3 chunks. Latent second-React risk.

```bash
bd create "minimal-app ships two react-query graphs (unguarded QueryClient split)" -t bug
bd create "with-selector emits __require(react) into the server bundle" -t bug
```

### 0.3 Delete the vacuous assertions

These can never fail. Deleting them is not a coverage loss.

| File | What to remove |
| --- | --- |
| `packages/ui/src/core/package-scaffold.test.ts:117` | `expect(deps.typescript).not.toBe("6.0.3")` |
| `packages/forms/src/core/package-scaffold.test.ts:193` | same assertion |
| `packages/sdk/src/build-config.test.ts` | the 3 tsup-absence assertions (whole file goes in 1.4 anyway) |
| `packages/styles/src/branding-ownership.test.ts` | whole file |
| `apps/wallow-web/src/styling.test.ts` | whole file |

The typescript-literal assertions are dead because both manifests declare `catalog:tooling` and
`packages/sdk` declares `catalog:tooling-tsc6` — no manifest in the repo can spell `"6.0.3"`.
The invariant they describe is now enforced by the catalog indirection in `pnpm-workspace.yaml`.

**Verify:** `pnpm test` green; test count drops by the expected amount, no file count surprise.

**Commit:** `test: delete assertions that can no longer fail`

---

## Phase 1 — Retire the shape-lock guards

**Gates Phases 2 and 3.** Each step names what it unlocks. `guard-test-audit.md` Part 2 is the
authoritative config→test map; consult it if a later phase hits an unexpected red.

Work in this order — each unlocks strictly more than the last.

| # | File | Action | Unlocks |
| --- | --- | --- | --- |
| 1.1 | `packages/testing/src/browser-optimize-deps.test.ts` | Delete the 3 `browserOptimizeDepsBaseline` describes. **Keep** the 4 `mergeOptimizeDeps` specs. | Editing the shared baseline (propagates to every browser project) |
| 1.2 | `packages/forms/src/core/package-scaffold.test.ts` | Delete the `vite.config.ts`/`vitest.config.ts` source-text specs, incl. `/projects:\s*\[\s*node\s*,\s*browser\s*\]/`. **Keep** the dependency-bucket specs. | 3.3, 3.4; and any reformat of either config |
| 1.3 | `packages/ui/src/core/package-scaffold.test.ts` | Delete the build-shape describes + the `@base-ui/react ^1.6` pin. **Keep** the component-layering and `source.css` `@source` describes. | 3.1; a Base UI minor bump |
| 1.4 | `packages/sdk/src/build-config.test.ts` | Delete the file. | 3.1 (blocked here + 1.2 + 1.3 simultaneously) |
| 1.5 | `packages/sdk/src/oxfmt-config.test.ts` | Delete the file. | Formatter changes; removes a cross-package false-red |
| 1.6 | `packages/ui/src/core/storybook-setup.test.ts` | Keep 2 specs (three-project assertion, headless-Chromium assertion). Delete the other 11. | A Storybook major; `.storybook/` restructure |
| 1.7 | `packages/testing/src/vitest-projects.test.ts` | Loosen `toEqual` → `toContain` on include/exclude. Delete the "not the v3 string" provider spec. | 3.4; a future Vitest major |
| 1.8 | `packages/styles/src/vite.test.ts` | Delete the last two describes only. | `packages/styles` build restructure |
| 1.9 | `packages/testing/src/sdk-seam-exports.test.ts` | Keep the barrel-purity spec. Delete the other 6. | `packages/testing` entry restructure |
| 1.10 | `packages/ui/src/components/list-row/list-row.composition.test.ts` | Remove the literal `"@base-ui/react/use-render"` assertion. | 3.3's glob |

**Do not touch** (verified load-bearing, with measured failures behind them):
`apps/*/server-only-naming.test.ts` — the `*.server.*` naming convention *is* the build's import
protection, and a plainly-named module shipped redis in a 512 KB browser chunk;
`apps/*/docker-workspace-copies.test.ts`; `apps/*/zone-dag.test.ts` (derives from `paths` rather
than pinning it); `packages/forms/src/core/browser-deps.test.ts` (verifier, not pinner — stays
valid even if the list shrinks to empty); the `copyPublicDir` and `importProtection` specs in
`apps/*/brand-assets.test.ts`; `packages/sdk/src/openapi-regen.test.ts`; the binary-run half of
`packages/sdk/src/oxlint-guardrails.test.ts`.

**Verify after each:** `pnpm test` green. **Verify after all:** `pnpm check`.

**Commit:** one per step, `test(<pkg>): stop pinning <thing> by config source text`

---

## Phase 2 — Remove the prebuilt-`dist` requirement

The root-cause fix. Proven end-to-end during review with all 7 `dist/` deleted.

### 2.1 Point `exports` at source

**Files:** the 7 `packages/*/package.json`.

For each, `exports` → `src/` entry points; add `publishConfig.exports` → the current `dist/`
map. Only `sdk` and `styles` are published; the other five are `private: true`, so there is no
publish risk there — but add `publishConfig` uniformly so a future publish can't regress.

pnpm applies `publishConfig.exports` on publish, so consumers of the published tarball are
unaffected.

### 2.2 Add `--configLoader runner` to the apps

**Files:** `apps/wallow-web/package.json`, `apps/wallow-auth/package.json`,
`apps/examples/minimal-app/package.json`.

Without it, Node's ESM resolver — not `tsc` — fails loading the app's own `vite.config.ts`,
which imports `styles/dist/vite.js`. This is the actual single-app-build failure and the reason
project references were rejected: they fix only `tsc`.

### 2.3 Fix the `packages/sdk` tsconfig drift

**Files:** `packages/sdk/tsconfig.json`, `packages/sdk/tsconfig.build.json`.

`tsconfig.json` doesn't extend `tsconfig.base.json` and redeclares everything with different
values. Verified: it compiles clean under full base semantics. Make it extend the base.

Rewrite the `tsconfig.build.json` header — it currently states four things that are false
(claims the package tracks `^5.6.0` and carries "no own pin"; the real peer range is
`>=5.5.3 || >=6.0.0` and the package pins `catalog:tooling-tsc6`).

### 2.4 Extract a shared `tsconfig.build.base.json`

The 7 `tsconfig.build.json` `compilerOptions` blocks are byte-identical. Extract.

**Keep per-package:** `rootDir` (TS7 `error TS5011` without it — verified) and the hand-listed
`include` (load-bearing: it's what keeps test files and configs out of the declaration program
so no stray `.d.ts` leaks into `dist/`).

**Verify Phase 2 — this is the acceptance test for the whole phase:**
```bash
find . -maxdepth 3 -type d -name dist -not -path "*/node_modules/*" -exec rm -rf {} +
pnpm typecheck                                              # must be exit 0, 10/10 projects
pnpm --filter @bc-solutions-coder/wallow-web build           # must be exit 0, full SSR+Nitro
pnpm build && pnpm test && pnpm check:exports                # attw/publint must still pass
```
`check:exports` is the one that proves `publishConfig` is right — it runs against the built
packages and would catch a broken published-consumer view.

**Commit:** `build: resolve workspace packages from source in-repo, dist on publish`

---

## Phase 3 — Consolidate duplicated config

### 3.1 `defineLibraryConfig()` for the 7 package builds

25 code lines are byte-identical across all seven; `query` and `auth` are 100% identical once
comments are stripped. 341 lines → ~40 plus 7 short files.

`packages/ui`'s `componentEntries()` **stays** — pass it as a parameter. Both simpler forms were
tested and fail: dropping it stops `dist/components/<name>/index.js` being emitted, and rolldown
1.1.5 rejects glob entries with `UNRESOLVED_ENTRY`.

**Verify:** `pnpm build`, then confirm `dist/` trees are unchanged vs. the baseline —
`dist-structure.test.ts` is now armed (Phase 0.1) and is the real check.

### 3.2 `wallowApp()` preset for the app configs

Mirror the existing `wallowStyles()` pattern. Hoists the shared SSR block out of wallow-web and
wallow-auth and fixes `minimal-app`'s unguarded split (bead from 0.2).

**Keep** `ssr.noExternal` — with `noExternal: []` you get two react-query graphs and
"No QueryClient set" on `/login`. Note this becomes *more* necessary after Phase 2, not less.
A working alternative is `ssr.external: ["@bc-solutions-coder/query"]` alone (one graph,
identical render) but it costs facade HMR in dev — not worth it.

**Delete** `resolve.dedupe` — removing it produces byte-identical output. The one genuinely
obsolete option found in 25.

**Keep** the `use-sync-external-store` aliases. Deleting from wallow-auth collapses `/login`
from 9895 → 2621 body chars with "Invalid hook call" at `TabsIndicator` in a booted
`.output/server`. wallow-web's copy is prophylactic; keep it as policy.

**Verify:** build each app, boot `.output/server/index.mjs`, load `/login` and a dashboard route,
confirm body size and no hook errors. `pnpm test` does NOT catch this class of bug — vitest
never builds the Nitro bundle.

### 3.3 Replace the hand-maintained pre-bundle lists with a glob

**Files:** `packages/ui/vitest.config.ts`, `packages/forms/vitest.config.ts`,
`packages/testing/src/browser-optimize-deps.ts`, `apps/*/vitest.config.ts`.

`include: ["@base-ui/react/*"]` — 3/3 cold green at 14.8/12.7/13.0s. vite@8.1.4's
`expandGlobIds` matches against the package's exports-map keys; the dep cache `_metadata.json`
shows 45 entries vs the 39 hand-listed, including the root.

**The mechanism stays** — do not empty the lists. Cold with them emptied: ~50% flake, 282s vs
16.4s when it breaks. `@vitest/browser` already sets `optimizeDeps.entries` (dist/index.js:1048)
and `holdUntilCrawlEnd` already defaults true; there is no generic lever left.

Remove the 9 dead entries that currently log `Failed to resolve dependency` and pre-bundle
nothing: 8 of wallow-web's 22 (all seven `@base-ui/react/*` — it doesn't declare the package —
plus `@tanstack/react-query`) and `vitest-browser-react` in `packages/ui` from the shared
baseline.

**Move** forms' existing `browser-deps.test.ts` resolvability check into the preset so it covers
every package instead of only forms, and a dead entry fails loudly. This is a relocation, not a
new test — see Rule 6. It is the only test change in the entire plan.

Then delete the "append your subpath here" ritual from `packages/ui/CLAUDE.md`.

**Verify:** `rm -rf packages/ui/node_modules/.vite` then run 3× cold; all three green, times in
line with baseline. Repeat for forms and both apps.

### 3.4 Absorb the rest into `createVitestProjects`

- Root `resolve` + `extends: true` replaces the per-project repetition. The "`resolve` is PER
  PROJECT" comment is stale — verified green on wallow-auth (66 files / 1367 tests).
- Fold in the 3×-duplicated `wallowStyles()` + `setupFiles` block.
- `nodeTsxSpecs` → a `*.ssr.test.tsx` naming convention. 4 entries, 3 renames; one file already
  uses the convention.
- **Keep** the `node:async_hooks` shim — reproduced exactly
  (`AsyncLocalStorage is not a constructor`, `start-storage-context@1.167.17` at module scope).

Target line counts: wallow-web 134→~45, ui 123→~55, forms 85→~30, wallow-auth 78→~25.

**Verify:** `pnpm test` green across all packages; per-package file/test counts match baseline.

**Commit:** one per sub-step, `refactor(build): …`

---

## Phase 4 — Lint split

Files partition exactly: 836 = 437 source + 399 test/story.

Two constraints — get either wrong and oxlint silently lints **zero** files, which looks like
success:
- oxlint does **not** expand globs in path arguments.
- `ignorePatterns` has no `!` negation.

**Files:** root `package.json`, `.oxlintrc.json`, new `.oxlintrc.tests.json`, new
`scripts/lint-tests.sh`.

- `lint` — source only, tests excluded via `--ignore-pattern`.
- `lint:tests` — explicit paths via `scripts/lint-tests.sh`, config via `.oxlintrc.tests.json`
  using oxlint's `extends`.
- Enable `--vitest-plugin` on the test side. It exists and is enabled nowhere today.

**Verify — the critical check, because the failure mode is a silent pass:**
```bash
pnpm lint        | tee /dev/stderr | grep -qE "Found|files" # confirm a real file count
pnpm lint:tests  | tee /dev/stderr | grep -qE "Found|files"
```
Both must report a **non-zero file count** summing to 836. Then introduce a deliberate violation
on each side and confirm it is caught.

**Commit:** `build: split lint into source and test passes`

---

## Phase 5 — Guard tests → lint rules

Independent of the build work. Sequence last.

~2,500 lines in the B bucket duplicate what a lint rule does better: `catalog-adoption.test.ts`
(523), both `typography.test.ts`, the four `query-facade*.test.ts`, `generated-mutations.test.ts`
(466), `features-api-seam.test.ts` (641), the catalog sweeps.

`apps/wallow-web/.oxlintrc.json` already carries `react/forbid-elements` and a custom plugin
(`tools/oxlint/wallow-lint-plugin.js`). Extending it to `wallow-auth` retires the two largest
app guards.

**Order:** add the rule → confirm it fires on a known violation → *then* delete the test. Never
the reverse.

---

## Rollback

Every step is one commit. Phases 2 and 3 are the only ones that can break a deploy path
(Phase 2 touches `exports`, Phase 3.2 touches the SSR bundle) — for those, verify a booted
`.output/server` before moving on, since no vitest spec covers it.

---

### Deviation 22 — Phase 4 landed without `.oxlintrc.tests.json` or `-c`

The plan's design (a `.oxlintrc.tests.json` selected with `-c`) is measurably wrong for this
repo, so the shape changed while the outcome did not.

- **`-c` disables oxlint's nested-config lookup** (documented, and confirmed with a probe file:
  `react/forbid-elements` fires in `apps/wallow-web/src` without `-c` and is silent with it).
  `packages/ui/.oxlintrc.json` and `packages/forms/.oxlintrc.json` each carry a test/story
  override the root config does not (`unicorn/prefer-query-selector`, `react/jsx-max-depth`,
  `unicorn/error-message`, `func-name-matching`, `unicorn/prefer-number-coercion`,
  `unicorn/prefer-dom-node-dataset`, `no-magic-numbers`) plus `react/jsx-props-no-spreading`.
  Under `-c` all of those come back as errors and the test pass is red on arrival. So the test
  pass passes **no config flag at all** — root plus nested configs apply exactly as they do for
  the source pass. Proved with a two-file probe: `getElementsByClassName` in a
  `packages/query` spec fires `prefer-query-selector`; the same line in a `packages/ui` spec
  does not.
- `extends` would not have rescued it either: oxlint inherits only `rules`, `plugins` and
  `overrides` — **not `ignorePatterns`**. A probe config extending the root enumerated 860
  files against the root's 840, leaking 20 `dist/`/`generated/` files.
- **Rule severities therefore live in the root `.oxlintrc.json`**, in the existing
  `**/*.test.*` + `**/*.stories.tsx` override, next to the other test relaxations. They are
  inert on the source pass because the plugin is off there, and active on the test pass because
  `--vitest-plugin` is on. One file still owns every severity in the repo.
- **The vitest plugin is enabled whole, minus 30 measured opt-outs.** At `--deny-warnings` the
  unfiltered plugin produced **3559** diagnostics, so it was measured rule by rule and the 30
  rules that fire are turned off; the remaining 41 are clean today and now locked in —
  including `no-focused-tests`, `no-disabled-tests`, `valid-title`, `valid-describe-callback`,
  `no-standalone-expect`, `no-commented-out-tests`, `no-conditional-tests`,
  `valid-expect-in-promise`, `hoisted-apis-on-top` and `require-awaited-expect-poll`. Cost: zero
  fixes, per Rule 6.
- Three of the opt-outs are **false positives against real idioms**, not style preferences, and
  are worth knowing before anyone tries to re-enable them: `valid-expect` (all 13 hits are
  "Expect takes at most 1 argument" against Vitest's legitimate `expect(value, message)`
  overload, used deliberately across the repo for named failures); `expect-expect` (assertions
  delegated to a shared helper, e.g. `referencesEveryEmittedVar(mode)` in
  `packages/styles/src/theme-css.test.ts`); `no-conditional-expect` (the
  `if (!isWallowError(value)) { expect.unreachable(...) }` type-narrowing guard in
  `packages/sdk/src/errors.test.ts`). The other 27 are ordinary style disagreements
  (`prefer-expect-assertions` alone accounted for 1968).
- **File list is enumerated, not globbed.** `scripts/lint-tests.sh` derives its 399 paths from
  `oxlint apps packages tools --debug=files`, so discovery and `ignorePatterns` cannot drift
  from the source pass, prints the count, and exits 1 on zero — the plan's silent-pass failure
  mode made loud. Written for bash 3.2 (no `mapfile`), since that is what macOS ships.
- Partition is **441 source + 399 test/story = 840**, exactly the unsplit set (the plan's 836
  predates later specs). Playwright `e2e/**/*.spec.ts` stays on the source side, as before.
- CI: `.github/workflows/js.yml` gained a `Lint tests` step, and `scripts/**` joined its path
  filters — the workflow already ran `check-exports.sh` without triggering on it.
- `lint-staged` still runs plain `oxlint --fix` on staged files with no `--vitest-plugin`, so
  the pre-commit hook enforces the non-vitest half only. The full test pass runs in `pnpm check`
  and CI. Left as is deliberately: the hook lints an arbitrary staged subset, not a partition.

### Deviation 23 — Phase 5.1 + 5.2: what a lint rule could and could not take over

Commit `5641bcf2`. Four disk-sweeping guard specs deleted (1,390 lines):
`apps/wallow-auth/src/catalog-adoption.test.ts` (523),
`apps/wallow-web/src/typography.test.ts` (392), `apps/wallow-web/src/app/typography.test.ts`
(338), `apps/wallow-web/src/shared/components/dashboard-chrome-tokens.test.ts` (137). Plus
`shared/testing/strip-comments.ts` and `__fixtures__/comment-stripper/` (12 files), which had
no other consumer.

- **Order held throughout**: rule added → probe file with a known violation → observed the
  error → then the delete. Every rule below was seen firing on a probe before anything was
  removed, and the probes were removed after.
- **`apps/wallow-auth/.oxlintrc.json` is new** — that app had no per-app config at all. It
  carries wallow-web's `react/forbid-elements` list plus `button` (wallow-web cannot ban it:
  `bff-demo` ships four raw ones deliberately, pinned by its own spec).
- **`wallow/text-heading-variant` is wallow-auth's alone.** wallow-web's headings are not one
  shape (`LandingPage` runs `display`/`title`/`h3`; `bff-demo` deliberately names no variant at
  all), so switching it on there would be inventing a contract, not retiring a test. Recorded
  in the plugin header so the next reader does not "finish the job".
- **`wallow/no-tinted-text` is narrower than the sweep it replaces, on purpose.** The sweep
  banned an alpha modifier on every colour family and then had to name two files back out
  (`SIDEBAR_INVERSION`). Restricting the ban to the `text-` family states the actual design
  argument — tinted COPY is a colour a fork cannot rename; a translucent SURFACE has no opaque
  spelling — and needs no exemption list at all. Its regex prefix must admit `:`
  (`hover:text-primary/80`); the first version stopped at `[a-z-]*` and silently passed every
  prefixed form. Caught by the probe, not by review.
- **Two live violations exist in wallow-auth** (`hover:text-primary/80` in `NotFoundPage` and
  `ErrorPage`), which is why the rule is enabled in wallow-web only — its original scope. Fixing
  those is a design decision, not a test retirement.
- **What did NOT survive as a rule, and why that is right**: "keeps its section headings at the
  subheading scale in <3 named files>" asserted only that the STRING `variant="subheading"`
  appeared somewhere in the file; the detail-page-title, bare-`div`, hand-rolled-field-scale and
  `bg-muted` assertions each named 1–3 files by hand. All were one-time migration checks that
  had already passed. The bff-demo half of `app/typography.test.ts` is strictly weaker than
  `app/routes/bff-demo.test.tsx`, which drives the whole `bff-*` testid set in a real browser.
- **The one contract with no lint expression** — AuthLayout's `<h1>` is `FocusOnNavigate`'s
  route-change focus target and carries `tabindex="-1"` — became a render assertion in
  `auth-layout.test.tsx`. It is the only thing Phase 5 added.
- **The chrome sweep's positive half was already measured**: `DashboardNav.restyle.test.tsx`
  asserts computed styles for the rail, drawer, rows and active state, so "the file contains
  the string `bg-sidebar`" added nothing.
- **Stale references fixed rather than left dangling**: `heading-scale.test.tsx` and
  `ConsentScreen.catalog.test.tsx` both cited `catalog-adoption.test.ts` as the half they could
  not cover; `apps/CLAUDE.md` still said wallow-auth had no config and that extending the gate
  was open work. wallow-web's forbid messages also told developers to write `variant="h1"`,
  which is not a `Text` variant.
- Suites after: wallow-auth 66 files / 1133 tests, wallow-web 125 files / 954 tests,
  `pnpm lint:tests` 395 files, `pnpm check` exit 0.

### Phase 5.3 — the query-facade family, the two wallow-auth data guards, web-shell removal

2,277 lines deleted, 415 added, across nine specs (one removed outright). `pnpm check` exit 0.

- **The import half of every facade guard is lint's already.** Probed first, in the previous
  batch: the root `no-restricted-imports` ban on `@tanstack/react-query` fires in all five
  consumer locations, in `.ts` and `.test.ts` alike (both lint passes run it). So the five
  `query-facade*.test.ts` files lost their file×import-name tables and their disk sweeps and
  kept only what a rule cannot see — the MANIFEST half (react-query declared in no dependency
  bucket, the facade declared `workspace:*`), the HARNESS half (the browser project pre-bundles
  the facade), and the RUNTIME half (copy identity: `facade.QueryClientProvider ===` the one a
  second import resolves to).
- **`packages/forms/src/core/query-facade.test.ts` was deleted whole** — `package-scaffold.test.ts`
  next to it already owned the manifest and pre-bundle assertions, so nothing in it was left
  once lint took the imports.
- **The pre-bundle cases collapsed to presence.** `describeBrowserPreBundleList` in
  `packages/testing` already asserts, for every consumer, that the list is non-empty, that each
  entry's package is declared, and that each entry RESOLVES in a pristine Node child. A per-app
  "react-query is not pre-bundled" case restated a manifest fact the same file checks better.
- **wallow-auth's data boundary became two rules**, then the tests came out:
  `no-restricted-imports` under `src/features/**` + `src/app/routes/**` banning
  `@bc-solutions-coder/sdk/query` outright and the three raw data operations by name, and
  `wallow/no-hand-rolled-mutation` (fourth plugin rule) reporting any `mutationFn` property.
  Both probe-verified before a line was deleted. Two oxlint limits shaped the design and are
  worth knowing: there is **no `no-restricted-syntax`** (hence a JS plugin rule for `mutationFn`)
  and **no `excludedFiles`** (hence the seam exemption is a LATER override turning the rule
  `"off"`, and since an override REPLACES the base entry it restates every root ban it wants).
- **`features-api-seam.test.ts` (641 → 146) stopped being a table.** `DATA_CONSUMERS`,
  `RAW_DATA_OPERATIONS` and `NO_SEAM_FEATURES` are gone; the seams are now DERIVED — whatever
  `features/*/api.ts` files exist — and each is checked for a co-located `api.test.ts`, for
  being a re-export and nothing else, for naming a non-empty surface, and for reaching only the
  SDK's two published entries. A new feature is covered the moment it is written.
- **`generated-mutations.test.ts` (466 → 142) kept only runtime fact**: each factory hands back
  `{ mutationFn }` and nothing else (which is why a screen with its own `onSuccess` must spread
  it), the magic-link GET gets an Options factory with a `queryFn` and a key, and the two
  behaviour cases proving the deleted `retry: false` overrides were inert. No `retry: false`
  lint rule was invented — a re-added override would be a no-op, which is exactly what those
  two cases state.
- **The `accountVerifyMagicLinkMutation` absence needed no replacement.** The deleted source
  scan guarded against naming a factory the generator never emits — which does not compile.
  Three comments citing that scan were corrected rather than left dangling.
- **`web-shell-removal.test.ts` (766 → 229), one deviation from the bead.** The bead said reduce
  to ~20 and drop the Dockerfile lock; the fixture tree and both text sweeps went (the
  "nothing resolves it" lock is the root lint ban, pinned by
  `packages/sdk/src/oxlint-guardrails.test.ts`), but the workspace-residue cases and the
  Dockerfile reverse check stayed. They are cheap, fully derived from disk, and nothing else
  owns them — `docker-workspace-copies.test.ts` asserts the FORWARD direction (every declared
  dep is copied), not that a copied directory still exists. The PROSE sweep is the deliberate
  loss: a stale docs row is a broken instruction for a fork, but not one worth a scanner that
  models which files are allowed to say the word.
- Suites after: wallow-auth 66 files / 1011 tests, packages/query 4 / 66. `pnpm check` exit 0.
