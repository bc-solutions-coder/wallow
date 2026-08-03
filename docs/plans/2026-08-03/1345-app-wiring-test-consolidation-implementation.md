# App Wiring Test Consolidation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**status: completed**

**Goal:** Collapse 21 policy specs (~1,700 lines) at the top of the two apps' `src/` into one node
spec and one browser spec per app (~350 lines), deleting every assertion another tool already owns.

**Architecture:** Three shared guards move into `@bc-solutions-coder/testing` as new subpath entries.
Assertions that pnpm, TypeScript, oxlint or the `packages/ui` stories already enforce are deleted.
The survivors merge into `src/app-wiring.test.ts` (node) and `src/app-wiring.browser.test.tsx`
(browser). Both stay **directly under `src/`** — `wallow/zone-dag` exempts single-segment paths as
`ROOT_ZONE`, and only that exemption permits importing `../vite.config`.

**Tech Stack:** Vitest 4 (two-project node/browser split, headless Chromium via Playwright), oxlint
with the `@bc-solutions-coder/lint` plugin, pnpm workspace, Vite library mode.

**Design doc:** `docs/plans/2026-08-03/1343-app-wiring-test-consolidation.md`

---

## A note on TDD for this plan

This plan changes *tests*, so "write a failing test first" mostly does not apply — the artifact
under construction is the assertion itself. The equivalent discipline, used throughout:

- **Before deleting anything**, prove the replacement enforcement actually fires (Task 5 runs
  oxlint and counts violations before and after).
- **After moving a guard**, deliberately break the thing it guards and confirm the failure still
  names it. Every task that relocates an assertion has this as an explicit step.

Do not skip the break-it steps. A guard that passes after a move but no longer fails is worse than
the duplication it replaced.

---

## Task 1: Share the brand-asset guard

**Files:**
- Create: `packages/testing/src/brand-assets.ts`
- Modify: `packages/testing/package.json` (both `exports` maps)
- Modify: `packages/testing/vite.config.ts:9-25` (`entries`)

**Step 1: Create the shared helper**

`packages/testing/src/brand-assets.ts`:

```ts
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import type { Plugin, UserConfig } from "vite";
import { describe, expect, it } from "vitest";

/**
 * A root-relative `<img src="/piggy-icon.svg">` is only half the fix: something has to
 * answer that URL. Start's nitro output serves `.output/public` at the root, so an app
 * owes only getting the icon INTO that directory — and getting it from the shared
 * package rather than a copy of its own, which is what makes `packages/styles/branding.json`
 * the one place a fork swaps the icon.
 *
 * Vite's `publicDir` is that mechanism, and the wiring lives inside `wallowStyles()`:
 * the brand-assets plugin sets `publicDir` through its `config()` hook rather than the
 * app declaring a raw field, so this guard asserts the behaviour through that seam.
 *
 * Node-only — it imports `vite` types and the app's own config. Keep it off the barrel.
 */
const brandAssetsDir: string = fileURLToPath(
  new URL("../../styles/assets/", import.meta.url),
);

/** Every plugin `wallowStyles()` contributes, flattened out of nested arrays. */
function flatten(option: unknown): Plugin[] {
  if (Array.isArray(option)) {
    return option.flatMap((entry: unknown): Plugin[] => flatten(entry));
  }
  if (option !== null && typeof option === "object" && "name" in option) {
    return [option as Plugin];
  }
  return [];
}

/** The `publicDir` the brand-assets plugin contributes, or `undefined` if it declares none. */
function brandAssetsPublicDir(): string | undefined {
  const plugin: Plugin | undefined = flatten(wallowStyles()).find(
    (candidate: Plugin): boolean => candidate.name === "wallow:brand-assets",
  );
  const hook: unknown = plugin?.config;
  const handler: unknown =
    typeof hook === "function" ? hook : (hook as { handler?: unknown })?.handler;

  if (typeof handler !== "function") {
    return undefined;
  }

  const config: UserConfig = (handler as () => UserConfig).call(plugin);

  return config.publicDir === undefined ? undefined : String(config.publicDir);
}

/** The Vite config shape this guard reads. Structural, so an app passes its own config as-is. */
export interface BrandAssetWiringOptions {
  /** The app's default-exported Vite config. */
  readonly viteConfig: {
    readonly environments?: { readonly client?: { readonly build?: { readonly copyPublicDir?: boolean } } };
  };
  /** The app's name, for the `describe` title. */
  readonly appName: string;
}

/** Assert an app takes its brand assets from the shared styles package. */
export function assertBrandAssetWiring({ viteConfig, appName }: BrandAssetWiringOptions): void {
  describe(`the ${appName} client build`, () => {
    it("takes its static assets from the shared styles package", () => {
      const publicDir: string | undefined = brandAssetsPublicDir();

      expect(publicDir).toBeDefined();
      expect(resolve(String(publicDir))).toBe(resolve(brandAssetsDir));
    });

    it("re-enables copyPublicDir on the client environment", () => {
      // Start builds through nitro/vite's two named environments, and nitro does
      // `config.build.copyPublicDir ??= false` on the CLIENT one. That silently drops
      // the publicDir the brand-assets plugin contributes, so `/piggy-icon.svg` 404s in
      // the BUILT app only — the dev server serves publicDir itself and looks fine.
      expect(viteConfig.environments?.client?.build?.copyPublicDir).toBe(true);
    });
  });
}
```

**Step 2: Verify `brandAssetsDir` resolves**

The app copies used `../../../packages/styles/assets/` from `apps/<app>/src/`. From
`packages/testing/src/` the correct hop is `../../styles/assets/`. Confirm:

Run: `ls packages/styles/assets/`
Expected: the brand asset files (`piggy-icon.svg` among them).

**Step 3: Register the entry**

Add to `packages/testing/vite.config.ts` `entries`, after `"browser-deps"`:

```ts
    "brand-assets": "src/brand-assets.ts",
```

Add to **both** `exports` maps in `packages/testing/package.json` — the source map uses
`./src/brand-assets.ts`, the `publishConfig` map uses `./dist/brand-assets.{d.ts,js}`:

```jsonc
    "./brand-assets": {
      "types": "./src/brand-assets.ts",
      "import": "./src/brand-assets.ts"
    },
```

```jsonc
      "./brand-assets": {
        "types": "./dist/brand-assets.d.ts",
        "import": "./dist/brand-assets.js"
      },
```

**Step 4: Verify it builds and typechecks**

Run: `pnpm --filter @bc-solutions-coder/testing build && pnpm --filter @bc-solutions-coder/testing typecheck`
Expected: PASS, `dist/brand-assets.js` and `dist/brand-assets.d.ts` emitted.

---

## Task 2: Share the browser-mode smoke guard

**Files:**
- Create: `packages/testing/src/browser-mode-smoke.ts`
- Modify: `packages/testing/package.json` (both `exports` maps)
- Modify: `packages/testing/vite.config.ts` (`entries`)

**Step 1: Create the shared helper**

`packages/testing/src/browser-mode-smoke.ts`:

```ts
import { describe, expect, it } from "vitest";

/**
 * A consumer's browser project really is real Chromium.
 *
 * Asserts signals only a genuine browser produces, so it fails if the multi-project split
 * routes the spec onto node (no `document`) or if jsdom/happy-dom is ever reintroduced
 * (fake userAgent, zero-sized layout boxes). Both are banned repo-wide — see
 * `.claude/rules/TESTING.md`.
 *
 * Browser-only. Keep it off the barrel, which is loaded in plain Node at config time.
 */
export function assertBrowserModeSmoke(appName: string): void {
  describe(`${appName} browser-mode smoke`, () => {
    it("runs inside a real Chromium window, not node or jsdom", () => {
      // node has no `document` at all; jsdom's navigator.userAgent contains "jsdom".
      expect(typeof document).toBe("object");
      expect(navigator.userAgent).toMatch(/Chrome|Chromium|HeadlessChrome/u);
    });

    it("has a real layout engine — jsdom reports every box as zero-sized", () => {
      const box: HTMLDivElement = document.createElement("div");
      box.style.width = "120px";
      box.style.height = "40px";
      document.body.append(box);

      const rect: DOMRect = box.getBoundingClientRect();

      expect(rect.width).toBeGreaterThan(0);
      expect(rect.height).toBeGreaterThan(0);

      box.remove();
    });
  });
}
```

**Step 2: Register the entry**

Same two files as Task 1, key `"browser-mode-smoke"` → `src/browser-mode-smoke.ts`.

**Step 3: Verify**

Run: `pnpm --filter @bc-solutions-coder/testing build && pnpm --filter @bc-solutions-coder/testing typecheck`
Expected: PASS.

---

## Task 3: Share `browserPreBundleList`

Three byte-identical copies of this function plus its 12-line comment live in
`apps/wallow-web/src/query-facade.test.ts`, `apps/wallow-auth/src/query-facade.test.ts` and
`apps/wallow-auth/src/shared-current-user.test.ts`.

**Files:**
- Modify: `packages/testing/src/browser-deps.ts` (add a named export; the entry already exists)

**Step 1: Add the export**

Append to `packages/testing/src/browser-deps.ts`:

```ts
/** The vitest config shape this reader needs. Structural, so a consumer passes its own config. */
export interface BrowserProjectConfig {
  readonly test?: {
    readonly projects?: readonly {
      readonly optimizeDeps?: { readonly include?: readonly string[] };
      readonly test?: { readonly name?: string };
    }[];
  };
}

/**
 * A consumer's browser-project `optimizeDeps.include`, read off the CONFIG OBJECT.
 *
 * Importing the config does not boot a browser provider: `playwright()` returns a
 * descriptor and nothing launches until vitest runs the project. Reading the value
 * asserts what Vite actually receives rather than how the file happens to be written.
 */
export function browserPreBundleList(config: BrowserProjectConfig): readonly string[] {
  const projects = config.test?.projects ?? [];

  return projects.find((project) => project.test?.name === "browser")?.optimizeDeps?.include ?? [];
}
```

**Step 2: Verify**

Run: `pnpm --filter @bc-solutions-coder/testing build && pnpm --filter @bc-solutions-coder/testing typecheck`
Expected: PASS.

---

## Task 4: Point the app callers at the shared guards, and commit

**Files:**
- Modify: `apps/wallow-web/src/brand-assets.test.ts`, `apps/wallow-auth/src/brand-assets.test.ts`
- Modify: `apps/wallow-web/src/browser-mode-smoke.test.tsx`, `apps/wallow-auth/src/browser-mode-smoke.test.tsx`
- Modify: `apps/wallow-web/package.json`, `apps/wallow-auth/package.json` if the entries need declaring

**Step 1: Rewrite the four callers**

`apps/wallow-web/src/brand-assets.test.ts` becomes, in full:

```ts
import { assertBrandAssetWiring } from "@bc-solutions-coder/testing/brand-assets";

import viteConfig from "../vite.config";

assertBrandAssetWiring({ viteConfig, appName: "wallow-web" });
```

`apps/wallow-auth/src/brand-assets.test.ts` is the same with `appName: "wallow-auth"`.

`apps/wallow-web/src/browser-mode-smoke.test.tsx` becomes, in full:

```tsx
import { assertBrowserModeSmoke } from "@bc-solutions-coder/testing/browser-mode-smoke";

assertBrowserModeSmoke("wallow-web");
```

`apps/wallow-auth/src/browser-mode-smoke.test.tsx` is the same with `"wallow-auth"`.

**Step 2: Run both app suites**

Run: `pnpm --filter ./apps/wallow-web test && pnpm --filter ./apps/wallow-auth test`
Expected: PASS, same test count as before (four cases per app across the two guards).

**Step 3: Break it deliberately**

Temporarily set `copyPublicDir: false` in `apps/wallow-web/vite.config.ts`'s
`environments.client.build`.

Run: `pnpm --filter ./apps/wallow-web test -- brand-assets`
Expected: FAIL naming "re-enables copyPublicDir on the client environment".

Revert the config change and re-run to confirm PASS.

**Step 4: Extend `minimal-app` (optional — drop if it reads as scope creep)**

`apps/examples/minimal-app` has neither guard, so a fork inherits neither. Create
`apps/examples/minimal-app/src/brand-assets.test.ts` and `src/browser-mode-smoke.test.tsx` on the
same two-line pattern with `appName: "minimal-app"`. Note this app is **not** zoned, so there is no
`zone-dag` consideration.

Run: `pnpm --filter ./apps/examples/minimal-app test`
Expected: PASS.

**Step 5: Gate and commit**

Run: `pnpm check`
Expected: PASS. `lint:deps` (knip) and `check:exports` (publint + attw) are the two that care about
new subpath entries — if either complains, fix the `exports` map before committing.

```bash
git add packages/testing apps/wallow-web/src apps/wallow-auth/src apps/examples/minimal-app/src
git commit -m "refactor(testing): share brand-asset and browser-mode guards"
```

---

## Task 5: Enable `text-heading-variant` in wallow-web

This lands **before** any deletion so heading coverage never dips.

**Files:**
- Modify: `apps/wallow-web/.oxlintrc.json`
- Modify: `apps/wallow-web/src/app/routes/bff-demo.tsx:154`
- Modify: `apps/wallow-web/src/features/landing/components/LandingPage.tsx:190,251`
- Modify: `apps/CLAUDE.md`

**Step 1: Add the rule and the override**

In `apps/wallow-web/.oxlintrc.json`, alongside the other `wallow/*` entries (near line 19):

```jsonc
    "wallow/text-heading-variant": ["error", { "levels": { "h2": "subheading" } }],
```

Then add an override block for the landing page. Order matters in oxlint — an override's entry
REPLACES the base one — so place it with the other overrides:

```jsonc
    {
      "files": ["src/features/landing/components/LandingPage.tsx"],
      "rules": {
        "wallow/text-heading-variant": [
          "error",
          { "levels": { "h1": "display", "h2": "title", "h3": "subheading" } }
        ]
      }
    }
```

The landing page renders a marketing scale, not a card scale. Its two bare `h2`s currently
*derive* `title` (`text-3xl`) from `as`; pinning them to `subheading` would shrink them to 20px.
The override declares that scale instead — mirroring how `auth-layout.tsx` relaxes `h1` rather
than switching the rule off.

**Step 2: Confirm the rule fires on exactly three sites**

Run: `pnpm --filter ./apps/wallow-web lint 2>&1 | grep -c "text-heading-variant"`
Expected: **3** — `bff-demo.tsx:154`, `LandingPage.tsx:190`, `LandingPage.tsx:251`.

If the count differs, STOP and reconcile before editing any call site. A count of 0 means the rule
did not register; a higher count means a site this plan did not survey.

**Step 3: Fix the three call sites**

All three gain `variant="title"`, which is what `as` was already deriving — rendering is unchanged.

`bff-demo.tsx:154`:
```tsx
      <Text as="h1" variant="title">Wallow BFF example</Text>
```

`LandingPage.tsx:190`:
```tsx
      <Text as="h2" variant="title" className="text-center mb-12">
```

`LandingPage.tsx:251`:
```tsx
        <Text as="h2" variant="title" color="onSidebar" className="mb-12">
```

**Step 4: Confirm zero violations**

Run: `pnpm --filter ./apps/wallow-web lint 2>&1 | grep -c "text-heading-variant"`
Expected: **0**.

**Step 5: Confirm rendering did not change**

Run: `pnpm --filter ./apps/wallow-web test`
Expected: PASS — including `heading-scale.test.tsx`, which still exists at this point and measures
these very screens. **This is the strongest possible check that the three edits are visually
inert**, and it is the reason this task precedes the deletion.

**Step 6: Update `apps/CLAUDE.md`**

In the `wallow/text-heading-variant` description, replace "wallow-auth only: " with a statement
covering both apps, and note wallow-web's LandingPage override. Keep the existing prose about
`h2` → `subheading` and no `weight`.

**Step 7: Gate and commit**

Run: `pnpm check`
Expected: PASS.

```bash
git add apps/wallow-web/.oxlintrc.json apps/wallow-web/src apps/CLAUDE.md
git commit -m "feat(web): pin heading variants with text-heading-variant"
```

---

## Task 6: Delete `heading-scale.test.tsx`

**Files:**
- Delete: `apps/wallow-web/src/heading-scale.test.tsx` (344 lines)
- Delete: `apps/wallow-auth/src/heading-scale.test.tsx` (336 lines)
- Modify: `apps/CLAUDE.md`

**Step 1: Confirm the replacement coverage exists**

Run: `grep -n "fontSize\|text-xl\|text-lg" packages/ui/src/components/card/card.stories.tsx packages/ui/src/components/text/text.stories.tsx`
Expected: measured probes in both — `scale-probe-lg` / `scale-probe-xl` / `scale-card-title` in
`card.stories.tsx`, `standard-probe-lg` / `standard-probe-xl` / `standard-subheading` in
`text.stories.tsx`. These run in the `storybook` vitest project, the only one that loads Tailwind
and the fork theme.

If those probes are absent, STOP — the deletion has no replacement.

**Step 2: Delete both files**

```bash
git rm apps/wallow-web/src/heading-scale.test.tsx apps/wallow-auth/src/heading-scale.test.tsx
```

Their `__screenshots__/heading-scale.test.tsx/` directories are gitignored failure artifacts and
need no git action; delete them from the working tree if you like.

**Step 3: Update `apps/CLAUDE.md`**

In the "A card heading is 20px (`text-xl`), catalog-wide" bullet, remove the clause naming
`heading-scale.test.tsx` as the measured cross-screen pin. The sentence should now attribute
call-site coverage to `wallow/text-heading-variant` and recipe coverage to the `packages/ui`
measured stories. Leave the rest of the bullet — the 20px standard itself is unchanged.

**Step 4: Verify**

Run: `pnpm --filter @bc-solutions-coder/ui test && pnpm --filter ./apps/wallow-web test && pnpm --filter ./apps/wallow-auth test`
Expected: PASS.

**Step 5: Commit**

```bash
git add -A apps/CLAUDE.md apps/wallow-web/src apps/wallow-auth/src
git commit -m "test: drop the cross-screen heading-scale specs"
```

---

## Task 7: Narrow the facade and auth specs

**Files:**
- Modify: `apps/wallow-web/src/query-facade.test.ts`, `apps/wallow-auth/src/query-facade.test.ts`
- Modify: `apps/wallow-web/src/shared-auth.test.ts`
- Modify: `apps/wallow-auth/src/shared-current-user.test.ts`

**What goes, and why.** Every deleted case asserts `typeof someImport === "function"` on a named
import. Under pnpm's strict `node_modules` plus TypeScript, a missing export cannot reach the
assertion — it is a load-time error. The files say so themselves: *"A missing re-export is a
load-time error here, not an assertion failure."* The `import` statement is the test.

**Step 1: `apps/wallow-web/src/query-facade.test.ts`**

- DELETE the `describe("the facade as this app resolves it")` block entirely — both the `typeof`
  sweep and the `instanceof`/`retry` case. `createQueryClient`'s `retry: false` default belongs to
  `packages/query` and is pinned in `packages/query/src/query-client.test.ts`; verify that with
  `grep -n "retry" packages/query/src/query-client.test.ts` before deleting, and if it is absent,
  keep only that one `it` and drop the rest of the block.
- KEEP `describe("the vitest harness resolves the facade explicitly")` — both cases.
- DELETE the local `browserPreBundleList` function and import it from
  `@bc-solutions-coder/testing/browser-deps`, passing `vitestConfig`.
- Trim the file header to the surviving claim. Max 8 lines, present tense, per
  `packages/testing/CLAUDE.md`.

**Step 2: `apps/wallow-auth/src/query-facade.test.ts`**

Same treatment: delete `describe("the facade as wallow-auth resolves it")`, keep
`describe("browser-mode pre-bundling survives the facade hop")`, swap the local helper for the
shared import.

**Step 3: `apps/wallow-web/src/shared-auth.test.ts`**

- DELETE the `it("exposes the current-user layer and the route guards from one barrel")` case.
- KEEP `it("keys the current-user query with the generated key the profile read uses")` and its
  `fakeClient()` helper — this is a genuine app-level claim (this app's two readers meet on one
  cache entry) that nothing else asserts.

**Step 4: `apps/wallow-auth/src/shared-current-user.test.ts`**

- DELETE `describe("the auth package as wallow-auth resolves it")`.
- KEEP `describe("browser-mode pre-bundling covers the auth package")`, swapping the local helper
  for the shared import.

Note this app's file has **no** query-key pin (unlike wallow-web's), so what remains is purely a
pre-bundle concern. In Task 9 it folds in beside the other pre-bundle assertions rather than
surviving as its own subject.

**Step 5: Verify**

Run: `pnpm --filter ./apps/wallow-web test && pnpm --filter ./apps/wallow-auth test`
Expected: PASS, with a lower case count.

**Step 6: Break it deliberately**

Temporarily remove `@bc-solutions-coder/query` from the browser project's `optimizeDeps.include`
in `apps/wallow-web/vitest.config.ts`.

Run: `pnpm --filter ./apps/wallow-web test -- query-facade`
Expected: FAIL naming the pre-bundle case. Revert and re-run to confirm PASS.

**Step 7: Commit**

```bash
git add apps/wallow-web/src apps/wallow-auth/src
git commit -m "test: drop surface assertions owned by pnpm and typescript"
```

---

## Task 8: Audit and narrow `generated-mutations.test.ts`

**This is the least certain item in the plan.** Its overlap with the SDK's own spec is assumed, not
verified. Audit before cutting.

**Files:**
- Modify: `apps/wallow-auth/src/generated-mutations.test.ts` (142 lines)

**Step 1: Read both specs side by side**

Run: `cat apps/wallow-auth/src/generated-mutations.test.ts packages/sdk/src/generated-query-surface.test.ts`

For each `it()` in the app spec, decide: is this a claim about **the generated SDK surface**
(belongs in `packages/sdk`, delete here if already covered) or about **which operations this app
uses** (app-level, keep)?

**Step 2: Delete only what is genuinely duplicated**

Keep anything the SDK spec does not assert. If the overlap turns out to be small, the file stays
large — that is an acceptable outcome, not a failure of the plan. Record what you kept and why in
the file header.

**Step 3: Verify**

Run: `pnpm --filter ./apps/wallow-auth test && pnpm --filter @bc-solutions-coder/sdk test`
Expected: PASS.

**Step 4: Commit**

```bash
git add apps/wallow-auth/src/generated-mutations.test.ts
git commit -m "test: narrow generated-mutations to app-level claims"
```

---

## Task 9: Merge into `app-wiring` specs

**Files (wallow-web):**
- Create: `apps/wallow-web/src/app-wiring.test.ts`, `apps/wallow-web/src/app-wiring.browser.test.tsx`
- Delete: `brand-assets.test.ts`, `browser-deps.test.ts`, `browser-styles-wiring.test.ts`,
  `log-headers.test.ts`, `query-facade.test.ts`, `shared-auth.test.ts`,
  `browser-mode-smoke.test.tsx`, `theme-wiring.test.tsx`, `feature-barrels.browser.test.tsx`

**Files (wallow-auth):**
- Create: `apps/wallow-auth/src/app-wiring.test.ts`, `apps/wallow-auth/src/app-wiring.browser.test.tsx`
- Delete: the same set plus `base-path-wiring.test.ts`, `shared-current-user.test.ts`,
  `generated-mutations.test.ts`

**Both files stay directly under `src/`.** A subdirectory breaks `wallow/zone-dag` with
`escapesSrc` on every `../vite.config` / `../vitest.config` import. This is verified, not assumed —
see the design doc.

**Step 1: Build the node file**

`apps/wallow-web/src/app-wiring.test.ts` concatenates the surviving node-project guards. Keep each
`describe` named for the guard it replaces — with the filename gone, the `describe` is what tells a
reader what broke.

Order: brand assets → browser deps → browser styles wiring → facade pre-bundle → current-user key
→ log headers. Imports merge at the top; `../vite.config` and `../vitest.config` stay one hop.

**Step 2: Build the browser file**

`apps/wallow-web/src/app-wiring.browser.test.tsx` holds browser-mode smoke, theme wiring, and the
feature-barrel loader. The `import.meta.glob("./features/*/index.ts")` specifier is unchanged — the
merged file sits in the same directory as the file it came from.

Note `assertThemeWiring` and `assertBrowserModeSmoke` are called at module top level and create
their own `describe`s; call them before the `describe` the barrel loader declares.

**Step 3: Repeat for wallow-auth**

Same shape. `base-path-wiring`'s `vi.stubEnv`/`afterEach` block folds into the node file unchanged —
keep its `afterEach(() => vi.unstubAllEnvs())` scoped **inside** its own `describe`, not at file
level, so it cannot affect the other guards now sharing the file.

`browser-styles-wiring`'s `extraSpecs` list of checkbox-bearing screens carries over verbatim.

**Step 4: Delete the old files**

```bash
git rm apps/wallow-web/src/{brand-assets,browser-deps,browser-styles-wiring,log-headers,query-facade,shared-auth}.test.ts
git rm apps/wallow-web/src/{browser-mode-smoke,theme-wiring,feature-barrels.browser}.test.tsx
git rm apps/wallow-auth/src/{base-path-wiring,brand-assets,browser-deps,browser-styles-wiring,generated-mutations,query-facade,shared-current-user}.test.ts
git rm apps/wallow-auth/src/{browser-mode-smoke,theme-wiring,feature-barrels.browser}.test.tsx
```

**Step 5: Verify project routing**

Run: `pnpm --filter ./apps/wallow-web test && pnpm --filter ./apps/wallow-auth test`
Expected: PASS. Confirm in the output that `app-wiring.test.ts` ran on the **node** project and
`app-wiring.browser.test.tsx` on the **browser** project. The preset routes by extension, so a
`.tsx` guard landing on node means the file was named wrong.

**Step 6: Break each merged guard once**

The whole risk of merging is a guard that silently stops asserting. For **each** of the following,
break it, confirm the failure names the right `describe`, then revert:

| Break | Expected failing describe |
| --- | --- |
| `copyPublicDir: false` in `vite.config.ts` | the client build |
| remove a `optimizeDeps.include` entry | browser deps / pre-bundling |
| remove the `virtual:wallow-theme.css` import from the browser setup file | theme wiring |
| rename a `src/features/*/index.ts` export | feature barrels |

**Step 7: Confirm zone-dag is still satisfied**

Run: `pnpm lint`
Expected: PASS — in particular, zero `zone-dag` errors. If `escapesSrc` appears, a file landed in a
subdirectory.

**Step 8: Gate and commit**

Run: `pnpm check`
Expected: PASS.

```bash
git add -A apps/wallow-web/src apps/wallow-auth/src
git commit -m "refactor: consolidate app wiring guards into app-wiring specs"
```

---

## Task 10: Update the testing package guide

**Files:**
- Modify: `packages/testing/CLAUDE.md`

**Step 1: Add the two new entries to the subpath table**

| Entry | Imported at | What it is |
| --- | --- | --- |
| `./brand-assets` | node-project spec | `assertBrandAssetWiring({ viteConfig, appName })` — the app's static assets come from the shared styles package, through `wallowStyles()`'s `config()` hook. Node-only: imports `vite` types. |
| `./browser-mode-smoke` | browser-mode spec | `assertBrowserModeSmoke(appName)` — the consumer's browser project really is Chromium, not node and not jsdom. |

Also note `browserPreBundleList` as a second export of the existing `./browser-deps` row.

**Step 2: Note the root-zone constraint**

Add a short paragraph recording why these guards live directly under an app's `src/`: `zone-dag`
exempts single-segment paths as `ROOT_ZONE`, and that exemption is the only thing permitting an
import of `../vite.config`. Without this written down, the next person will try to tidy them into a
folder and hit `escapesSrc`.

**Step 3: Commit**

```bash
git add packages/testing/CLAUDE.md
git commit -m "docs(testing): document the shared wiring guards"
```

---

## Final verification

Run: `pnpm check`
Expected: PASS.

Run: `git diff --stat main`
Expected: roughly −1,350 lines net across `apps/`, +~200 in `packages/testing`.

Confirm the file count:

Run: `ls apps/wallow-web/src/*.test.* apps/wallow-auth/src/*.test.*`
Expected: exactly four files — `app-wiring.test.ts` and `app-wiring.browser.test.tsx` per app.

Finally, mark the design doc `**status: completed**` and update this plan's status line to match.
