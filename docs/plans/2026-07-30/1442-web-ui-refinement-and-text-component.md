# App-Level UI Refinement — `Text`, Dark Mode, and Catalog-Only App Surfaces

**status: active**

Bead: **Wallow-lrlm** (app-level polish pass over wallow-web and wallow-auth after the UI
rebuild). Deferred from epic Wallow-m5aq, whose sweeps were correctness-driven — same markup,
now on Base UI — rather than design-driven.

Intended runner: `/team-build docs/plans/2026-07-30/1442-web-ui-refinement-and-text-component.md`

---

## Problem

A read-through of `apps/wallow-web` found four distinct classes of problem. They are listed
here as the evidence the features below act on; every finding was verified against the tree at
`8df6ee62`.

### 1. Genuine UX gaps, not cosmetics

- **Detail routes are unreachable.** `/dashboard/organizations/$orgId` and
  `/dashboard/inquiries/$inquiryId` have loaders, prefetch, and full components, but no list
  row links to them (`OrganizationList.tsx:15`, `InquiryList.tsx:35` render inert `<li>`s).
  The only way in is typing a URL.
- **No list has an error state.** Every list destructures `{ data, isPending }` then does
  `data ?? []`, so a failed fetch renders the *empty* state — API down shows "No apps yet. 🐷".
  Affects `AppList.tsx:56`, `InquiryList.tsx:74`, `OrganizationList.tsx:67`,
  `MemberList.tsx:72`, `ProfileSection.tsx:58`. `OrganizationDetail.tsx:282` is worse: any
  fetch error renders "Organization not found." `InquiryDetail.tsx:66-83` is the one correct
  implementation and is the template the rest copy.
- **Every intra-app link is a raw `<a href>`** — full page reload, query cache discarded:
  `routes/dashboard/apps/index.tsx:32`, `OrganizationDetail.tsx:229`,
  `InquiryDetail.tsx:87,101`, all of `PublicLayout.tsx`. `DashboardNav` is the only consumer
  of TanStack `Link`. Several carry a comment justifying the anchor ("renders standalone
  without router context") — that is a spec-shape problem being paid for in production
  behaviour.
- **Two inputs have no accessible name**: `<Field><Input/></Field>` with no `Field.Label` at
  `OrganizationDetail.tsx:128` (client display name) and `MemberList.tsx:130` (user id).
  `OrganizationDetail.tsx:138` is a bare `<textarea>` with hand-rolled classes rather than the
  catalog `Textarea`.

### 2. Dark mode is emitted but never activated

`api/branding.json` ships a complete dark palette and
`packages/styles/src/branding.ts:242-249` (`renderThemeStyle`) already emits `:root`, `.dark`,
and `.light` blocks. **Nothing in either app ever applies `.dark` or `.light`**, and there are
zero `dark:` variants anywhere. Half the fork's theme is unreachable.

It is also actively fragile: `DashboardNav.tsx:191,213` fakes a dark rail with
`bg-foreground text-background`. That only reads as dark because light mode's `foreground`
happens to be dark — apply `.dark` and the rail inverts to a light bar. `PublicLayout`'s footer
uses the same trick.

### 3. Token discipline — web is the outlier, auth is already clean

|              | ad-hoc alpha | `text-muted-foreground` | `bg-muted` |
| ------------ | ------------ | ----------------------- | ---------- |
| `wallow-auth` | **0**        | 48                      | 6          |
| `wallow-web`  | **28**       | 0                       | 0          |

wallow-web leans on `text-foreground/60` (×9), `bg-background/50` (×7), `text-foreground/70`
(×5), `bg-foreground/10`, `bg-background/15`, and so on. `--muted`, `--muted-foreground`, and
`--popover` are defined in `branding.json` and entirely unused by web. Auth got the sweep; web
did not.

### 4. The catalog under-serves the apps, so the apps hand-roll

`packages/ui/src/components/button/button.styles.ts` — the base recipe is `w-full`, three
variants, **no hover or focus-visible treatment at all**, no size/width/shape variant, no
`outline`/`ghost`/`link`. The consequences are mechanical:

- Five call sites override with `className="w-auto"` (`OrganizationDetail.tsx:306,318`,
  `MemberList.tsx:47,141`) or `"rounded-full"` (`OrganizationDetail.tsx:148`).
- Every CTA bypasses `Button` entirely and hand-rolls
  `bg-primary … rounded-full hover:opacity-90` on an `<a>` — `apps/index.tsx:32`,
  `PublicLayout.tsx:73`, `LandingPage.tsx:107`.
- Both nav controls at `DashboardLayout.tsx:47,67` are raw `<button>`s with byte-identical
  hand-rolled classes.

Four recipes are duplicated across the app with no home:

- **Table card + row** (`bg-card rounded-lg shadow-sm border border-border overflow-hidden` /
  `flex items-center justify-between px-6 py-4 hover:bg-background/50`) — 5 files.
  `OrganizationDetail.tsx:34-37` already extracted them as `TABLE_CARD`/`TABLE_ROW`; nobody
  imports them.
- **The chip** (`bg-accent text-accent-foreground text-xs … rounded-full`) — 5 copies, two of
  them independently named `CHIP` (`ProfileSection`, `MfaSettingsSection`).
- **Empty-state card** — three near-identical (`AppList.tsx:39`, `InquiryList.tsx:57`,
  `OrganizationList.tsx:50`), each with an arbitrary `text-[80px]` and inconsistent emoji
  (🐷 / 🐷 / 🏢).
- **Page shell** — container width has no rule: `max-w-5xl` (apps, orgs, org detail,
  inquiries) vs `max-w-2xl` (settings, register, inquiry detail). The heading row has four
  spellings: flex row with CTA (apps), flex row with a *single* child i.e. a dead wrapper
  (`organizations/index.tsx:30`), bare `h1` + `space-y-8` (inquiries), `h1 mb-8` (settings,
  register).

Composite components the bead explicitly names — `Combobox`, `Autocomplete`, `Toolbar`,
`ScrollArea`, `Separator`, `Tooltip`, `Avatar` — have **zero uses in web**. The most pointed:
`MemberList.tsx:130` asks a user to paste a raw user GUID into a text box.

---

## Goals

1. **One typography component.** `Text` owns every piece of text in both apps: the element
   (`as`), the type scale (`variant`), and the semantic colour (`color`). No app ever writes
   `text-3xl font-bold text-foreground` again, and no app ever writes `text-foreground/60`.
2. **Dark mode is real, chosen, and correct.** A user-facing toggle, seeded from
   `branding.json` `defaultMode`, falling back to `prefers-color-scheme`, persisted, applied
   before first paint. Every colour in both apps resolves through a semantic token, so
   flipping the mode is complete and automatic.
3. **Apps compose the catalog; they never build primitives.** No styled bare HTML element in
   `apps/*/src/**` — enforced by lint, not convention.
4. **The links that should exist, exist.** Every list row reaches its detail route; every
   intra-app navigation is a client-side `Link`; every query has an error state.
5. **Nothing regresses.** Every `data-testid` survives, every E2E suite stays green, no
   component is deleted from the catalog.

## Non-goals

- No new backend endpoints, no OpenAPI regeneration, no SDK changes.
- No visual redesign of the landing page's content or copy — only its primitives change.
- No new catalog components beyond the six named below. If a task wants a seventh, it files a
  follow-up bead instead.
- No removal of any existing catalog component.

---

## Repo facts every agent MUST honour

These are the constraints that make this plan land without breaking the build. The decomposer
should copy the relevant ones into each bead's `--design`.

**Adding a component to `packages/ui` touches four files in one commit.** Growing one alone
turns another red:

1. `src/components/<name>/` — `<name>.tsx`, `<name>.styles.ts`, `<name>.stories.tsx`,
   `<name>.test.tsx`, `index.ts`
2. `COMPONENT_FOLDERS` in `src/core/package-scaffold.test.ts:287`
3. `src/index.ts` (the root barrel — components and prop types only, never the recipe block)
4. `PUBLIC_RUNTIME_EXPORTS` + the `PublicTypeExports` tuple in `src/index.test.ts`

Plus `baseUiSubpaths` in `packages/ui/vitest.config.ts` (alphabetically) if the component
imports a `@base-ui/react/<part>` subpath not already listed.

**Other hard rules** (from `packages/ui/CLAUDE.md`, `.claude/rules/TESTING.md`, `CLAUDE.md`):

- Recipes live in `<name>.styles.ts` as CVA only — no JSX, no React import. Recipes reference
  **only semantic token utilities**; a missing token is added to `packages/styles/styles.css`'s
  `@theme` (and `api/branding.json`) **first**.
- `className` is narrowed back to `string` on every part; `cn(recipe(...), className)` so a
  caller's class always wins.
- **Stories ARE the render/interaction coverage.** `*.test.tsx` covers only edges a story
  cannot express (data-attribute state, className-override-wins, keyboard interaction). Do not
  duplicate story coverage into a test file.
- **Never mock `@bc-solutions-coder/ui`.** Component tests run in real headless Chromium via
  Vitest browser mode — never jsdom, never happy-dom, never jest.
- The `browser` vitest project **loads no Tailwind**, so a component whose box comes only from
  its recipe and has no text measures 0×0 and `userEvent.click` hangs. Use `element.click()`
  or `focus()` + keyboard. Pointer interaction belongs in stories.
- Tailwind `@source` must scan `*.tsx` **and** `*.styles.ts` — already true in `source.css` and
  `.storybook/preview.css`; don't regress it.
- Build order: `pnpm --filter @bc-solutions-coder/sdk build` then
  `pnpm --filter @bc-solutions-coder/ui build` before an app typechecks. `pnpm check` runs test
  before build, and `dist-structure.test.ts` asserts the **built** artifact — rebuild `ui`
  after adding a component.
- Formatter/linter is **oxc** (`oxfmt` + `oxlint --deny-warnings`), never prettier/eslint.
- **`data-testid` is a contract.** `{page}-{element}` kebab-case. `apps/wallow-web/e2e-cross-app/login-journey.spec.ts` selects many of them; none may drift. Several
  `*.restyle.test.tsx` specs pin exact classes (e.g. `CreateOrganizationForm.restyle.test.tsx`
  pins `space-y-6`) — when a migration changes the class, update the spec deliberately in the
  same task and say so on the bead.
- Commit format: `<type>(<scope>): <description>`, lowercase, imperative, no trailing period,
  first line < 72 chars. Scope is the package or app (`ui`, `styles`, `wallow-web`).
- `api/branding.json` is `merge=ours` in `.gitattributes`. A fork's copy will **not** receive
  new keys on an upstream merge — see F1.T2 for the fallback requirement this imposes.

---

## Feature plan

Seven features, run sequentially. Dependency edges are noted per feature; within a feature,
tasks marked *(parallel)* have no ordering between them.

```
F1 tokens ──┬──> F2 Text ──┬──> F3 catalog ──> F5 web migration ──> F6 composites ──> F7 auth + docs
            │              │                        ^
            └──────────────┴──> F4 web correctness ─┘
```

F4 is independent of F2/F3 and could run earlier; it is placed after F3 so its rewrites land on
the final component set rather than being written twice.

---

## F1 — Design tokens and theme activation

**Why:** every later feature's recipes reference semantic tokens, and two tokens the apps need
do not exist. Dark mode's CSS is already emitted and simply never applied.

### F1.T1 — Add the missing semantic tokens

**Change.** Add to `api/branding.json` under both `theme.light` and `theme.dark`, and map each
in `packages/styles/styles.css`'s `@theme` block:

| Token                  | Purpose                                                                   |
| ---------------------- | ------------------------------------------------------------------------- |
| `sidebar`              | The dashboard rail / footer surface. Replaces the `bg-foreground` inversion. |
| `sidebarForeground`    | Text on that surface. Replaces `text-background`.                          |
| `sidebarAccent`        | Active/hover row on the rail. Replaces `bg-background/15` and `/10`.       |
| `success`              | State colour the MFA chip needs (`MfaSettingsSection.tsx:51-54` documents the gap). |
| `successForeground`    | Text on `success`.                                                        |

Pick values in the same oklch family as the existing palette so the fork's look is unchanged in
light mode (the current rail is `foreground` = `oklch(0.22 0.035 45)`; `sidebar` should start
there for light, and stay dark — not invert — for dark mode).

**Critical: forks must not break.** `packages/styles/src/branding.ts:110-119` (`toCssVars`)
drops empty values, and `ThemeColors` is `Readonly<Record<string, string>>` — so a fork whose
`branding.json` predates these keys emits no `--sidebar` at all, and `bg-sidebar` would resolve
to nothing. Every new `@theme` mapping **must** carry a fallback:

```css
--color-sidebar: var(--sidebar, var(--foreground));
--color-sidebar-foreground: var(--sidebar-foreground, var(--background));
```

**Verify first:** grep `api/src` for a C# `ThemeColorSet` / `BrandingOptions` binding of the
`theme` block. The Blazor apps that consumed it are deleted and the initial read found no such
type — if one exists, add the keys there too for parity; if not, note on the bead that the
theme block is TypeScript-only now.

**Acceptance**

- `packages/styles` tests pass, including `theme-css.test.ts`'s "defines every custom property
  the `@theme` block maps, in dark mode" and "maps every custom property `forkBranding.theme`
  emits" — both are catalog-derived and will fail if the two sides drift.
- A new test proves a `ForkBranding` **missing** the new keys still resolves: rendered CSS
  contains no `--sidebar` declaration, and the `@theme` fallback chain is present in
  `styles.css`.
- No existing token's value changed.

### F1.T2 — `ThemeProvider` + `ThemeToggle` in the catalog

**Why:** `.dark` / `.light` are emitted and never applied. Both apps need the same mechanism,
so it belongs in `packages/ui`, not in either app.

**Change.** Two new catalog folders (`theme-provider`, `theme-toggle`) following the four-file
registration rule above.

Resolution order, highest priority last:

1. `branding.json` `theme.defaultMode` (already surfaced as `ResolvedBranding.defaultMode`)
2. `prefers-color-scheme`
3. the user's persisted choice (`localStorage`)

**No flash of wrong theme.** The class must be on `<html>` before first paint. Emit a tiny
blocking inline script from the app shell (alongside `<DocumentStyles/>` in each app's
`__root.tsx`) that reads storage + media query and stamps the class synchronously. React then
hydrates against it — do not compute the initial class in a `useEffect`.

`ThemeToggle` is a `Button`-composed control with a `data-testid="theme-toggle"`, an
`aria-label`, and `aria-pressed` (or a three-state `light | dark | system` cycle — the
implementer picks one and documents it on the bead).

**Acceptance**

- Stories cover light, dark, and system-derived states.
- A test proves the resolution order: persisted choice beats media query beats `defaultMode`.
- A test proves the pre-paint script stamps the class on `<html>` and that hydration does not
  change it (no mismatch warning).
- Both apps' `__root.tsx` render the provider + script; wallow-web's dashboard nav and
  wallow-auth's shell each expose the toggle.
- `pnpm --filter @bc-solutions-coder/ui test` green (all three projects).

---

## F2 — The `Text` component

**Depends on:** F1.T1 (its `color` variants reference the new tokens).

**Why:** this is the single highest-leverage piece. Every heading, paragraph, caption, label,
and value in both apps currently spells out its own Tailwind. Centralising it is what makes
"switch mode and all text knows its colour scheme" true by construction rather than by sweep.

### F2.T1 — Build `Text`

**Change.** New catalog folder `packages/ui/src/components/text/` with the full four-file
registration. It wraps no Base UI part (there isn't one for text) — it is a plain polymorphic
element carrying a CVA recipe, in the same shape as the existing `MutedText`.

**API**

```tsx
<Text as="h1">Organizations</Text>
<Text as="p" color="muted">No organizations yet.</Text>
<Text as="span" variant="caption" color="accent">3 members</Text>
<Text as="h2" variant="body" weight="semibold">Bound Clients</Text>
```

| Prop      | Values                                                                          | Notes                                                                                                                   |
| --------- | ------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `as`      | `h1 h2 h3 h4 h5 h6 p span div label legend code`                                | Chooses the **element**. Defaults to `p`. Each maps to a default `variant`, so `<Text as="h1">` is styled with no further props. |
| `variant` | `display title heading subheading body bodySm caption overline code`            | Chooses the **type scale** (size + weight + leading + tracking). Explicit `variant` overrides the `as`-derived default, so `<Text as="h2" variant="body">` is legal and is how semantic level is decoupled from visual weight. |
| `color`   | `default muted primary accent destructive success onSidebar onCard onPrimary`   | Semantic token pairs **only** — never alpha math. This is the prop that makes mode-switching correct.                     |
| `weight`  | `normal medium semibold bold`                                                   | Optional override of the variant's weight.                                                                              |
| `align`   | `left center right`                                                             | Optional.                                                                                                               |

**Rules the recipe must obey**

- Every `color` value maps to exactly one semantic token utility (`text-foreground`,
  `text-muted-foreground`, `text-primary`, `text-accent-foreground`, `text-destructive`,
  `text-success`, `text-sidebar-foreground`, `text-card-foreground`, `text-primary-foreground`).
  **No `/NN` opacity anywhere in this recipe.** This is the whole point of the component.
- `overline` carries the uppercase caption treatment the apps hand-roll twice today
  (`ProfileSection.tsx:26` and `MfaSettingsSection.tsx:45`:
  `block text-xs font-semibold … uppercase tracking-wider mb-1`) — minus the `mb-1`, which is
  layout and belongs to the caller.
- The `as`→`variant` default table is data, not a chain of conditionals, so it reads in one
  place.
- `className` narrowed to `string`, merged via `cn()` so a caller's class wins.

**Acceptance**

- Stories cover every `variant`, every `color`, and the `as`-derived defaults — rendered in
  **both** modes (the storybook project has the real Tailwind pipeline and real fork theme
  attached, so this is where dark-mode correctness is actually visible).
- Tests cover: `as` renders the right element; `variant` overrides the `as` default; a caller
  `className` beats the recipe; the recipe contains no `/` opacity token (assert by reading the
  recipe's class string).
- Registered in all four places; `pnpm --filter @bc-solutions-coder/ui test` and `build` green.

### F2.T2 — Reconcile `MutedText`

**Why:** `MutedText` is `text-sm text-muted-foreground` — exactly `<Text variant="bodySm" color="muted">`. Two ways to say one thing is what this plan exists to remove.

**Change.** Keep `MutedText` exported (41 call sites in wallow-auth, 5 in web — and the goals
forbid deleting catalog components), but reimplement it as a thin composition over `Text` so
there is one recipe. Its own `*.test.tsx` contract (a `<p>`, the muted classes, additive
className, caller className wins) must still pass unchanged.

**Acceptance**

- `muted-text.test.tsx` passes with **no edits** — that is the proof the refactor is
  behaviour-preserving.
- `mutedTextRecipe` still exported from `@bc-solutions-coder/ui/muted-text` (subpath contract).
- Its stories still render identically.

---

## F3 — Close the catalog gaps that force apps to hand-roll

**Depends on:** F1.T1 (tokens), F2.T1 (`Text` — the new components compose it).

### F3.T1 — Upgrade the `Button` recipe *(parallel)*

**Change.** `packages/ui/src/components/button/button.styles.ts`:

- New `variant`s: `outline`, `ghost`, `link` (the three shapes the apps currently hand-roll on
  `<a>`).
- New `size`: `sm | md | lg | icon` (`icon` is the square control `DashboardLayout.tsx:47,67`
  needs).
- New `width`: `full | auto`. **Keep `full` the default** — five call sites rely on it and the
  auth app's forms do too. `auto` retires the `className="w-auto"` overrides.
- New `shape`: `rounded | pill`. `pill` retires `className="rounded-full"` and the three
  hand-rolled CTAs.
- **Add hover and focus-visible treatment to the base recipe** — there is none today, which is
  the deepest reason call sites route around `Button`. Use `focus-visible:ring-ring` and a
  token-based hover per variant.

**Acceptance**

- Every existing `Button` call site in both apps renders **unchanged** under the new defaults
  (`variant="primary" size="md" width="full" shape="rounded"`). This is a pure superset.
- Stories cover the full variant × size × shape matrix, in both modes.
- Existing `button.test.tsx` passes without edits.

### F3.T2 — `PageHeader` *(parallel)*

Title, optional description, optional `actions` slot (right-aligned). Composes `Text`
(`as="h1" variant="title"`). Replaces four different spellings of the same row and deletes the
dead single-child flex wrapper at `organizations/index.tsx:30`.

**Acceptance:** stories for title-only, title+description, title+actions; a test that the
`actions` slot is omitted from the DOM entirely when not supplied (not rendered empty).

### F3.T3 — `EmptyState` *(parallel)*

`icon` (a node — emoji or SVG), `title`, `description`, optional `action`. Composes `Text`.
Replaces the three near-identical cards and retires the arbitrary `text-[80px]` for a token
size.

**Acceptance:** stories with and without an action; the three web empty states are expressible
with no `className` escape hatch (prove it by migrating one in this task).

### F3.T4 — `Badge` *(parallel)*

The five-copy chip. `variant`: `default | accent | success | destructive | muted`, `size`:
`sm | md`. Composes `Text` for its label. Unblocks the MFA chip's documented "no success
token" gap now that F1.T1 shipped one.

**Acceptance:** stories per variant in both modes; a test that `variant="success"` uses the
`success` token and no raw hue.

### F3.T5 — `ListCard` + `ListRow`

The table-card + row recipe from five files, including the two orphaned constants at
`OrganizationDetail.tsx:34-37`. `ListCard` is the bordered surface with `divide-y`; `ListRow`
is the `<li>`.

**`ListRow` must accept Base UI's `render` prop** so a caller can compose it onto a TanStack
`Link` — that is what F4.T1 needs to make rows navigate without re-nesting an anchor inside a
list item. When rendered as a link it must carry hover/focus-visible affordance; when rendered
as a plain `<li>` it must not look interactive.

**Acceptance:** stories for static rows and navigating rows; a test that `render` composes onto
an anchor and that the interactive treatment is present only then (assert via `data-*`, per the
catalog's state-attribute convention).

---

## F4 — wallow-web correctness: links and query states

**Depends on:** F3.T5 (`ListRow` `render`), F3.T1 (`Button` `link` variant).

These are the user-visible bugs. Each task changes behaviour, so each needs a failing test
first.

### F4.T1 — List rows navigate to their detail routes

**Change.** `OrganizationList` rows link to `/dashboard/organizations/$orgId`; `InquiryList`
rows link to `/dashboard/inquiries/$inquiryId`, via `ListRow`'s `render` + TanStack `Link`.
`AppList` has no detail route — leave its rows static and note that on the bead rather than
inventing one.

**Acceptance:** a component test clicking a row asserts the router navigated to the right
param; existing `organization-item` / `inquiry-item` testids unchanged; keyboard `Enter` on a
focused row navigates.

### F4.T2 — Every query gets an error state

**Change.** Adopt `InquiryDetail.tsx:66-83`'s pattern — which correctly distinguishes
*errored* from *resolved-empty* by checking `isError` only when there is no data to fall back
on — across `AppList`, `InquiryList`, `OrganizationList`, `MemberList`, `ProfileSection`,
`OrganizationDetail`, and `MfaSettingsSection`.

Render `ErrorBanner` with `errorText(error, "<fallback>")` from `src/lib/error-text.ts`. New
testids follow `{page}-{element}`: `apps-error`, `inquiries-error`, `organizations-error`,
`organization-members-error`, `settings-profile-error`, `organization-detail-error`.

**Acceptance:** per list, a test that a rejected query renders the error banner and **not** the
empty state; a test that a successful empty response still renders the empty state; a test that
a background-refetch failure with cached data keeps showing the data.

### F4.T3 — Client-side navigation everywhere

**Change.** Replace every intra-app `<a href>` with TanStack `Link`:
`routes/dashboard/apps/index.tsx:32`, `OrganizationDetail.tsx:229`,
`InquiryDetail.tsx:87,101`, and `PublicLayout.tsx`'s internal links. External links
(`repositoryUrl`, `docsUrl`, `getStartedHref` → the BFF login flow) stay anchors — they leave
the SPA — but move to the `Button` `link`/`outline` variants rather than hand-rolled classes.

**The blocker is spec shape, and it must be fixed properly.** Three components document the
anchor as deliberate: "a plain anchor, not a router `Link`, so the component renders standalone
without a router context." The fix is a router-context test helper (a
`renderWithRouter` in `@bc-solutions-coder/testing`, or a memory-router wrapper), **not**
keeping anchors in production. Add the helper in this task and migrate those specs onto it.

**Acceptance:** zero `<a href="/…">` (app-internal) in `apps/wallow-web/src`; a test that
clicking the back link does not trigger a document navigation; all previously-standalone specs
pass through the new helper; `apps-register-link`, `organization-detail-back-link`,
`inquiry-detail-back-link` testids unchanged; `pnpm --filter ./apps/wallow-web test:e2e` green.

### F4.T4 — Accessible names and the bare textarea

**Change.** Add `Field.Label` to `OrganizationDetail.tsx:128` (client display name) and
`MemberList.tsx:130` (user id). Replace the bare `<textarea>` at `OrganizationDetail.tsx:138`
with the catalog `Textarea`.

**Acceptance:** a test asserting each input is reachable by its accessible name; the textarea
carries the catalog recipe; testids unchanged.

---

## F5 — wallow-web migrates onto the catalog, then the door closes

**Depends on:** F2, F3, F4.

### F5.T1 — Page shells

**Change.** Every dashboard route uses `PageHeader`. Settle the container width to one rule and
apply it: **`max-w-5xl` for list/detail pages, `max-w-2xl` for single-form pages** (register,
settings) — document the rule in `apps/CLAUDE.md`. Delete the dead flex wrapper in
`organizations/index.tsx`.

**Acceptance:** all six dashboard routes render through `PageHeader`; page-root testids
(`dashboard-apps`, `dashboard-organizations`, `dashboard-inquiries`, `dashboard-settings`,
`dashboard-apps-register`, and both detail pages) unchanged.

### F5.T2 — Lists, empty states, badges

**Change.** The three lists use `ListCard`/`ListRow`/`EmptyState`; all five chips become
`Badge`; the two `FIELD_LABEL` constants become `<Text variant="overline">`; the two local
`CHIP` constants and the two `TABLE_CARD`/`TABLE_ROW` constants are deleted.

**Acceptance:** no `bg-card rounded-lg shadow-sm border border-border` string literal remains in
`apps/wallow-web/src`; no chip literal remains; all list/empty/chip testids unchanged. Update
the affected `*.restyle.test.tsx` specs deliberately and record the diff on the bead.

### F5.T3 — All text becomes `Text`; all alpha becomes tokens

**Change.** Every heading, paragraph, and styled `<span>` in `apps/wallow-web/src` becomes
`Text`. All 28 ad-hoc alpha classes resolve to a semantic token:

| Today                                    | Becomes                                        |
| ---------------------------------------- | ---------------------------------------------- |
| `text-foreground/60`, `text-foreground/70` | `<Text color="muted">` / `text-muted-foreground` |
| `bg-background/50` (row hover)            | `bg-muted` (via `ListRow`)                     |
| `bg-foreground/10`, `bg-foreground/40`    | `bg-muted`, `bg-foreground/40` → a scrim token or `Dialog.Backdrop`'s existing recipe |
| `text-background/80`, `bg-background/15`  | `sidebar-foreground`, `sidebar-accent`         |

**Acceptance:** `grep -rn 'foreground/[0-9]\|background/[0-9]' apps/wallow-web/src --include='*.tsx'`
returns nothing outside tests; `wallow-web` reaches parity with `wallow-auth`'s zero.

### F5.T4 — Retire the `bg-foreground text-background` inversion

**Change.** `DashboardNav.tsx:191,213` and `PublicLayout`'s footer move to
`bg-sidebar text-sidebar-foreground`; the active-row `bg-background/15` becomes
`bg-sidebar-accent`. The raw `<button>`s at `DashboardLayout.tsx:47,67` and the backdrop at
`:88` become `Button size="icon"` (the backdrop keeps its `data-testid` and `aria-label`).

**Acceptance:** a test that the rail renders dark in **both** modes (assert the token class, not
a computed colour); `dashboard-nav-toggle`, `dashboard-nav-mobile-menu`,
`dashboard-nav-backdrop`, `data-nav-open` all unchanged; the three-mode nav behaviour
(`DashboardNav`'s documented desktop-expanded / icon-rail / mobile-drawer contract) is
untouched.

### F5.T5 — Hand-rolled forms move to `useAppForm`

**Change.** `RegisterClientForm` (`OrganizationDetail.tsx:107`), `AddMemberForm`
(`MemberList.tsx:113`), and `InquiryDetail`'s `AddCommentForm` are `useState` + `<form>`,
against the repo's "`useAppForm` is the one way a form is written" rule. All three carry
in-file comments admitting they are structural ports. Migrate them onto `useAppForm` + the
`AppForm` shell, following `CreateOrganizationForm` (the canonical template) and
`RegisterAppForm` (the one with a `toVariables` remap).

**Acceptance:** each form's existing testids survive (several are derived from the shell's
`testIdPrefix` — check the derivation matches before renaming anything); RFC 7807 per-property
errors land next to their field; existing specs pass or are updated with the reason on the bead.

### F5.T6 — The lint gate (**must be the last task in F5**)

**Change.** Add a `no-restricted-syntax` block to the root `.oxlintrc.json`, scoped to
`apps/*/src/**`, banning styled bare elements with a message naming the replacement:

| Banned                                     | Message                       |
| ------------------------------------------ | ----------------------------- |
| `<h1>`–`<h6>`, `<p>`                       | use `<Text as="…">`           |
| `<span className>`, `<div className>` with text/colour utilities | use `<Text>` or `<Badge>` |
| `<button>`                                 | use `<Button>`                |
| `<a href>` to an app-internal path         | use `<Link>`                  |
| `<input>`, `<textarea>`, `<select>`        | use `<Input>` / `<Textarea>` / `<Select>` |

**Allowed** (document the allowlist in the config comment): `<div>`/`<section>`/`<main>`/`<aside>`
with layout-only classes (flex/grid/spacing/sizing), `<form>` (the `AppForm` shell renders it),
`<li>`/`<ul>` inside `ListCard`, `<a>` to an external origin, `<img>`, `<svg>`, and anything
under `src/routes/**/*.ts` (server routes, no JSX).

The rule ships **after** F5.T1–T5, so `pnpm lint` is green the moment it lands.

**Acceptance:** `pnpm lint` green across the workspace; a deliberate violation (added and
reverted in the task, recorded on the bead) is proven to fail; the allowlist is documented in
`apps/CLAUDE.md` alongside the width rule from F5.T1.

---

## F6 — Composite adoption and loose ends

**Depends on:** F5. Adopt where it deletes hand-rolled code — do **not** force a component in
just because the catalog has it.

### F6.T1 — Add-member becomes a `Combobox`

`MemberList.tsx:130` asks for a raw user GUID. Replace with the catalog `Combobox` (or
`Autocomplete`) over users the org can add. **Check the SDK first** for an operation that lists
candidate users; if none exists, keep the text input, file a follow-up bead for the endpoint,
and record that on this bead — do not invent an API.

### F6.T2 — Resolve `BrandingSection`

`RegisterAppForm`'s `BrandingSection` is fully uncontrolled and wired to nothing — three inputs
and a file picker that silently do nothing on submit. The in-file comment says the live upsert
(`clientBrandingUpsertBrandingMutation`) needs the client id the register call returns. Either
wire it into the post-registration success view (where that id exists) or remove it and file a
bead. **Live dead UI is not an acceptable end state.**

### F6.T3 — Fix the mobile nav flash

`useIsDesktop`'s `getServerSnapshot()` returns `true`, so phones SSR the desktop rail and swap
after hydration — a visible flash on first paint. Options: a CSS-driven container that is
correct pre-JS, or a cookie/client-hint. Pick one, document the trade-off on the bead.

**Acceptance:** an E2E assertion at a phone viewport that the rail is never painted.

---

## F7 — wallow-auth parity and documentation

**Depends on:** F6.

### F7.T1 — Auth adopts `Text`, `PageHeader`, `Button` variants, `ThemeToggle`

Auth is already token-clean (zero ad-hoc alpha, 48 `text-muted-foreground`), so this is mostly
mechanical `Text` adoption across its 15 feature folders plus placing the theme toggle in its
shell.

**Acceptance:** the F5.T6 lint rule (which is scoped to `apps/*/src/**` and therefore already
covers auth) passes; every auth testid unchanged; `pnpm --filter ./apps/wallow-auth test:e2e`
green.

### F7.T2 — Documentation

Update `docs/development/component-library.md` (the six new components, the `Text` API table,
the `as`→`variant` default table), `docs/development/frontend-setup.md` (theming and the mode
toggle), `apps/CLAUDE.md` (the no-bare-HTML rule, its allowlist, the container-width rule), and
`packages/ui/CLAUDE.md` (component count — currently "47 component folders" — and the `Text`
composition convention). Add anything new to `docs/toc.yml`.

Docs rules live in `docs/CLAUDE.md`: site content only, lowercase-kebab filenames.

---

## Definition of done

- [ ] `pnpm check` green (format:check + lint + typecheck + test + build + check:exports).
- [ ] `./scripts/run-tests.sh` green — no backend change is intended, so this is a regression check.
- [ ] `./scripts/e2e.sh` green (all three Playwright suites: wallow-auth, wallow-web, cross-app).
- [ ] Zero ad-hoc alpha colour classes in `apps/*/src/**`.
- [ ] Zero styled bare HTML elements in `apps/*/src/**`, enforced by `pnpm lint`.
- [ ] Every dashboard list row reaches its detail route; every query has an error state.
- [ ] Toggling light/dark changes every surface in both apps with no unreadable text and no
      inverted rail.
- [ ] No `data-testid` removed or renamed without an explicit note on the owning bead.
- [ ] No catalog component deleted.
- [ ] Bead **Wallow-lrlm** closed.

## Risks

| Risk                                                                 | Mitigation                                                                                                       |
| -------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Testid drift breaks the cross-app E2E journey                        | Every migration task lists its testids as acceptance; `login-journey.spec.ts` is named as a hard contract.        |
| `*.restyle.test.tsx` specs pin exact classes and will fail on migration | Treated as deliberate updates inside the owning task, recorded on the bead — never silently loosened.             |
| New tokens break existing forks (`branding.json` is `merge=ours`)     | F1.T1 requires `@theme` fallbacks plus a test proving a key-less fork still resolves.                             |
| `Text` becomes a dumping ground of props                              | The prop table is fixed in F2.T1. A seventh prop, or a seventh component, is a follow-up bead — not scope creep.  |
| The lint rule lands before migration and blocks everything            | F5.T6 is explicitly the last task in F5 and its acceptance is a green `pnpm lint`.                                |
| `ui` `dist/` goes stale and `dist-structure.test.ts` fails            | Every catalog task's acceptance includes a `ui` rebuild; the build-order note is in the shared facts section.     |
