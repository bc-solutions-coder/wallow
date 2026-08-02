**status: completed**

# Maintenance & Sidebar Epic — Wallow-x7ht, Wallow-0g7w, Wallow-0byr

One epic covering three pre-existing beads: two toolchain/maintenance fixes and one
frontend feature (dashboard sidebar rework). The existing beads (Wallow-x7ht,
Wallow-0g7w, Wallow-0byr) MUST be attached under the epic (re-parent or link them —
do not duplicate them as new beads). The sidebar bead is the only one large enough
to decompose into child tasks.

## Feature 1 — Dependency security fix (existing bead: Wallow-x7ht)

Clear Dependabot alert #53 (high): js-yaml >=4.0.0 <4.3.0 (quadratic CPU via YAML
merge-key chains, patched in 4.3.0). The only vulnerable resolution is js-yaml 4.2.0
pulled by `@hey-api/openapi-ts` 0.99.0 → `@hey-api/json-schema-ref-parser` 1.4.4
(dev-time SDK codegen only; runtime paths already resolve 4.3.0).

**Approach:** Prefer bumping `@hey-api/openapi-ts` if a release now pulls a fixed
ref-parser; otherwise add a pnpm override `js-yaml: ">=4.3.0"` in the root
`package.json`. Reinstall and verify the lockfile has no js-yaml < 4.3.0 resolution.

**Acceptance:**
- `pnpm why js-yaml` (and the lockfile) shows no resolution of js-yaml < 4.3.0.
- SDK codegen still works: the OpenAPI client regen command in `packages/sdk/CLAUDE.md`
  runs cleanly and produces no unexpected diff against the committed client.
- `pnpm check` passes.

## Feature 2 — routeTree.gen.ts deterministic ordering (existing bead: Wallow-0g7w)

`vite build` / dev server rewrites `apps/wallow-web/src/routeTree.gen.ts` and
`apps/wallow-auth/src/routeTree.gen.ts` with a different import/route-declaration
order than the committed content (a pure reorder — 0 net-unique lines), dirtying the
tree on every build. Cause: version skew between `@tanstack/router-cli` (^1.167.21,
used by `routes:generate`) and `@tanstack/router-plugin` (^1.168.20, used by vite).

**Approach:** Align `@tanstack/router-cli` and `@tanstack/router-plugin` to the same
version in both apps, regenerate the route trees, and commit whichever ordering both
tools agree on.

**Acceptance:**
- Both apps pin router-cli and router-plugin to matching versions.
- After `routes:generate` AND a `vite build` (or dev-server start), `git status`
  stays clean — no routeTree.gen.ts churn in either app.
- `pnpm check` passes.

## Feature 3 — Dashboard sidebar: expanded / icon-rail / mobile overlay (existing bead: Wallow-0byr)

Today `apps/wallow-web/src/components/DashboardNav.tsx` collapses to `w-16` while
still rendering full-size text labels, which get clipped ("Settin", "Inquir",
"Sign O"). Rework the sidebar into three deliberate modes:

1. **Expanded (desktop default):** normal sidebar (~w-64) with icon + label per nav
   item and active-route highlight — current expanded behavior preserved.
2. **Collapsed icon rail (desktop):** toggle collapses the sidebar to an icon-only
   rail. Each nav item shows ONLY its icon (with an accessible name, e.g.
   `aria-label`/tooltip) — no clipped text, ever. Toggle switches between expanded
   and collapsed; these are the ONLY two desktop states.
3. **Mobile (below the responsive breakpoint):** the sidebar disappears entirely
   (no rail). A menu button opens it as an **overlay drawer** above the content
   (with backdrop; closes on backdrop click / navigation / Escape). The overlay
   shows the full expanded content (icons + labels). Collapse-to-rail does not
   apply on mobile.

**Constraints & conventions:**
- Every nav item needs an icon (choose icons consistent with the app's existing
  icon usage; if none exists, pick one library already in the workspace or the
  lightest consistent option — check `packages/ui` first for shared components).
- Sidebar open/collapsed state is UI-only state → Zustand (never TanStack Query),
  per `docs/development/frontend-state.md`.
- Use Tailwind v4 classes consistent with `@bc-solutions-coder/styles` theme tokens.
- Keep/extend `data-testid` attributes (kebab-case `{page}-{element}`) for E2E.
- Component tests run in Vitest browser mode (real Chromium; never jsdom).

**Acceptance:**
- Desktop collapsed state renders icons only — no text nodes visible/clipped.
- Desktop toggle switches expanded ↔ collapsed; active-route highlight works in both.
- On a mobile-width viewport the rail is absent; the menu button opens an overlay
  drawer with backdrop; the drawer closes on backdrop click, on nav-link click,
  and on Escape.
- All nav destinations (Apps, Settings, Inquiries, Sign Out) remain reachable in
  every mode and keep accessible names.
- Vitest browser-mode component tests cover the three modes; existing wallow-web
  tests, `pnpm check`, and the wallow-web Playwright route-reachability suite pass.

## Ordering

Features 1 and 2 are independent of each other and of Feature 3. Feature 2 should
land before Feature 3 only in the trivial sense that a clean routeTree avoids noise
in the sidebar diffs — no hard dependency edges between features.
