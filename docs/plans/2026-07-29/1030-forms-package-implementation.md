**status: active**

# @bc-solutions-coder/forms Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
> Design doc: `docs/plans/2026-07-29/1016-forms-package-design.md` — read it first.

**Goal:** Create `packages/forms` (`@bc-solutions-coder/forms`) — the single package owning form
authoring (TanStack Form `createFormHook` catalog + zod validation + submit pipeline + RFC 7807
error mapping) — and migrate all five existing forms onto it.

**Architecture:** forms sits above `@bc-solutions-coder/ui` (consumes its `Field` anatomy, `Input`,
`Select`, `Button`, `ErrorBanner`; ui must gain **zero** knowledge of forms) and below the apps.
Catalog field components are pre-bound via TanStack Form's `createFormHook`; an `AppForm` shell owns
the `<form>` element, testid derivation, and pending/error context; `useAppForm` unifies submit
through `useMutation` (a plain-`onSubmit` escape hatch becomes the mutationFn when no mutation is
given).

**Tech Stack:** TypeScript, React 19, `@tanstack/react-form` ^1.33.2, `@tanstack/react-query`
^5.101.2 (peer), `zod` (NEW dependency — v4.x), Vite 8 lib mode, vitest 4 browser mode via
`@bc-solutions-coder/testing`, oxlint/oxfmt.

## Execution process: worktree + Wallow-vufu coordination

This plan runs **in a dedicated git worktree** and merges back to `main` when complete. The
BFF CSRF/topology epic (**Wallow-vufu**, source plan
`docs/plans/2026-07-29/0948-bff-csrf-and-topology-remediation.md`) may land on `main`
concurrently. File overlap between the two is small; follow this process to keep the merge
trivial:

**Setup (before Task 1):**

```bash
git worktree add ../Wallow-forms -b feat/forms-package main
cd ../Wallow-forms
pnpm install
```

(Use the superpowers:using-git-worktrees skill if available. All commands in this plan then run
from the worktree root.)

**Sequencing rule — the CSRF regression (Wallow-vufu.1):** `main` currently 403s every
dashboard mutation in dev (the SDK CSRF interceptor only stamps `x-csrf-token` for `bff-demo`).
This is NOT a forms bug. Consequences for this plan:

- Tasks 1–10 (package build + wallow-auth migrations) are unaffected — proceed regardless.
- **Before starting Task 11** (first wallow-web migration, whose verification submits real
  mutations): check whether Wallow-vufu.1 has landed on main (`bd show Wallow-vufu.1`, or
  `git log origin/main --oneline -- packages/sdk/src/csrf.ts`). If it has, rebase onto main
  first (below). If it has NOT, still proceed — but any 403 seen during live/dev verification
  of a migrated wallow-web form is the known CSRF regression, not a migration fault; verify
  those forms via their vitest browser specs + `routes.spec.ts`, note the deferred live check
  in the task's commit message, and re-verify after the final rebase.

**Rebase cadence:** `git pull --rebase origin main` (from the worktree) after each completed
task, and ALWAYS immediately before Task 11 and before the final merge. Expected conflict
points, all small — resolve as noted:

| File | Why it may conflict | Resolution |
| --- | --- | --- |
| `packages/sdk/src/server/errors.ts` + interceptor sites | Task 2's `fieldErrors` vs vufu's handler/proxy work reshaping problem-details parsing | Keep BOTH: vufu's parsing structure, with `fieldErrors` threaded through it; re-run the sdk suite |
| `apps/wallow-auth/package.json` | This plan adds the forms dep; vufu 2.2 may add base-path config | Keep both changes (different keys) |
| `pnpm-lock.yaml` | Both sides touching deps | `git checkout --theirs pnpm-lock.yaml && pnpm install` (regenerate; never hand-merge) |
| `apps/wallow-auth/vite.config.ts` | vufu 2.2 base path | Take vufu's version — this plan does not edit it |

After ANY rebase that pulled in sdk changes: `pnpm --filter @bc-solutions-coder/sdk build`
before re-running app typechecks (apps typecheck against dist/).

**Merge-back (extends Task 15):** rebase onto latest main, run the full gate (`pnpm check`,
`./scripts/e2e.sh`) IN THE WORKTREE on the rebased result, then fast-forward merge to main
(`git checkout main && git merge --ff-only feat/forms-package`) — no merge commit, so the
history stays linear and conventional commits stay release-parseable. If vufu landed its 1.3
CSRF e2e spec by then, it drives dashboard-form testids this plan preserves — the post-rebase
e2e run is the proof both plans compose. Finally remove the worktree
(`git worktree remove ../Wallow-forms`) after push succeeds.

**Read before starting:**
- `packages/ui/CLAUDE.md` (layering, catalog-set files, browser-test gotchas)
- `packages/sdk/CLAUDE.md` (build template, error contract)
- `.claude/rules/TESTING.md` and `.claude/rules/CONVENTIONS.md`
- `docs/development/frontend-state.md` (query-key/invalidation rules the migrations must keep)

**Verified API facts (do not re-derive):**
- `createFormHookContexts()` → `{ fieldContext, formContext, useFieldContext, useFormContext }`;
  `createFormHook({ fieldContext, formContext, fieldComponents, formComponents })` →
  `{ useAppForm, withForm }`. The form instance exposes `form.AppField` (render prop receives the
  field object **with the registered field components attached**, e.g. `f.TextField`) and
  `form.AppForm` (a bare `PropsWithChildren<{}>` context provider — takes NO props; we do not use
  it since we register no `formComponents`).
- With a zod schema as validator (`validators: { onSubmit: schema }`), `field.state.meta.errors`
  holds `StandardSchemaV1Issue[]` (objects with `.message`), NOT strings. Function validators
  yield strings. Normalize both (Task 4).
- `form.setErrorMap({ onServer: ... })` exists on FormApi
  (`@tanstack/form-core/dist/esm/FormApi.d.ts:496`); the onServer value shape is
  `{ form: Record<string, StandardSchemaV1Issue[]>, fields: Record<string, StandardSchemaV1Issue[]> }`
  (`FormApi.d.ts:516-517`). **Verify the exact literal accepted by `setErrorMap` against that
  .d.ts before writing Task 8's code** — it is the one API this plan did not exercise.
- `WallowError` (`packages/sdk/src/errors.ts`) has `status/code/title/detail` only — NO field
  errors dict. Task 2 adds it.
- Workspace versions to pin against: react `^19.2.7`, `@types/react` `^19.2.17`, vite `^8.1.4`,
  vitest `^4.1.10`, typescript `^5.6.0`, `@vitest/browser-playwright` `^4.1.10`,
  `vitest-browser-react` `^2.2.0`.

**Conventions that apply to every task here:**
- TDD: failing test → run (expect FAIL) → implement → run (expect PASS) → commit.
- Frontend tests: NEVER jsdom/happy-dom/jest. Node project for pure logic (`*.test.ts`), browser
  project (real Chromium) for anything rendering (`*.test.tsx`). Never mock `@bc-solutions-coder/ui`.
- Commits: conventional commits, lowercase, imperative, <72 chars first line.
- After EVERY task's tests pass, also run
  `pnpm --filter @bc-solutions-coder/forms typecheck` (or the touched package's filter).
- `docs/plans/` is gitignored — never `git add` anything under it.

---

## Task 1: Scaffold `packages/forms`

**Files:**
- Create: `packages/forms/package.json`
- Create: `packages/forms/tsconfig.json`, `packages/forms/tsconfig.build.json`
- Create: `packages/forms/vite.config.ts`
- Create: `packages/forms/vitest.config.ts`
- Create: `packages/forms/src/index.ts` (empty export for now)
- Reference (copy patterns from): `packages/ui/package.json`, `packages/ui/vite.config.ts`,
  `packages/ui/tsconfig.json`, `packages/ui/tsconfig.build.json`, `apps/wallow-auth/vitest.config.ts`

**Step 1: Write `package.json`**

```json
{
  "name": "@bc-solutions-coder/forms",
  "version": "0.0.0",
  "private": true,
  "description": "Wallow shared form layer: TanStack Form catalog bound to the ui component library",
  "files": ["dist"],
  "type": "module",
  "sideEffects": false,
  "main": "./dist/index.js",
  "module": "./dist/index.js",
  "types": "./dist/index.d.ts",
  "exports": {
    ".": {
      "types": "./dist/index.d.ts",
      "import": "./dist/index.js"
    }
  },
  "scripts": {
    "build": "vite build && tsc -p tsconfig.build.json",
    "test": "vitest run",
    "test:watch": "vitest",
    "typecheck": "tsc --noEmit"
  },
  "dependencies": {
    "@bc-solutions-coder/sdk": "workspace:*",
    "@bc-solutions-coder/ui": "workspace:*",
    "@tanstack/react-form": "^1.33.2",
    "zod": "^4.1.5"
  },
  "devDependencies": {
    "@bc-solutions-coder/styles": "workspace:*",
    "@bc-solutions-coder/testing": "workspace:*",
    "@tanstack/react-query": "^5.101.2",
    "@types/node": "^24.0.0",
    "@types/react": "^19.2.17",
    "@types/react-dom": "^19.2.3",
    "react": "^19.2.7",
    "react-dom": "^19.2.7",
    "typescript": "^5.6.0",
    "vite": "^8.1.4",
    "vitest": "^4.1.10"
  },
  "peerDependencies": {
    "@tanstack/react-query": "^5.101.2",
    "react": "^19.2.7",
    "react-dom": "^19.2.7"
  }
}
```

Check the current zod major on npm (`npm view zod version`) and use the latest 4.x. React,
react-dom, and react-query are peers (must share the app's instances); sdk and ui are regular
workspace deps (forms is private, never published).

**Step 2: Copy build/TS configs from ui**

- `vite.config.ts`: copy `packages/ui/vite.config.ts` but DELETE the `componentEntries()`
  helper and its spread — forms exposes only the root barrel, so `lib.entry` is just
  `{ index: fileURLToPath(new URL("src/index.ts", import.meta.url)) }`. Keep `preserveModules`,
  ES-only, externalize-everything.
- `tsconfig.json` / `tsconfig.build.json`: copy ui's, adjust any paths that mention
  components. Keep `jsx: react-jsx`, strict.
- `vitest.config.ts`: use `createVitestProjects` from `@bc-solutions-coder/testing` exactly as
  `apps/wallow-auth/vitest.config.ts` does (two projects: node + browser). Copy that app's
  `optimizeDeps` handling verbatim — the browser project renders `@bc-solutions-coder/ui`
  components, and ui's Base UI subpaths must be pre-bundle-listed or specs die on
  `Cannot read properties of null (reading 'useRef')` (see `packages/ui/CLAUDE.md`). Add
  `@tanstack/react-form`, `@tanstack/react-query`, and `zod` to the same include list.

**Step 3: Create the empty barrel**

`packages/forms/src/index.ts`:

```ts
export {};
```

**Step 4: Install and verify**

```bash
pnpm install
pnpm --filter @bc-solutions-coder/forms typecheck
pnpm --filter @bc-solutions-coder/forms build
```

Expected: both succeed (empty dist with an index.js).

**Step 5: Check root wiring**

- Root `package.json`: read the `check:exports` script. If it enumerates packages explicitly,
  decide whether forms belongs (it is private like ui — mirror whatever ui does; if ui is
  covered, cover forms the same way).
- `pnpm-workspace.yaml` already globs `packages/*` — no change.
- Run `pnpm lint` — oxlint covers `packages/` from the root; fix any config fallout. If ui's
  `.oxlintrc.json` disables `react/jsx-props-no-spreading` for passthrough props, forms needs
  the same override (create `packages/forms/.oxlintrc.json` copied from ui's).

**Step 6: Commit**

```bash
git add packages/forms pnpm-lock.yaml
git commit -m "feat(forms): scaffold @bc-solutions-coder/forms package"
```

---

## Task 2: SDK — expose RFC 7807 field errors on `WallowError`

The API's validation failures (ASP.NET `ValidationProblemDetails` / FluentValidation) carry an
`errors` member: `Record<string, string[]>` keyed by property name. `WallowError` drops it today;
forms needs it for field-level server errors.

**Files:**
- Modify: `packages/sdk/src/errors.ts`
- Modify: wherever `new WallowError(` is constructed from a response body — find with
  `grep -rn "new WallowError" packages/sdk/src` (expect the browser interceptor in
  `create-sdk.ts` and the server-side `parseProblemDetails` in `src/server/errors.ts`; update
  every constructor site that has a problem-details body in hand)
- Test: `packages/sdk/src/errors.test.ts` (extend) and the interceptor's existing spec
  (`error-contract.test.ts` — read it first; extend, don't restructure)

**Step 1: Write the failing tests**

In `errors.test.ts` — construction carries the dict through:

```ts
it("carries RFC 7807 validation errors when provided", () => {
  const error = new WallowError({
    status: 400,
    code: "VALIDATION_ERROR",
    title: "One or more validation errors occurred.",
    fieldErrors: { Name: ["'Name' must not be empty."], Email: ["Invalid email."] },
  });
  expect(error.fieldErrors).toEqual({
    Name: ["'Name' must not be empty."],
    Email: ["Invalid email."],
  });
});

it("leaves fieldErrors undefined when the problem details had none", () => {
  const error = new WallowError({ status: 500, code: "UNKNOWN", title: "boom" });
  expect(error.fieldErrors).toBeUndefined();
});
```

In the interceptor spec: feed a problem-details body containing
`"errors": { "Name": ["'Name' must not be empty."] }` and assert the rejected `WallowError` has
`fieldErrors.Name`. Mirror however that spec already fakes responses.

**Step 2: Run to verify they fail**

```bash
pnpm --filter @bc-solutions-coder/sdk test
```

Expected: FAIL — `fieldErrors` does not exist.

**Step 3: Implement**

In `errors.ts`, add to the class and the constructor init (keep the existing doc-comment style):

```ts
/** RFC 7807 `errors` member — validation messages keyed by property name, when the API sent them. */
readonly fieldErrors?: Readonly<Record<string, readonly string[]>>;
```

At each constructor site that parses a problem-details body, pass `errors` through when it is an
object of string arrays (validate shape defensively — never trust the body). Follow the sdk's
existing parsing conventions (see the `extensions.code` note in `packages/sdk/CLAUDE.md`).

**Step 4: Run tests, typecheck, rebuild**

```bash
pnpm --filter @bc-solutions-coder/sdk test
pnpm --filter @bc-solutions-coder/sdk typecheck
pnpm --filter @bc-solutions-coder/sdk build   # apps + forms typecheck against dist/
```

Expected: PASS.

**Step 5: Commit**

```bash
git add packages/sdk
git commit -m "feat(sdk): expose rfc 7807 validation errors on wallowerror"
```

---## Task 3: ui — add the missing `Textarea` component (forms-unaware)

`CreateInquiryForm` uses a raw `<textarea>` with a hand-measured class string. The catalog's
`TextareaField` needs a real ui `Textarea`. Per the layering rule this lands in **ui first, as a
normal component that knows nothing about forms**.

**Files:**
- Create: `packages/ui/src/components/textarea/{textarea.tsx,textarea.styles.ts,textarea.stories.tsx,index.ts}`
- Modify (the three catalog-set files that move together — see `packages/ui/CLAUDE.md`):
  `packages/ui/src/core/package-scaffold.test.ts` (`COMPONENT_FOLDERS`),
  `packages/ui/src/index.ts`,
  `packages/ui/src/index.test.ts` (`PUBLIC_RUNTIME_EXPORTS` + `PublicTypeExports`)
- Reference: `packages/ui/src/components/input/` (copy its structure exactly)

**Steps:**

1. Read `packages/ui/src/components/input/input.tsx` and `input.styles.ts`. Base UI has no
   textarea part, so `Textarea` wraps a native `<textarea>` (unlike Input's Base UI part) with a
   recipe copied from the input recipe (same border/background/focus-ring tokens; add
   `min-h-20` and `resize-y`). No new tokens should be needed — if one is, it goes to
   `packages/styles` + `api/branding.json` first.
2. Add the folder to the three catalog-set files **in the same commit**. Run
   `pnpm --filter @bc-solutions-coder/ui test` — the scaffold guards go red until all three
   agree; `dist-structure.test.ts` needs a rebuild:
   `pnpm --filter @bc-solutions-coder/ui build`.
3. Write one story (`textarea.stories.tsx`) with default + disabled states — stories are the
   render coverage; no separate `.test.tsx` unless there is a behavioural edge.
4. `pnpm --filter @bc-solutions-coder/ui test && pnpm --filter @bc-solutions-coder/ui build`
   Expected: PASS.
5. Commit:

```bash
git add packages/ui
git commit -m "feat(ui): add textarea component"
```

---

## Task 3b: ui — fix Select popup width and chevron icon (user-reported bug)

Two confirmed defects in the catalog `Select`, visible in every wallow-web form select:

1. **Popup is narrower than the trigger.** `selectPopupRecipe`
   (`packages/ui/src/components/select/select.styles.ts`) has no width rule, so the popup
   shrinks to its longest option. Base UI publishes the trigger's width as the
   `--anchor-width` CSS variable on `Select.Positioner` for exactly this.
2. **The chevron is a text glyph.** Call sites pass `▾` as `Select.Icon` children
   (`apps/wallow-web/src/components/SelectControl.tsx:83`,
   `packages/ui/src/components/select/select.stories.tsx:62`). A font glyph in the `size-4`
   icon box sits off-baseline and renders platform-dependently — the "weird arrow". The fix
   is a default inline SVG chevron (no new dependency — ui must not gain lucide-react).

**Files:**
- Modify: `packages/ui/src/components/select/select.styles.ts` (popup recipe)
- Modify: `packages/ui/src/components/select/select.tsx` (`SelectIcon` default children)
- Modify: `packages/ui/src/components/select/select.test.tsx` (this spec PINS each part's exact
  utility set — the recipe change goes red here by design; update the pinned constant)
- Modify: `packages/ui/src/components/select/select.stories.tsx` (drop the `▾` child)
- Modify: `apps/wallow-web/src/components/SelectControl.tsx` (drop the `▾` child so the ui
  default shows immediately, before the form migrations delete this file)
- Same-pattern extension: `packages/ui/src/components/combobox/combobox.tsx` Icon (Autocomplete
  reuses Combobox's parts, so this fixes both) + their stories' `▾` children — same two changes,
  same commit.

**Step 1: Write/adjust the failing tests**

- In `select.test.tsx`, extend the pinned popup utility constant with
  `min-w-[var(--anchor-width)]` (goes red until the recipe changes).
- Add a spec: `Select.Icon` with no children renders an `svg` (query
  `container.querySelector("svg")` inside the icon span); with children, renders the children
  instead (override preserved).

**Step 2: Run to verify failure** — `pnpm --filter @bc-solutions-coder/ui test` → FAIL.

**Step 3: Implement**

- `selectPopupRecipe`: append `min-w-[var(--anchor-width)]` to the base string. (The variable
  lives on the Positioner, an ancestor of the popup, so the `var()` resolves. Do NOT set a hard
  `w-`; longer options may still widen the popup past the trigger.)
- `SelectIcon`: when `children` is undefined, render a stroke-based chevron so the existing
  recipe (`size-4`, `data-[popup-open]:rotate-180`) drives it:

```tsx
function DefaultChevron(): ReactElement {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="size-4"
      aria-hidden="true"
    >
      <path d="m6 9 6 6 6-6" />
    </svg>
  );
}

function SelectIcon({ className, children, ...rest }: SelectIconProps): ReactElement {
  return (
    <BaseSelect.Icon className={cn(selectIconRecipe(), className)} {...rest}>
      {children ?? <DefaultChevron />}
    </BaseSelect.Icon>
  );
}
```

- Mirror both fixes on Combobox's Icon/popup recipes; update the four `▾` story call sites and
  `SelectControl.tsx` to pass no children.
- Remember `packages/ui/CLAUDE.md`: Tailwind `@source` scans `*.styles.ts`, so the new
  `min-w-[var(--anchor-width)]` utility needs no extra wiring — but verify it lands in a built
  app's CSS (grep `.output`/dev CSS for `--anchor-width`) before calling this done.

**Step 4: Run tests + rebuild**

```bash
pnpm --filter @bc-solutions-coder/ui test
pnpm --filter @bc-solutions-coder/ui build
pnpm --filter ./apps/wallow-web test
```

Expected: PASS. Visually confirm via Storybook (`pnpm --filter @bc-solutions-coder/ui storybook`)
or the running app: popup spans the trigger width; chevron is a crisp centered SVG that flips
when open.

**Step 5: Commit**

```bash
git add packages/ui apps/wallow-web/src/components/SelectControl.tsx
git commit -m "fix(ui): select popup matches trigger width, svg chevron default icon"
```

---

## Task 4: forms core — contexts, testid derivation, error normalization

**Files:**
- Create: `packages/forms/src/core/contexts.ts`
- Create: `packages/forms/src/core/test-id.ts`
- Create: `packages/forms/src/core/errors.ts`
- Test: `packages/forms/src/core/test-id.test.ts`, `packages/forms/src/core/errors.test.ts` (node project)

**Step 1: Write the failing tests**

`test-id.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { fieldErrorTestId, fieldTestId } from "./test-id";

describe("fieldTestId", () => {
  it("joins prefix and field name", () => {
    expect(fieldTestId("inquiry", "name")).toBe("inquiry-name");
  });

  it("kebab-cases camelCase field names", () => {
    expect(fieldTestId("inquiry", "projectType")).toBe("inquiry-project-type");
    expect(fieldTestId("inquiry", "budgetRange")).toBe("inquiry-budget-range");
  });
});

describe("fieldErrorTestId", () => {
  it("appends -error to the control testid", () => {
    expect(fieldErrorTestId("inquiry", "projectType")).toBe("inquiry-project-type-error");
  });
});
```

`errors.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { firstErrorMessage } from "./errors";

describe("firstErrorMessage", () => {
  it("returns undefined for no errors", () => {
    expect(firstErrorMessage([])).toBeUndefined();
  });

  it("returns a string error as-is (function validators)", () => {
    expect(firstErrorMessage(["Email is required"])).toBe("Email is required");
  });

  it("unwraps a standard-schema issue's message (zod validators)", () => {
    expect(firstErrorMessage([{ message: "This field is required" }])).toBe(
      "This field is required",
    );
  });

  it("returns undefined for unrecognizable shapes", () => {
    expect(firstErrorMessage([42])).toBeUndefined();
  });
});
```

**Step 2: Run to verify failure**

```bash
pnpm --filter @bc-solutions-coder/forms test
```

Expected: FAIL — modules do not exist.

**Step 3: Implement**

`core/contexts.ts` — pure wiring, covered by typecheck + downstream tests:

```ts
import { createFormHookContexts } from "@tanstack/react-form";

/**
 * The shared contexts every catalog field and the form shell hang off. Created
 * once at module scope — createFormHook (core/form-hook.ts) and every field
 * component must import THESE instances, never call createFormHookContexts again.
 */
export const { fieldContext, formContext, useFieldContext, useFormContext } =
  createFormHookContexts();
```

`core/test-id.ts`:

```ts
/**
 * Testid derivation for the repo's `{page}-{element}` convention (.claude/rules/E2E.md).
 * Derived, not hand-written: `testIdPrefix="inquiry"` + field `projectType` →
 * `inquiry-project-type` / `inquiry-project-type-error`. A field's `testId` prop
 * overrides the derivation so migrated forms keep their E2E ids byte-identical.
 */
const kebab = (name: string): string =>
  name.replace(/[A-Z]/gu, (c) => `-${c.toLowerCase()}`).replace(/\./gu, "-");

export function fieldTestId(prefix: string, fieldName: string): string {
  return `${prefix}-${kebab(fieldName)}`;
}

export function fieldErrorTestId(prefix: string, fieldName: string): string {
  return `${fieldTestId(prefix, fieldName)}-error`;
}
```

`core/errors.ts`:

```ts
/**
 * First displayable message from a TanStack field's `state.meta.errors`.
 * Zod (standard schema) validators yield `{ message }` issue objects; function
 * validators yield strings. Both normalize to the string the Field.Error shows.
 */
export function firstErrorMessage(errors: ReadonlyArray<unknown>): string | undefined {
  const first = errors[0];
  if (typeof first === "string") {
    return first;
  }
  if (
    typeof first === "object" &&
    first !== null &&
    "message" in first &&
    typeof (first as { message: unknown }).message === "string"
  ) {
    return (first as { message: string }).message;
  }
  return undefined;
}
```

**Step 4: Run tests**

```bash
pnpm --filter @bc-solutions-coder/forms test
pnpm --filter @bc-solutions-coder/forms typecheck
```

Expected: PASS.

**Step 5: Commit**

```bash
git add packages/forms
git commit -m "feat(forms): add core contexts, testid derivation, error normalization"
```

---

## Task 5: `AppFormContext` + `AppForm` shell + `SubmitButton` + `FormError`

**Files:**
- Create: `packages/forms/src/form/app-form-context.ts`
- Create: `packages/forms/src/form/app-form.tsx`
- Create: `packages/forms/src/form/submit-button.tsx`
- Create: `packages/forms/src/form/form-error.tsx`
- Test: `packages/forms/src/form/app-form.test.tsx` (browser project)

**Step 1: Write the failing browser test**

Renders a minimal raw TanStack form through the shell (the catalog fields don't exist yet, so
drive `form.handleSubmit` via the shell's `<form>` submit):

```tsx
import { render } from "vitest-browser-react";
import { describe, expect, it, vi } from "vitest";

import { AppForm } from "./app-form";
import { FormError } from "./form-error";
import { SubmitButton } from "./submit-button";
// build a form with the package's own hook contexts — import the createFormHook
// result once Task 6 lands; for THIS task use a thin local harness component that
// calls useForm from @tanstack/react-form and passes it in.

describe("AppForm shell", () => {
  it("renders a <form> stamped with `{prefix}-form` testid and submits without navigation", async () => {
    const onSubmit = vi.fn();
    const screen = render(<Harness onSubmit={onSubmit} />);
    await screen.getByTestId("demo-submit").click();
    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("SubmitButton disables and swaps label while pending", async () => {
    const screen = render(<Harness pending pendingLabel="Sending..." />);
    const button = screen.getByTestId("demo-submit");
    await expect.element(button).toBeDisabled();
    await expect.element(button).toHaveTextContent("Sending...");
  });

  it("FormError renders the shell's serverError with `{prefix}-error` testid, nothing when null", async () => {
    const screen = render(<Harness serverError="Could not submit." />);
    await expect.element(screen.getByTestId("demo-error")).toHaveTextContent("Could not submit.");
  });
});
```

Write the `Harness` in the test file: it calls `useForm` from `@tanstack/react-form` and renders
`<AppForm form={form} testIdPrefix="demo" serverError={...} pending={...}>` with children
`<FormError />` and `<SubmitButton pendingLabel={...}>Send</SubmitButton>`.

**Step 2: Run to verify failure** — `pnpm --filter @bc-solutions-coder/forms test` → FAIL.

**Step 3: Implement**

`form/app-form-context.ts`:

```ts
import { createContext, useContext } from "react";

/** What the AppForm shell publishes to FormError, SubmitButton, and every catalog field. */
export interface AppFormContextValue {
  readonly testIdPrefix: string;
  readonly pending: boolean;
  readonly serverError: string | null;
}

export const AppFormContext = createContext<AppFormContextValue | null>(null);

export function useAppFormContext(): AppFormContextValue {
  const value = useContext(AppFormContext);
  if (value === null) {
    throw new Error("forms components must render inside <AppForm>");
  }
  return value;
}
```

`form/app-form.tsx` — owns the `<form>` element and the submit boilerplate every current form
hand-writes (`preventDefault`/`stopPropagation`/`void form.handleSubmit()`), plus vertical rhythm:

```tsx
import type { ReactElement, ReactNode } from "react";

import { AppFormContext } from "./app-form-context";

/** The minimal form surface the shell needs — satisfied by any TanStack form instance. */
export interface AppFormInstance {
  handleSubmit: () => Promise<void>;
}

export interface AppFormProps {
  readonly form: AppFormInstance;
  readonly testIdPrefix: string;
  /** Pending/serverError normally come from useAppForm's wallow extras (Task 6). */
  readonly pending?: boolean;
  readonly serverError?: string | null;
  readonly children: ReactNode;
  readonly className?: string;
}

export function AppForm({
  form,
  testIdPrefix,
  pending = false,
  serverError = null,
  children,
  className,
}: AppFormProps): ReactElement {
  return (
    <AppFormContext.Provider value={{ testIdPrefix, pending, serverError }}>
      <form
        data-testid={`${testIdPrefix}-form`}
        className={className ?? "space-y-5"}
        onSubmit={(e) => {
          e.preventDefault();
          e.stopPropagation();
          void form.handleSubmit();
        }}
        noValidate
      >
        {children}
      </form>
    </AppFormContext.Provider>
  );
}
```

(`noValidate` because zod, not the browser, owns validation — otherwise a `type="email"` control
would double-validate.)

`form/submit-button.tsx` — ui `Button`, `type="submit"`, testid `{prefix}-submit`, disabled +
`pendingLabel` swap while `pending`. `form/form-error.tsx` — renders `null` when `serverError`
is null, else ui `ErrorBanner` with testid `{prefix}-error`. Both read `useAppFormContext()`;
both accept a `testId` override prop.

**Step 4: Run tests** → PASS. **Step 5:** typecheck. **Step 6: Commit**

```bash
git add packages/forms
git commit -m "feat(forms): add appform shell, submitbutton, formerror"
```

---

## Task 6: `useAppForm` — createFormHook + zod + mutation unification

**Files:**
- Create: `packages/forms/src/core/form-hook.tsx` (the `createFormHook` call — field components
  register here as they land; starts with an empty `fieldComponents: {}`)
- Create: `packages/forms/src/form/use-app-form.ts` (the public hook)
- Create: `packages/forms/src/core/server-error.ts`
- Test: `packages/forms/src/form/use-app-form.test.tsx` (browser project),
  `packages/forms/src/core/server-error.test.ts` (node)

**Step 1: Failing tests**

`server-error.test.ts` — mapping a `WallowError` (import the real class from
`@bc-solutions-coder/sdk`; never a hand-rolled duck):

```ts
import { WallowError } from "@bc-solutions-coder/sdk";
import { describe, expect, it } from "vitest";

import { splitServerError } from "./server-error";

const KNOWN_FIELDS = ["name", "email"];

describe("splitServerError", () => {
  it("maps matching field errors (PascalCase from FluentValidation → camelCase field names)", () => {
    const error = new WallowError({
      status: 400, code: "VALIDATION_ERROR", title: "Validation failed",
      fieldErrors: { Name: ["'Name' must not be empty."] },
    });
    const result = splitServerError(error, KNOWN_FIELDS, "fallback");
    expect(result.fieldErrors).toEqual({ name: ["'Name' must not be empty."] });
    expect(result.formError).toBeNull();
  });

  it("routes unmatched field names to the form-level error instead of dropping them", () => {
    const error = new WallowError({
      status: 400, code: "VALIDATION_ERROR", title: "Validation failed",
      fieldErrors: { Surprise: ["Nope."] },
    });
    const result = splitServerError(error, KNOWN_FIELDS, "fallback");
    expect(result.fieldErrors).toEqual({});
    expect(result.formError).toBe("Nope.");
  });

  it("uses detail for a WallowError without field errors", () => {
    const error = new WallowError({ status: 409, code: "CONFLICT", title: "Conflict", detail: "Name taken." });
    expect(splitServerError(error, KNOWN_FIELDS, "fallback").formError).toBe("Name taken.");
  });

  it("falls back for non-wallow errors", () => {
    expect(splitServerError(new Error(""), KNOWN_FIELDS, "fallback").formError).toBe("fallback");
  });
});
```

`use-app-form.test.tsx` — a harness component using the real hook + shell (no catalog fields yet;
submit through the shell). Cover: (a) zod `onSubmit` validation blocks the submit callback;
(b) a passing submit calls the mutation exactly once with `{ body: values }` (default
`toVariables`); (c) the no-mutation `onSubmit` path still reports `pending` while the promise is
in flight; (d) a rejecting mutationFn whose error is a `WallowError` with `detail` surfaces it
via `FormError`. Use a locally-constructed `QueryClientProvider` (real `@tanstack/react-query`,
never mocked) around the harness.

**Step 2: Run to verify failure.**

**Step 3: Implement**

`core/server-error.ts`:

```ts
import { isWallowError } from "@bc-solutions-coder/sdk";

export interface SplitServerError {
  /** camelCase field name → messages, only for names the form actually has. */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  /** The banner text, or null when everything mapped onto fields. */
  readonly formError: string | null;
}

const camel = (name: string): string => name.charAt(0).toLowerCase() + name.slice(1);

/**
 * Splits a failed submit into field-level and form-level surfaces (design §3).
 * Field names arrive as the API's property names (typically PascalCase from
 * FluentValidation); they match the form's camelCase values by case-insensitive
 * first-letter fold. Unmatched entries join the form-level banner rather than
 * vanishing; a WallowError without field errors contributes its RFC 7807 detail;
 * anything unrecognized contributes `fallback`.
 */
export function splitServerError(
  error: unknown,
  knownFields: readonly string[],
  fallback: string,
): SplitServerError {
  if (!isWallowError(error)) {
    const message = error instanceof Error && error.message !== "" ? error.message : fallback;
    return { fieldErrors: {}, formError: message };
  }
  const matched: Record<string, readonly string[]> = {};
  const unmatched: string[] = [];
  for (const [name, messages] of Object.entries(error.fieldErrors ?? {})) {
    const field = camel(name);
    if (knownFields.includes(field)) {
      matched[field] = messages;
    } else {
      unmatched.push(...messages);
    }
  }
  const formError =
    unmatched.length > 0 ? unmatched.join(" ") : Object.keys(matched).length > 0 ? null : (error.detail ?? fallback);
  return { fieldErrors: matched, formError };
}
```

`core/form-hook.tsx`:

```tsx
import { createFormHook } from "@tanstack/react-form";

import { fieldContext, formContext } from "./contexts";

/**
 * The one createFormHook call. Every catalog field registers here; each entry in
 * fieldComponents becomes available on the AppField render-prop object (f.TextField…).
 */
export const { useAppForm: useTanstackAppForm, withForm } = createFormHook({
  fieldContext,
  formContext,
  fieldComponents: {},
  formComponents: {},
});
```

`form/use-app-form.ts` — the public hook. Shape (fill in the generics against the real
`UseMutationOptions` and zod types while implementing; keep explicit types per CONVENTIONS):

```ts
import { useMutation, type UseMutationOptions } from "@tanstack/react-query";
import { useState } from "react";
import type { z } from "zod";

import { splitServerError } from "../core/server-error";
import { useTanstackAppForm } from "../core/form-hook";

export interface UseAppFormOptions<TSchema extends z.ZodType, TVariables, TData> {
  readonly schema: TSchema;
  readonly defaultValues: z.input<TSchema>;
  /** Generated SDK mutation options ({op}Mutation({ client })). Omit for the plain-onSubmit escape hatch. */
  readonly mutation?: UseMutationOptions<TData, unknown, TVariables>;
  /** Values → mutation variables. Default: (values) => ({ body: values }). */
  readonly toVariables?: (values: z.output<TSchema>) => TVariables;
  /** No-mutation escape hatch (e.g. ForgotPassword's anti-enumeration swallow). Runs through an internal useMutation so pending still works. */
  readonly onSubmit?: (values: z.output<TSchema>) => Promise<void> | void;
  readonly onSuccess?: (data: TData) => void;
  /** Banner text when a failure carries no usable message. */
  readonly fallbackError?: string;
}
```

Implementation outline (all inside the hook, in this order):

1. `const mutation = useMutation(options.mutation ?? { mutationFn: async (values) => { await options.onSubmit?.(values); } })` —
   exactly one `useMutation` call on every path (hooks rules).
2. `const form = useTanstackAppForm({ defaultValues, validators: { onSubmit: options.schema }, onSubmit: ({ value }) => { ... } })`.
   The inner onSubmit builds variables (`toVariables` default `{ body: value }` when
   `options.mutation` is set; identity otherwise) and calls `mutation.mutate(vars, { onSuccess, onError })`
   — fire-and-observe with `mutate`, NEVER awaited `mutateAsync` (the repo's established rule;
   see `CreateInquiryFormFields`' comment).
3. `onError`: `const split = splitServerError(error, Object.keys(options.defaultValues), fallback)`;
   store `split.formError` in a `useState`; when `split.fieldErrors` is non-empty, push them onto
   the form via `form.setErrorMap({ onServer: ... })` — **check the exact accepted literal against
   `@tanstack/form-core` FormApi.d.ts:496/516** and wrap messages as `[{ message }]` issues.
   Clear the stored formError at the start of the next submit.
4. Return `Object.assign(form, { wallow: { pending: mutation.isPending, serverError, reset: mutation.reset } })` —
   augmenting the instance is the library's own createFormHook pattern. Type the return as the
   tanstack form type intersected with `{ wallow: WallowFormExtras }`.
5. `AppForm` usage then reads `<AppForm form={form} testIdPrefix="x" pending={form.wallow.pending} serverError={form.wallow.serverError}>`.
   To keep call sites terse, ALSO have `AppForm` default `pending`/`serverError` from
   `form.wallow` when the prop is omitted and the member exists.

**Step 4: Run tests + typecheck** → PASS.

**Step 5: Commit**

```bash
git add packages/forms
git commit -m "feat(forms): add useappform hook with zod and mutation unification"
```

---

## Task 7: Catalog fields — `TextField` first (the template), then the rest

**Files (TextField):**
- Create: `packages/forms/src/fields/text-field.tsx`
- Modify: `packages/forms/src/core/form-hook.tsx` (register in `fieldComponents`)
- Test: `packages/forms/src/fields/text-field.test.tsx` (browser)

**Step 1: Failing test** — through the REAL pipeline (useAppForm + AppForm + AppField), not in
isolation:

```tsx
// Harness: schema z.object({ name: z.string().trim().min(1, "This field is required") }),
// defaultValues { name: "" }, onSubmit spy; renders
// <AppForm form={form} testIdPrefix="demo">
//   <form.AppField name="name">{(f) => <f.TextField label="Name" />}</form.AppField>
//   <SubmitButton>Go</SubmitButton>
// </AppForm>

it("renders label associated with the input and derives the testid", async () => {
  const screen = render(<Harness />);
  const input = screen.getByTestId("demo-name");
  await expect.element(input).toBeVisible();
  await expect.element(screen.getByLabelText("Name")).toBeVisible();
});

it("shows the zod message under the field on failed submit, with derived error testid", async () => {
  const screen = render(<Harness />);
  await screen.getByTestId("demo-submit").click();
  await expect.element(screen.getByTestId("demo-name-error")).toHaveTextContent("This field is required");
});

it("clears the error once the value is corrected and resubmitted", async () => {
  const screen = render(<Harness />);
  await screen.getByTestId("demo-submit").click();
  await screen.getByTestId("demo-name").fill("Ada");
  await screen.getByTestId("demo-submit").click();
  await expect.poll(() => screen.container.querySelector('[data-testid="demo-name-error"]')).toBeNull();
});

it("testId prop overrides derivation", async () => {
  const screen = render(<Harness testId="legacy-email" />);
  await expect.element(screen.getByTestId("legacy-email")).toBeVisible();
});
```

**Step 2: Run → FAIL.**

**Step 3: Implement `text-field.tsx`**

```tsx
import { Field } from "@bc-solutions-coder/ui";
import { Input } from "@bc-solutions-coder/ui";
import type { ReactElement } from "react";

import { useFieldContext } from "../core/contexts";
import { firstErrorMessage } from "../core/errors";
import { fieldErrorTestId, fieldTestId } from "../core/test-id";
import { useAppFormContext } from "../form/app-form-context";

export interface TextFieldProps {
  readonly label: string;
  readonly type?: "text" | "email" | "password";
  readonly placeholder?: string;
  readonly optional?: boolean;
  /** Overrides the derived `{prefix}-{name}` testid (migration compatibility). */
  readonly testId?: string;
  readonly autoComplete?: string;
}

/**
 * The catalog text field: ui Field row (Label auto-associated with the control,
 * Field.Error auto-associated with both) bound to the TanStack field this
 * renders under via AppField. One look for every text input in every form.
 */
export function TextField({
  label,
  type = "text",
  placeholder,
  optional = false,
  testId,
  autoComplete,
}: TextFieldProps): ReactElement {
  const field = useFieldContext<string>();
  const { testIdPrefix, pending } = useAppFormContext();
  const error = firstErrorMessage(field.state.meta.errors);
  const controlTestId = testId ?? fieldTestId(testIdPrefix, field.name);

  return (
    <Field invalid={error !== undefined}>
      <Field.Label>
        {label}
        {optional ? <span className="text-muted-foreground"> (optional)</span> : null}
      </Field.Label>
      <Input
        type={type}
        placeholder={placeholder}
        autoComplete={autoComplete}
        disabled={pending}
        data-testid={controlTestId}
        value={field.state.value}
        onChange={(e) => {
          field.handleChange(e.target.value);
        }}
        onBlur={field.handleBlur}
      />
      {error === undefined ? null : (
        <Field.Error match data-testid={testId !== undefined ? `${testId}-error` : fieldErrorTestId(testIdPrefix, field.name)}>
          {error}
        </Field.Error>
      )}
    </Field>
  );
}
```

Check `Field.Error`'s real props in `packages/ui/src/components/field/field.tsx` — it renders
only when its `match` prop is satisfied or Base UI marks the field invalid; passing `match`
(always-show) plus conditional rendering keeps display authority with TanStack state. Adjust to
whatever the ui part actually accepts.

Register it: `fieldComponents: { TextField }` in `core/form-hook.tsx`.

**Step 4: Run → PASS. Commit:** `feat(forms): add textfield catalog field`

**Then the remaining five fields, one commit each, same TDD loop, mirroring TextField:**

| Field | ui parts | Notes |
| --- | --- | --- |
| `PasswordField` | `Input type="password"` | Thin variant of TextField; exists so authoring reads declaratively and autocomplete defaults (`current-password`/`new-password`) are baked in. |
| `TextareaField` | `Textarea` (Task 3) | Same anatomy, `textarea` control. |
| `SelectField` | `Select` | Props: `options: readonly { value, label }[]`, `placeholder`. Mirror the anatomy in `apps/wallow-web/src/components/SelectControl.tsx` (read it first) but through ui `Select` directly; the testid goes on the trigger. Use `<Select.Icon />` with NO children — Task 3b's default SVG chevron. Carry over SelectControl's two boundary translations (`""` ↔ `null`, `items` for label display). |
| `CheckboxField` | `Checkbox` + `Field.Label` | Boolean value; label to the right; error below. |
| `OtpField` | ui `OtpField` | Check ui's otp-field folder for its part names/props before writing. Needed by wallow-auth MFA screens (future) and ResetPassword if it uses a code input — check the form first; if nothing needs it yet, SKIP it (YAGNI) and note that in the commit. |

After the last field: `pnpm --filter @bc-solutions-coder/forms build` and commit.

---

## Task 8: Public barrel + package quality gate

**Files:**
- Modify: `packages/forms/src/index.ts`
- Test: `packages/forms/src/index.test.ts` (node — pin the public surface, mirroring ui's
  `PUBLIC_RUNTIME_EXPORTS` pattern in `packages/ui/src/index.test.ts`)

Barrel exports: `useAppForm`, `AppForm`, `SubmitButton`, `FormError`, `withForm`, every catalog
field component + its props type, `fieldTestId`/`fieldErrorTestId`, `splitServerError`, and the
option/context types. NOT the raw contexts (`fieldContext` etc.) — internals stay internal.

TDD the surface-pin test, implement the barrel, then:

```bash
pnpm --filter @bc-solutions-coder/forms test
pnpm --filter @bc-solutions-coder/forms build
pnpm --filter @bc-solutions-coder/forms typecheck
pnpm lint && pnpm format:check
```

Commit: `feat(forms): export public surface`

---

## Task 9–13: Migrate the five forms (one task, one commit each)

**Order (simplest → hardest):**

| # | Form | App | Proves |
| --- | --- | --- | --- |
| 9 | `ForgotPasswordForm` | wallow-auth | no-mutation escape hatch + testid overrides |
| 10 | `ResetPasswordForm` | wallow-auth | password fields, multi-field validation |
| 11 | `CreateOrganizationForm` | wallow-web | the canonical mutation path (smallest wallow-web form) |
| 12 | `RegisterAppForm` | wallow-web | checkbox/select variety (read the form first) |
| 13 | `CreateInquiryForm` | wallow-web | selects + textarea, the largest form |

**The invariant for every migration:** existing component tests and E2E specs pass **unchanged**.
Testids are pinned by the E2E suites and must survive byte-identical — use the `testId` override
prop wherever the derived id would differ from the legacy id (e.g. ForgotPassword's control is
`forgot-password-email` under prefix `forgot-password`, which derivation produces — but its
input `id`/label wiring changes, and the legacy `{page}-error` ids must be checked one by one
against the spec files in `apps/<app>/e2e/` and co-located `*.test.tsx` before porting).

**Per-form procedure (bite-sized):**

1. Read the form file, its co-located tests, and every e2e spec referencing its testids
   (`grep -rn "<prefix>-" apps/<app>/e2e apps/<app>/src`).
2. Add `@bc-solutions-coder/forms": "workspace:*"` to the app's `package.json` (first migration
   in each app only) + `pnpm install`.
3. Run the form's existing tests FIRST to establish green baseline:
   `pnpm --filter ./apps/<app> test -- <FormName>`.
4. Write the zod schema mirroring the form's current validators exactly (same messages —
   "This field is required" vs "Email is required" differ per form; preserve each form's
   current message strings verbatim, they are asserted in specs).
5. Rewrite the component on `useAppForm` + `AppForm` + catalog fields. Delete the local
   presentational field components and the hand-rolled `<form>` boilerplate. Keep each form's
   surrounding Card/heading/success-state structure untouched.
6. Special cases:
   - **ForgotPassword (Task 9):** keep the entire anti-enumeration comment block and behavior —
     `useAppForm({ schema, onSubmit })` where onSubmit trims, calls the operation, and swallows
     (`try/catch` around `accountForgotPassword`); NO `FormError` in the tree (the spec asserts
     the testid's absence). The submitted/confirmation state swap stays in the parent exactly
     as-is.
   - **CreateInquiry (Task 13):** per-field errors currently render through `ErrorBanner`; they
     become `Field.Error` text (design §3's intended unification). The error **testids** stay,
     so specs keep passing; if a spec asserts banner-specific classes, flag it — per E2E.md
     specs must assert app-level signals, so fix the spec in the same commit and say so.
   - **wallow-web forms:** keep invalidation via `queriesWithTag` in `onSuccess` exactly as today.
7. Run: the app's vitest suite, then typecheck, then the app's route-reachability e2e
   (`pnpm --filter ./apps/<app> exec playwright test routes.spec.ts`). Backend-dependent specs
   need the stack — run `./scripts/e2e.sh` once after Task 10 (covers wallow-auth) and the
   wallow-web suite after Task 13 if infra is available; otherwise state plainly in the final
   report which suites ran and which need the backend. **wallow-web caveat:** until
   Wallow-vufu.1 lands, live dashboard mutations 403 on the known CSRF regression (see
   "Execution process" above) — a 403 during Tasks 11–13 verification is that bug, not the
   migration; verify via specs, note it in the commit, and re-verify after the final rebase.
8. Commit: `refactor(<app-scope>): migrate <form> to @bc-solutions-coder/forms`

---

## Task 14: Docs + repo-map updates

**Files:**
- Create: `docs/development/forms.md`
- Modify: `docs/toc.yml` (add the new page — see `docs/CLAUDE.md` rules)
- Modify: root `CLAUDE.md` (add `packages/forms/` row to the Repository Layout table)
- Modify: `apps/CLAUDE.md` (the "five `@bc-solutions-coder` workspace packages" wording → six,
  add forms to the list)
- Create: `packages/forms/CLAUDE.md` (short agent guide: layering rule — forms consumes ui, ui
  never knows about forms; how to add a catalog field: component + registration in
  `core/form-hook.tsx` + barrel + surface-pin test; testing model)

`docs/development/forms.md` contents: why the package exists, the full authoring example (crib
the design doc §2's InquiryForm sample, updated to the as-built API), the error model table
(design §3), the styling conventions (design §4), how to add a catalog field, and the two escape
hatches. Follow docs/CLAUDE.md conventions (lowercase-kebab, toc entry).

Verify the docs build if the DocFX container is available (`docfx.json` at root); otherwise note it.

Commit: `docs: add forms package guide and update repo maps`

---

## Task 15: Full quality gate + worktree merge-back + session completion

1. **Final rebase** (in the worktree): `git pull --rebase origin main`. Resolve per the
   conflict table in "Execution process". If sdk changes came in, rebuild it first.
2. `pnpm --filter @bc-solutions-coder/sdk build && pnpm build` (SDK first — apps typecheck
   against dist).
3. `pnpm check` — the one-command gate (format:check + lint + typecheck + test + build +
   check:exports). Fix anything it surfaces.
4. E2E on the REBASED result: `./scripts/e2e.sh`. If Wallow-vufu.1/1.3 have landed, this run
   is also the live proof that migrated forms mutate successfully (the CSRF fix) and that
   vufu's new CSRF spec passes against the migrated forms' preserved testids. If a wallow-web
   form 403s here and vufu.1 has NOT landed, record it as the known upstream bug — do not
   patch around it in forms.
5. **Merge back:** `git checkout main && git merge --ff-only feat/forms-package` (linear
   history; conventional commits stay release-parseable). If ff-only fails, main moved —
   go back to step 1.
6. Beads: file follow-up issues (`bd create`) for anything deferred (e.g. OtpField if skipped,
   migrating future MFA screens onto forms, Storybook for forms, live re-verification of
   wallow-web mutations if blocked on vufu.1). Close/update in-progress beads.
7. `git pull --rebase && bd dolt push && git push` — work is NOT complete until push succeeds;
   verify `git status` shows "up to date with origin". Then
   `git worktree remove ../Wallow-forms`.
