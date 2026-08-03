# App wiring test consolidation

**status: active**

Consolidate the policy specs at the top of `apps/wallow-web/src/` and `apps/wallow-auth/src/`
into one node spec and one browser spec per app, delete the assertions another tool already
owns, and share the two guards that were copy-pasted between the apps.

## The problem

Twenty-one files and roughly 1,700 lines sit directly under the two apps' `src/`. They are
not co-located with anything, they are the first thing a reader sees in the tree, and three
distinct problems are tangled together in them:

- **Duplication.** `brand-assets.test.ts` and `browser-mode-smoke.test.tsx` exist twice each,
  byte-for-byte apart from prose and one `describe` name — 233 lines of copy-paste. Every
  other cross-app guard in this repo is already a thin delegation into
  `@bc-solutions-coder/testing`; these two never got the treatment.
- **Redundancy.** Roughly 560 lines assert what pnpm's strict `node_modules`, TypeScript, and
  the `wallow/*` lint rules already enforce. The files say so themselves: `query-facade.test.ts`
  concedes that lint owns the import ban and pnpm owns the manifest half, then spends twenty
  lines on `expect(typeof useQuery).toBe("function")`. A missing export cannot reach that
  assertion — it is a load-time error, so the `import` is the test and the `expect` is ceremony.
- **Coverage that moved.** `heading-scale.test.tsx` mounts sixteen screens across the two apps,
  680 lines, to measure that `CardTitle` and `Text variant="subheading"` land on the same
  20px step. `packages/ui` now measures exactly that claim in `card.stories.tsx` and
  `text.stories.tsx`, with `text-lg`/`text-xl`/`base` probes, in the storybook project that has
  the real Tailwind pipeline and fork theme attached.

## Constraint: these files cannot move

`wallow/zone-dag` classifies a file whose `src/`-relative path is a single segment as
`ROOT_ZONE` and exempts it from every zone check. Anything deeper is judged as a product
module, and `escapesSrc` fires on an import resolving outside `src/`:

> `../../vitest.config` resolves outside `src/`. That is legal only from a root-level policy
> spec, which exists precisely to assert things about `vite.config.ts` and the app manifest.

Verified empirically with throwaway probes: a file at `src/` importing `../vitest.config`
draws zero zone-dag errors; the same import from `src/wiring/` or `src/app/wiring/` errors.
Nine of these specs import `../vite.config` or `../vitest.config`, so any subdirectory —
`tests/`, `wiring/`, `app/` — breaks `pnpm lint`.

"Root-level policy spec" is therefore a designed category with a written rationale, not an
accident. The only lever on file count is **merging**, and a merged file at `src/` root keeps
the exemption. Merging at root also leaves `../vite.config`, `new URL("../", import.meta.url)`
and `import.meta.glob("./features/*/index.ts")` all correct as written.

## Target

Two files per app, both directly under `src/`:

| File | Vitest project | Holds |
| --- | --- | --- |
| `app-wiring.test.ts` | node | brand assets, browser deps, browser styles wiring, the surviving facade and query-key pins, and (auth only) base path and generated mutations |
| `app-wiring.browser.test.tsx` | browser | browser-mode smoke, theme wiring, feature barrels |

The extension split is forced by the shared preset's project routing, so two is the floor.

**21 files → 4. ~1,700 lines → ~350.**

## Disposition of every file

| File | Lines | Disposition |
| --- | --- | --- |
| `heading-scale.test.tsx` ×2 | 680 | **delete** — `packages/ui` stories measure the recipe; `wallow/text-heading-variant` pins the call sites |
| `brand-assets.test.ts` ×2 | 163 | **share** as `@bc-solutions-coder/testing/brand-assets` |
| `browser-mode-smoke.test.tsx` ×2 | 70 | **share** as `@bc-solutions-coder/testing/browser-mode-smoke` |
| `query-facade.test.ts` ×2 | 227 | **narrow** to the pre-bundle and `ssr.noExternal` pins; drop the `typeof` sweep and `instanceof` |
| `shared-auth.test.ts` / `shared-current-user.test.ts` | 173 | **narrow** to the query-key equality pin; drop the barrel sweep |
| `generated-mutations.test.ts` | 142 | **narrow** to whatever `packages/sdk/src/generated-query-surface.test.ts` does not already cover |
| `log-headers.test.ts` | 22 | **keep verbatim** — only an app depending on both packages can pin it |
| `base-path-wiring.test.ts` | 54 | **fold in** unchanged |
| `browser-deps` / `browser-styles-wiring` / `theme-wiring` / `feature-barrels` | 223 | **fold in** unchanged — already thin delegations |

## The gap the deletion opens, and how it closes

Deleting `heading-scale` removes wallow-web's only guard on heading call sites: wallow-auth
enables `wallow/text-heading-variant`, wallow-web never did. Closing it is a config change
plus three call-site edits, all rendering-identical.

```jsonc
// apps/wallow-web/.oxlintrc.json — root
"wallow/text-heading-variant": ["error", { "levels": { "h2": "subheading" } }]

// override for src/features/landing/components/LandingPage.tsx
"wallow/text-heading-variant": ["error", {
  "levels": { "h1": "display", "h2": "title", "h3": "subheading" }
}]
```

| Site | Now | After |
| --- | --- | --- |
| `bff-demo.tsx:154` | `<Text as="h1">` | `variant="title"` |
| `LandingPage.tsx:190` | `<Text as="h2">` | `variant="title"` |
| `LandingPage.tsx:251` | `<Text as="h2" color="onSidebar">` | `variant="title"` |

Auth's `{ h1: false, h2: "subheading" }` cannot be reused: wallow-web legitimately opens its
own `<h1>` per page, and the two bare LandingPage `h2`s currently *derive* `title` (`text-3xl`)
from `as`. Pinning them to `subheading` would shrink the landing page from 30px to 20px, so
`LandingPage.tsx` gets an override naming its own scale — mirroring how `auth-layout.tsx`
relaxes `h1` rather than switching the rule off. The landing page's scale ends up declared
rather than merely tolerated.

Net: wallow-web gains enforcement it did not have, from config instead of 344 lines of mounting.

## New surface in `packages/testing`

Two entries, each on its own subpath per this package's subpath-per-entry rule — neither may
ride the barrel, which is loaded in plain Node at Vitest config time.

| Entry | Project | Export |
| --- | --- | --- |
| `./brand-assets` | node | `assertBrandAssetWiring({ viteConfig, appName })` |
| `./browser-mode-smoke` | browser | `assertBrowserModeSmoke(appName)` |

Each addition touches four places: `src/`, the `exports` map (both the source map and the
`publishConfig` dist map), `vite.config.ts`'s `entries`, and the entry table in
`packages/testing/CLAUDE.md`.

## Sequencing

Four commits, each independently green under `pnpm check`.

1. `refactor(testing): share brand-asset and browser-mode guards` — add both entries, rewrite
   the four callers in place, extend `minimal-app` (it has neither guard today, so a fork
   inherits neither). *Separable: drop the `minimal-app` half if it reads as scope creep.*
2. `feat(web): pin heading variants with text-heading-variant` — rule, override, three call
   sites. **Lands before the deletion so coverage never dips.**
3. `test: drop assertions owned by lint, pnpm and the ui stories` — the deletions and narrowings.
4. `refactor: consolidate app wiring guards into app-wiring specs` — the merge.

Steps 3 and 4 touch the same files; combined they would make the diff unreadable, hence the split.

## Verification

- `pnpm check` after each commit.
- Step 2: `pnpm --filter ./apps/wallow-web lint` shows exactly three `text-heading-variant`
  violations before the call-site edits and zero after.
- Step 1: `pnpm lint:deps` (knip) and `pnpm check:exports` (publint + attw) — new subpath
  entries are what those two gates exist to catch.
- Step 4: temporarily break one wired piece and confirm the failure still names which.

## Risks

- **`generated-mutations` narrowing is the least certain item.** Its overlap with
  `packages/sdk/src/generated-query-surface.test.ts` is assumed, not audited. If the overlap is
  partial it keeps more than planned and the line total lands higher. Audit before cutting.
- **Merged browser specs serialise** — vitest parallelises per file. Three small browser
  guards in one file is a negligible cost, but it is a real one.
- **Failure attribution drops to `describe` level.** Mitigated by naming each `describe` for
  the guard it replaces, so the test name still says what broke.
