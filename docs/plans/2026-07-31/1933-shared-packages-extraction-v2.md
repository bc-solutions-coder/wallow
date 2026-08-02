**status: active**

# Shared Package Extraction — v2 (supersedes the 2026-07-30 slice plan)

Replaces `docs/plans/2026-07-30/1347-shared-packages-extraction.md`. That file was written
against a tree that no longer exists: three of its stated premises are now false and one of its
package names collides with a package that already shipped. Mark it
`**status: superseded**` and work from this file.

**Design ancestor:** `docs/plans/2026-07-30/1201-shared-packages-and-app-zones-design.md`
(still valid for intent; its *names* and *prerequisite checks* are not).

---

## Part A — what changed under the old plan

Every item here was verified against the working tree, not recalled.

### A1. `packages/config` already exists, and it is something else entirely

The old Slice 2 proposed `@bc-solutions-coder/config` for **env/config validation**. A package
of that exact name already ships: `packages/config` is the **Vite preset** package
(`./vite/library`, `./vite/app`), consumed by all seven library builds and all three apps'
`vite.config.ts`.

It is deliberately unbuildable and unpublishable — its `exports` map points at `src/`
permanently, it has no `build` script, no `dist/`, no `publishConfig`, and
`scripts/check-exports.sh` correctly omits it. `packages/config/CLAUDE.md` explains why
(a build would be circular: every `packages/*` build runs `vite build` against a config that
imports it). It also forbids relative imports between its own modules and forbids adding an
app-shaped dependency, because `@bc-solutions-coder/styles` depends on it and that would cycle.

**Consequence:** the env package needs a different name, and it cannot be folded into this one.

> **Decision: name it `@bc-solutions-coder/env` (`packages/env`).**
> `config` is taken and means "build configuration" here. `env` says what it validates. It is a
> normal built + published package, unlike `packages/config`.

So the answer to "is config already done?" is **no** — nothing of the old Slice 2 exists. What
exists is an unrelated package wearing the name.

#### A1a. `base-path.ts` goes to `packages/env`, as its own `./base-path` subpath

`packages/env` is being built regardless, and keeping every "how is this app configured"
derivation in **one** package beats splitting `base-path.ts` off into the Vite-preset package and
asking every future reader to remember the split. `packages/config` stays exactly what it is: the
Vite presets, never built, never published, **zero runtime footprint** — its CLAUDE.md invariant
(*"Nothing imports it at runtime, so there is nothing for a bundle to contain"*) stays true, which
is worth more than the small wiring saving of putting `base-path` there.

**This is not free, and the constraint is load-bearing.** `apps/wallow-auth/vite.config.ts`
imports the module **at config load time**, evaluated as plain Node ESM before any app bundle
exists. Three hard requirements follow:

1. **`./base-path` is its own subpath with zero dependencies and no side effects.** It must not
   pull in zod, the env schema, or anything server-only. If importing it can trigger the package's
   fail-loudly-at-boot validation, then `vite build` starts failing on a missing *runtime* env
   var — exactly backwards, since the boot check exists to fail at startup. Subpath isolation is
   what prevents that, and it is the same reason `packages/config` deliberately has no barrel.
   **Verify it with a spec:** importing `@bc-solutions-coder/env/base-path` in a bare Node context
   with no environment variables set must not throw.
2. **`packages/env` must be built before wallow-auth's Vite config loads.** A real build-order
   edge — the same one `packages/sdk` already has (apps typecheck against `dist/`), so a known
   pattern rather than a new hazard, but it must be reflected in the build order and the
   Dockerfile.
3. **The module stays pure string work for all three of its contexts** — `vite.config.ts` (plain
   Node ESM, where `import.meta.env` does not exist at all, hence the optional read behind
   `BASE_PATH`), the SSR bundle (`start.ts`, the passthrough wrapper), and the client bundle
   (`router.tsx`). No `node:` imports, no `process.env` reads outside the config's own call.

Note what `base-path` is *not*: `BASE_PATH` reads `import.meta.env?.BASE_URL`, a Vite value, so
there is nothing for zod to validate and nothing to fail at boot over. It shares a package with
the env contract for cohesion, not because it is the same kind of thing — which is exactly why it
needs its own dependency-free subpath rather than a spot in the main entry.

### A2. `aliases.ts` was never adopted — `tsconfig.json` is the single declaration site

The old plan's prerequisite gate runs `test -f apps/wallow-web/aliases.ts`. That file does not
exist in either app and never landed.

The zone aliases are declared **once**, in each app's `tsconfig.json` `compilerOptions.paths`:

```jsonc
"paths": {
  "@app/*":      ["./src/app/*"],
  "@features/*": ["./src/features/*"],
  "@shared/*":   ["./src/shared/*"]
}
```

Vite reads it via `resolve.tsconfigPaths`, vitest reads it per project, and the `wallow/zone-dag`
lint rule reads it to derive which prefixes it polices. `moduleResolution: "Bundler"` resolves
these relative to that file, so there is no `baseUrl` — and `paths` must **not** move to
`tsconfig.base.json`, which would resolve them against the repo root. Adding a zone is that one
edit and nothing else.

### A3. `zone-dag.test.ts` is retired — the DAG is an oxlint rule now

The old gate runs `test -f apps/wallow-web/src/zone-dag.test.ts`. Both apps' zone-DAG specs were
retired onto the **`wallow/zone-dag`** rule in `packages/lint`, registered by each app's
`.oxlintrc.json` under `jsPlugins`.

**This changes how two of the old plan's tasks are done:**

- Slice 4 said "drop `stores` from the `shared/` subdirectory allowlist in `zone-dag.test.ts`."
  That allowlist now lives in `packages/lint/src/rules/zone-dag.ts`, and the fixture suite
  (`packages/lint/fixtures/zone-dag/`, driven by `packages/lint/src/fixtures.test.ts`) is what
  proves the change. Editing a rule is a `packages/lint` change with its own fixture, not an
  app-spec edit.
- Any acceptance criterion phrased as "the zone-DAG spec passes" becomes "`pnpm lint` passes and
  the `packages/lint` fixture suite covers the new allowlist shape."

### A4. `packages/lint` is a ninth workspace member the old plan never saw

It landed on the current branch (`feat/oxlint-plugin-package`) and is absent from the old plan's
repo map and from its "what Slice 0 established" briefing. Any agent briefed from the old file
will not know the rule seam exists.

### A5. `ready-indicator` is already rehomed — do not re-extract it

Both apps' `shared/components/ready-indicator.tsx` are 12-line bindings over
`ReadyIndicator` from `@bc-solutions-coder/ui`, each supplying its own testid
(`web-ready` / `auth-ready`). That is the correct end state, not duplication. Leave both.

### A6. `check:exports` does not cover a new package by default

`scripts/check-exports.sh` names its packages explicitly:
`(packages/auth packages/query packages/sdk packages/styles packages/testing)`.
The old plan's acceptance criterion "`pnpm check:exports` is clean" is **vacuously true** for a
new package until it is added to that array. Every scaffold task must add it, and the verifier
must check the array, not just the command's exit code.

### A7. Untracked cruft that will confuse a grep

`apps/wallow-web/src/routes/` and `apps/wallow-auth/src/routes/` still exist on disk but are
**untracked** (`git ls-files` → 0) — orphaned `__screenshots__` baselines left behind when
`src` moved into zones; the live baselines are under `src/app/routes/__screenshots__/`. Both
apps' `.output/` build artifacts also match every source grep. Delete the stale `src/routes/`
dirs, and scope greps to `src/` excluding `.output/`.

### A8. Old blocker list — current status

| Old blocker | Status now |
| --- | --- |
| 1. wallow-auth has no CSRF token | Still resolved, as the old file says. Carry its "Log ingest security model" section across verbatim. |
| 2. `navigation` dependency list stated two ways | **Resolved — A9 below.** The design doc is right on every disputed point; the old plan is wrong on every one. No longer blocks. |
| 3. `site-links.ts` — `navigation` or `styles`? | **Resolved below** (Part B): `styles`. Note this **overrides** the design doc, which assigns it to `navigation` — it is not merely an open question being closed. Its consumers are `PublicLayout` and `LandingPage`, neither of which is navigation, and neither moves into that package. |
| 4. `minimal-app` / `scripts/fork-smoke` scope | **Still open**, and now sharper: `minimal-app` holds the **third** copy of `request-origin.ts`, so the `env` slice has to decide it rather than defer it. |

### A9. Reconciling `packages/navigation`'s dependency list — closes old blocker 2

The two source documents disagree. Resolved by reading the actual imports of the five modules
that move (`DashboardNav.tsx`, `DashboardLayout.tsx`, `nav-icons.ts`, `use-is-desktop.ts`,
`stores/ui-store.ts`) rather than by preferring one document:

| Edge | Design doc | Old plan | Verdict | Evidence |
| --- | --- | --- | --- | --- |
| `@bc-solutions-coder/ui` | ✅ | ✅ | **dependency** | Four per-component subpath imports: `/error-banner`, `/navigation-menu`, `/theme-toggle`, `/button`. |
| `lucide-react` | ✅ | ❌ omitted | **dependency** | `nav-icons.ts` imports eight named icons. Its header already documents *why* that library: per-icon ES exports, no icon font, no runtime deps, peers React 19. |
| `zustand` | ✅ | ✅ | **peerDependency** at `catalog:react`, plus devDependency at the same range | The store is a module-global singleton (see the standing decision). |
| `react` (+ `react-dom`) | ✅ explicit | ❌ unstated | **peerDependency** | Hooks throughout. `react-dom` mirrors `packages/ui`'s shape for a React component package. |
| `@tanstack/react-router` | ✅ explicit | ❌ unstated | **peerDependency** at `^1.170.18` | `Link` / `LinkProps` in `DashboardNav`, `Outlet` in `DashboardLayout`. A library peering `^1.170.18` against an app pinning `1.170.18` exactly is correct practice, and stays a literal — not `catalog:start`. |
| `@bc-solutions-coder/styles` | ❌ absent | ✅ claimed | **no edge** | Nothing in the five modules imports it. `packages/ui` already depends on it, and the tokens arrive as CSS through the app's stylesheet, not as a JS import. The old plan invented this edge. |
| `@bc-solutions-coder/utils` | ❌ absent | ✅ claimed | **no edge** | Nothing in the five modules calls anything utils-shaped. The old plan asserted the edge before the package existed. Add it only if the migration turns up a real call site. |
| `@bc-solutions-coder/auth` | ❌ | ❌ | **no edge** | Both agree. Visibility is an app-supplied `can()` prop. |
| `@bc-solutions-coder/sdk` | ❌ | ❌ | **no edge — but the code has one today** | `DashboardNav.tsx:1` imports `logout` from `@bc-solutions-coder/sdk`. The standing "logout control is a footer slot" decision is what removes it. **This is a migration task, not a declared dependency** — an implementer who reads the import and adds `sdk` to the manifest has silently reversed a decision. Call it out on the bead. |

**Resulting manifest:**

```jsonc
"dependencies":     { "@bc-solutions-coder/ui": "workspace:*", "lucide-react": "^0.5xx.x" },
"peerDependencies": { "react": "catalog:react", "react-dom": "catalog:react",
                      "@tanstack/react-router": "^1.170.18", "zustand": "catalog:react" },
"devDependencies":  { /* the four peers again, at the same ranges */ }
```

The design doc wins on all four disputed points. Where the two disagree elsewhere, check the
imports before believing either.

---

## Part B — the rehome inventory (the second half of the ask)

Every file in both apps' `shared/` zones, with a destination and the evidence for it. This is
the part the old plan left as "Slice 5, decompose it last from a scout report." It does not need
to be last — most of it is decidable now, and deciding it now is what shrinks `shared/` instead
of shuffling it.

### B1. Duplicated across apps — extract because the copies drift

| Module | Copies | Evidence | Destination |
| --- | --- | --- | --- |
| `request-origin.ts` | **3** — wallow-web, wallow-auth, `apps/examples/minimal-app/src/lib/` | The three source files are **byte-identical**. Their own spec header admits it: *"The helper is a verbatim copy per Start app, so the drift guard lives here too"* and *"the guard keeping all three copies byte-identical."* A hand-written drift guard across three copies is the shape that says "this is a package." | **`packages/env`** — it composes with `withBasePath`, which is the same slice. |
| `browser-styles-wiring.test.ts` | 2 | Same three-part assertion (plugin pair, virtual theme stylesheet, no JS import of the styles package); differs only in each app's extra cases. | **`packages/testing`**, as an exported **factory** (`assertBrowserStylesWiring({ appDir, extraSpecs })`), not a moved file. The app-local cases stay in the app. |
| `theme-wiring.test.tsx` | 2 | Same probe; differs only in the token list (`--sidebar` vs `--card`) and the probe class (`bg-sidebar` vs `bg-card`). | **`packages/testing`**, as `assertThemeWiring({ tokens, probeClass })`. |

`request-origin` is the strongest single item in this document: three identical copies, a
security-relevant scheme-sanitisation path (the web copy's spec covers `javascript:`,
`https://evil.example`, embedded-credential and whitespace-injection cases that the auth copy
does **not**), and a comment that already concedes the duplication.

### B2. Single-app today, but library-shaped — extract because a fork re-derives them

| Module | Lines | Consumers | Destination | Why |
| --- | --- | --- | --- | --- |
| `base-path.ts` (auth) | 114 | `vite.config.ts` **at config load**, the SSR bundle, the client bundle | **`packages/env`**, as a dependency-free `./base-path` subpath *(A1a)* | One package owns config derivation. The subpath must carry no zod and no side effects, or a Vite config importing it trips the fail-at-boot validation during `vite build`. |
| `error-text.ts` (web) | 29 | 9 feature components across organizations, inquiries, settings, apps, mfa | **`packages/forms`** | `forms` already owns the RFC 7807 error split. Nine call sites in one app is a shared concern that wallow-auth will grow the moment it renders a server error. |
| `branding.ts` (auth) | 32 | `app/routes/__root.tsx` | **`packages/styles`** | `styles` owns `branding.json` and the canonical `ForkBranding` type. A fork rebrands by editing one file; a per-app branding reader defeats that. |
| `site-links.ts` (web) | 26 | `PublicLayout`, `LandingPage` (+ both specs) | **`packages/styles`** — *closes old blocker 3* | Neither consumer is navigation. The links are fork identity (repo URL, docs URL), which is exactly what `branding.json` already holds. It rides with `branding.ts` above. |
| `page-container.ts` (web) | 19 | 7 route modules + `style-contract.ts` | **`packages/ui`** | Layout tokens for the dashboard shell. `ui` owns the recipes; a second place that decides page width is the thing `wallow/no-tinted-text` and friends exist to prevent. |
| `SelectControl.tsx` (web) | 123 | 1 (`InquiryDetail`) | **absorb into `packages/ui`'s `Select`** — *decided, see below* | Already a pure composition of `Field`, `Label` and `Select` from `ui`, with zero app-specific content. |
| `use-is-desktop.ts` (web) | 56 | `DashboardLayout`, `DashboardNav` only | **`packages/navigation`** | Both consumers move in that slice; this moves with them. It is not a general shared hook. |
| `catalog-select.ts` (web) | 62 | browser specs | **`packages/testing`** | Drives `ui`'s portalled `role="option"` divs — a `ui` fact, not an app fact. `.claude/rules/TESTING.md` already documents `chooseOption` as the canonical way to drive a catalog `Select`. |
| `invalidation.ts` (web) | 40 | invalidation specs | **`packages/testing`** | Runs the SDK's real `invalidations` predicates against real generated query keys — a `sdk`/`query` fact every app needs. |
| `node-async-hooks-browser-shim.ts` (web) | 54 | browser project setup | **`packages/testing`** | Browser-project plumbing; `packages/testing` already owns `browser-deps` and `vitest-projects`. |
| `harness-routes.ts` (web) 127 · `harness.ts` (auth) 20 | | specs in both apps | **`packages/testing`** | `packages/testing` already exports `render-with-wallow` and `sdk-harness`. These are the last app-local pieces of the same harness. |

#### `SelectControl` → absorbed into `packages/ui`'s `Select`

Its own header makes the case: *"This is a COMPOSITION, not a reimplementation: every element
below comes from `@bc-solutions-coder/ui`."* It imports exactly `Field`, `Label` and `Select`
from the catalog and adds nothing app-specific. It exists because a usable select is a
seven-part portal tree (`Root > Trigger > Value/Icon`, plus `Portal > Positioner > Popup > List
> Item > ItemText`) that every call site needs identically — which is an argument for the
catalog owning it, not for each app re-deriving it.

Absorb it as `Select.Control` (or a `SimpleSelect` sibling — namer's call) in
`packages/ui/src/components/select/`, preserving all four behaviours its header documents,
because each is a bug that was already fixed once:

1. **`testId` lands on the TRIGGER**, since that is the element the E2E suite and the component
   specs click.
2. **The `""` ↔ `null` translation** — "nothing chosen" is `""` on the app side (TanStack Form's
   default for a required select) and `null` in Base UI.
3. **`items` makes the trigger report the LABEL, not the value.** Without it Base UI's
   `Select.Value` renders the raw value and a `web-app` / "Web Application" pair shows the wire
   value to the user.
4. **`label` stays REQUIRED.** The trigger is a `role="combobox"` button with no text content of
   its own, so without an explicit name a screen reader announces it as unlabelled.

Two things to carry across with it: the component must stay split one-per-nesting-level (it was
written that way to stay inside the repo's `react/jsx-max-depth` budget at every call site), and
its render coverage becomes a **story**, per `packages/ui`'s pattern — the `storybook` vitest
project is the only one there that loads Tailwind and the fork theme, and a portalled
`role="option"` tree with no stylesheet measures 0×0 and hangs every click to Playwright's ~15s
actionability timeout.

Drive it in specs with `chooseOption` (`.claude/rules/TESTING.md`); `userEvent.selectOptions`
only drives an `HTMLSelectElement` and this is not one.

### B3. Stays in the app — do not extract

| Module | Why it stays |
| --- | --- |
| `ready-indicator.tsx` (both) | Already a 12-line binding over `ui`'s `ReadyIndicator`. Correct as-is (A5). |
| `api-passthrough.server.ts` (auth, 84) | App-specific mounting glue over `@bc-solutions-coder/sdk/server/passthrough`, which is already the package. Extracting the glue extracts nothing. |
| `auth-layout.tsx` (auth, 114) · `PublicLayout.tsx` (web, 138) | App shells. They compose `ui` primitives for one app's identity; a shared "layout" package is the coupling the fork-first model is trying to avoid. Revisit only if a third app appears. |
| `DashboardLayout.tsx` · `DashboardNav.tsx` · `nav-icons.ts` · `stores/ui-store.ts` | Owned by the navigation slice, not by the rehome pass. |

### B4. Delete, do not rehome

`style-contract.ts` (web, **288 lines**, 12 consuming specs) is the single largest file in
either `shared/` — and `.claude/rules/TESTING.md` names it directly as something to delete on
sight:

> *"So is any `it()` whose body only reads `element.classList` (the `shared/testing/style-contract.ts`
> helpers): a component can render the right classes and be broken, or restyle correctly with
> different classes and fail. Assert the computed value, never the class string."*

Moving it into `packages/testing` would promote a banned pattern into shared API and hand it to
every fork. Its 12 consumers convert to computed-value assertions, or the assertion is dropped
where a `wallow/*` lint rule already says the same thing by construction. **This is its own
task, and it should run early** — it is the biggest single reduction of `shared/` in this
document and it unblocks nothing, so it can run in parallel with everything else.

### B5. What `shared/` looks like when this lands

- **wallow-auth `shared/`**: `components/{auth-layout, ready-indicator}`, `lib/api-passthrough.server.ts`.
  `lib/` loses three of four modules; `testing/` empties entirely.
- **wallow-web `shared/`**: `components/{PublicLayout, ready-indicator}` (+ `SelectControl` if it
  is not absorbed). `components/` loses the nav trio, `lib/` empties, `stores/` is deleted,
  `testing/` empties.

Both `stores/` and `testing/` leaving means **two** entries drop out of the `shared/`
subdirectory allowlist in `packages/lint/src/rules/zone-dag.ts` — the old plan only knew about
`stores`. Drop them in the same change that empties them, with a fixture, or the rule goes stale
in the permissive direction.

---

## Part C — revised slice list

Renumbered. The old Slice 5 ("remaining rehomes, decompose last") is gone: Part B decided it, so
its content is distributed into the slices that own each destination.

```
  utils (S1) ──┬──> env (S2)         [request-origin ×3 + boot env reads]
               ├──> logger (S3)
               └──> navigation (S4)  [ui + lucide-react; absorbs use-is-desktop]

                       S2 ──> S2b   base-path -> packages/env ./base-path

  independent, run any time ──> S0   style-contract deletion
                               S5   testing consolidation
                               S6   styles/forms/ui rehomes
```

| Slice | Package / target | Content | Depends on |
| --- | --- | --- | --- |
| **S0** | — | Delete `style-contract.ts`, convert its 12 consumers to computed-value assertions (B4). Delete the untracked `src/routes/` cruft (A7). | nothing |
| **S1** | `packages/utils` | Unchanged from the old plan — same five subpaths (`./format`, `./string`, `./array`, `./result`, `./guards`), same machine-enforced charter. | nothing |
| **S2** | **`packages/env`** *(renamed — A1)* | Schema-validated **server-side** env contract that fails loudly at boot: `WALLOW_API_INTERNAL_URL` and the other boot reads, plus all three `request-origin.ts` copies. Must resolve old blocker 4 (`minimal-app` holds the third copy). | S1 |
| **S2b** | `packages/env` | `base-path.ts` → a dependency-free, side-effect-free `./base-path` subpath, with a spec proving a bare-Node import throws nothing (A1a). Reflect the new build-order edge (env before wallow-auth's Vite config) in the build order and the Dockerfile. | S2 |
| **S3** | `packages/logger` | Unchanged, **including its "Log ingest security model" section verbatim** — origin allowlist, payload caps, per-IP rate limit, server-side stamping in both apps; CSRF for wallow-web only. | S1 |
| **S4** | `packages/navigation` | Manifest per A9 (`ui` + `lucide-react`; peers `react`, `react-dom`, `@tanstack/react-router`, `zustand`; **no** `styles`, `utils`, `auth` or `sdk` edge). Absorbs `use-is-desktop.ts`. Drops `DashboardNav`'s `logout` import onto a footer slot. The `shared/` allowlist edit is a `packages/lint` rule + fixture change (A3). **Unblocked** (A9). | S1, ui, zustand |
| **S5** | `packages/testing` | `catalog-select`, `invalidation`, `node-async-hooks-browser-shim`, `locators` (S0's survivor), both harnesses; the two wiring specs as factories (B1). | S0 |
| **S6** | `styles` / `forms` / `ui` | `branding.ts` + `site-links.ts` → styles; `error-text.ts` → forms; `page-container.ts` → ui; **`SelectControl` absorbed into `ui`'s `Select`** with a story for render coverage (B2). | S4 for the `page-container` half only |

S1 is no longer on the critical path for as much as it was: S0, S2b and S5 all start immediately,
and S2b is small enough to land on its own.

### Serialization hazard — unchanged and still real

Every "wire into both apps" task edits the same four files (`apps/wallow-web/package.json`,
`apps/wallow-auth/package.json`, both `Dockerfile`s), and every docs task edits `docs/toc.yml`
plus the repo-map tables in `CLAUDE.md` and `apps/CLAUDE.md`. Chain those tasks so exactly one
runs at a time, even though their parent slices run in parallel.

### Acceptance criteria for every new package — corrected

- `pnpm --filter @bc-solutions-coder/<pkg> build` succeeds.
- **The package is added to the `packages=(…)` array in `scripts/check-exports.sh`**, and
  `pnpm check:exports` is clean. Without the array edit that check passes vacuously (A6).
- No literal versions for anything the catalogs cover — `catalog:react`, `catalog:start`,
  `catalog:tooling` (and `catalog:tooling-tsc6` for `packages/sdk` only).
- `apps/*/src/docker-workspace-copies.test.ts` passes in both apps — the guard for the two
  Dockerfile COPY lines per consuming app.
- `pnpm lint` **and** `pnpm lint:tests` pass; if the change touched `shared/`'s shape, the
  `packages/lint` fixture suite covers the new allowlist.
- `pnpm check` green from a clean install.
- Docs guide exists, is in `docs/toc.yml`, and the repo-map tables in `CLAUDE.md` and
  `apps/CLAUDE.md` name the package — **including `packages/lint`, which is missing today** (A4).

---

## Briefing for any agent working from this file

The old plan's "What Slice 0 established" list is **half wrong**. Corrected:

1. **Both apps are three-zone** — `src/app/`, `src/features/<name>/` (each with an `index.ts`
   barrel), `src/shared/` (`components`, `lib`, `stores`, `testing`, `types`). Root-level
   `src/*.test.ts(x)` are app-wide policy specs. *(unchanged)*
2. **Cross-zone imports use `@app/*`, `@features/<name>`, `@shared/*`, declared once in each
   app's `tsconfig.json` `paths`.** There is no `aliases.ts`. *(corrected — A2)*
3. **The import DAG is the `wallow/zone-dag` oxlint rule in `packages/lint`**, not a spec.
   Its `shared/` allowlist is in `src/rules/zone-dag.ts` and is proven by
   `packages/lint/fixtures/zone-dag/`. *(corrected — A3)*
4. **Version pins come from pnpm catalogs**, never literals. *(unchanged)*
5. **`.github/workflows/sdk-publish.yml` runs `pnpm publish --no-git-checks`**, which resolves
   both `catalog:` and `workspace:*` at pack time. *(unchanged)*
6. **A co-move leaves its specifier untouched** — when both ends of a relative import move by
   the same amount, the specifier does not change. *(unchanged; shipped as a bug twice)*
7. **The vitest two-project split is keyed on file EXTENSION** — a `.test.ts` can never run in
   Chromium; a browser spec must be `.test.tsx` and cannot use `node:fs`. *(unchanged)*
8. **`packages/config` is the Vite-preset package and is off-limits** — never built, never
   published, no relative imports between its modules, no app-shaped dependencies. The env
   package is `packages/env`. *(new — A1)*

### Decisions from the old plan that still stand — do not reopen

`utils` subpath names · the `sendBeacon` body-carried CSRF token on the BFF path only · log
ingest guarded by origin + caps + per-IP rate limit + server-side stamping in both apps ·
`ui-store.ts` moving wholesale as `useNavStore` with the `TWO AXES, NOT ONE` comment carried
verbatim · `shared/stores/` deleted with **no** pass-through re-export · `zustand` as a
peerDependency at `catalog:react` · `packages/utils`' empty-dependency charter ·
`DashboardNav` importing `@bc-solutions-coder/ui` by per-component subpath, never the barrel ·
nav testids from `testIdPrefix` + `id` defaulting to `"dashboard"` · `packages/navigation` has
no `auth` and no `sdk` edge.

### New decisions this document makes

| Decision | Why |
| --- | --- |
| **The env package is `packages/env`, not `packages/config`** | The name is taken by a package that cannot absorb the server-only, zod-carrying, fail-at-boot half (A1). |
| **`base-path.ts` → `packages/env`, as a dependency-free `./base-path` subpath** | One package owns config derivation; `packages/config` keeps its zero-runtime invariant intact. The subpath must carry no zod and no side effects, because `wallow-auth`'s Vite config imports it at config load time and must not trip the fail-at-boot validation (A1a). |
| **`packages/navigation` depends on `ui` + `lucide-react`; peers `react`, `react-dom`, `@tanstack/react-router`, `zustand`; has NO `styles` and NO `utils` edge** (closes old blocker 2) | Read from the actual imports of the five moving modules. The design doc was right on all four disputed points; the old plan invented the `styles` and `utils` edges (A9). |
| **`DashboardNav`'s `logout` import is removed, not declared** | The code imports `logout` from `@bc-solutions-coder/sdk` today. The standing "logout is a footer slot" decision is what deletes it; adding `sdk` to the manifest silently reverses that decision (A9). |
| **`SelectControl` is absorbed into `ui`'s `Select`, not moved as a file** | It is already a pure composition of `Field`/`Label`/`Select` with no app-specific content. Its four documented behaviours are each a fixed bug and must survive (B2). |
| **`site-links.ts` → `packages/styles`** (closes old blocker 3) | Its consumers are `PublicLayout` and `LandingPage`; neither is navigation and neither moves into that package. This **overrides** the design doc, which assigned it to `navigation`. |
| **`style-contract.ts` is deleted, not moved** | `.claude/rules/TESTING.md` bans the pattern by name; moving it would promote a banned pattern into shared API for every fork (B4). |
| **The two wiring specs become `packages/testing` factories, not moved files** | Their shared half is identical; their differing half is genuinely app-local (token list, probe class). |
| **`use-is-desktop.ts` rides with `packages/navigation`** | Its only two consumers are `DashboardLayout` and `DashboardNav`. |
| **`auth-layout.tsx` and `PublicLayout.tsx` stay in their apps** | App shells composed from `ui` primitives. A shared layout package couples every fork's identity to this one. |

---

## Reminders

- `docs/plans/` is gitignored. Never `git add` this file.
- Mark `docs/plans/2026-07-30/1347-shared-packages-extraction.md` **superseded** rather than
  deleting it — its logger security model and its decision table are the source for this one.
- Run `pnpm --filter @bc-solutions-coder/sdk build` before typechecking an app.
- Conventional Commits, lowercase, imperative, no trailing period, first line < 72 chars. A new
  package is `feat(<pkg>):`. These slices **do** cut releases.
- The current branch (`feat/oxlint-plugin-package`) is where `packages/lint` landed. Land it
  before S0, so `wallow/zone-dag` is the allowlist seam every later slice edits.
- Work is not complete until `git push` succeeds (`CLAUDE.md`). Shut each agent down as soon as
  its role is complete (`.claude/rules/TEAMS.md`).
