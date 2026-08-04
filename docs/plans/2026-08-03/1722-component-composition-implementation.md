# Component Composition Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**status: completed**

**Goal:** Cut the size of both apps' components by composing the layers the workspace already
has — migrate 11 hand-rolled forms onto `@bc-solutions-coder/forms`, extract 5 hooks, and give
`wallow-auth` the screen shell its 16 screens each re-derive.

**Architecture:** Bottom-up through the existing `styles → ui → forms → apps` layering. Two
generic additions to the catalog, one app-local screen shell, one shared hook plus four
feature-local ones, then the form migrations one screen per commit. No new framework-level
abstraction is introduced.

**Tech Stack:** React 19, TanStack Form + Query, zod, Base UI, CVA, Tailwind v4, Vitest browser
mode (real headless Chromium), Playwright, oxlint.

**Design:** `docs/plans/2026-08-03/1722-component-composition-design.md`

---

## Before you start — read these

- `packages/ui/CLAUDE.md` — component folder anatomy, the two-file export pinning rule
- `packages/forms/CLAUDE.md` — the form-authoring API and its gotchas
- `apps/CLAUDE.md` — the zone DAG, the lint rules both apps enable
- `.claude/rules/TESTING.md` — browser mode, no jsdom, no source tests
- `packages/testing/CLAUDE.md` — before writing any spec

## Facts that will bite you

1. **`react/jsx-max-depth` is 2** in `packages/ui`, `packages/forms` and both apps, and
   `pnpm lint` runs `--deny-warnings`. Split a nested tree into one component per level.
2. **In-repo, packages resolve from `src/`**, not `dist/` — no rebuild between a package edit
   and an app spec. (`packages/forms/CLAUDE.md` says otherwise; Task 12 fixes that.)
3. **`data-testid` values are E2E contract.** `fieldTestId` kebab-derives from the field name;
   when derivation does not match what the spec fills, pass an explicit `testId`.
4. **Real CSS is required in browser specs.** Without the stylesheet a `Checkbox.Root`
   measures 0×0 and a clicking spec hangs to Playwright's timeout.
5. **Never mock `@bc-solutions-coder/ui`.** Repo-wide rule.
6. **`z.string().trim()` does not trim submitted values** — TanStack discards the parsed
   output. `.trim()` only makes `"   "` fail a `.min(1)`.

---

## Phase 1 — Catalog additions

### Task 1: `CardHeader`

**Files:**
- Create: `packages/ui/src/components/card/card-header.tsx`
- Modify: `packages/ui/src/components/card/index.ts`
- Modify: `packages/ui/src/index.ts`
- Modify: `packages/ui/src/index.test.ts`
- Create: `packages/ui/src/components/card/card-header.test.tsx`

`CardTitle` already renders `<h2>` with `cardTitleRecipe` (`text-xl font-semibold
text-card-foreground`), which is the same computed style as the screens' `<Text as="h2"
variant="subheading" color="onCard">`. `CardHeader` composes it with `MutedText` — no new recipe.

**Step 1: Write the failing test**

```tsx
// packages/ui/src/components/card/card-header.test.tsx
import { render, screen } from "@bc-solutions-coder/testing/browser";
import { expect, test } from "vitest";

import { CardHeader } from "./card-header";

test("renders the title as the card's h2", () => {
  render(<CardHeader title="Create an account" />);

  expect(screen.getByRole("heading", { level: 2 }).textContent).toBe("Create an account");
});

test("omits the description element when no description is given", () => {
  render(<CardHeader title="Create an account" />);

  expect(screen.queryByText("Enter your details")).toBeNull();
});

test("renders the description beneath the title when given", () => {
  render(<CardHeader title="Create an account" description="Enter your details" />);

  expect(screen.getByText("Enter your details")).toBeTruthy();
});

test("passes data-testid through to the wrapper", () => {
  render(<CardHeader title="T" data-testid="register-heading" />);

  expect(screen.getByTestId("register-heading")).toBeTruthy();
});
```

**Step 2: Run it and watch it fail**

```bash
pnpm --filter @bc-solutions-coder/ui test -- card-header
```

Expected: FAIL — `Failed to resolve import "./card-header"`.

**Step 3: Write the implementation**

```tsx
// packages/ui/src/components/card/card-header.tsx
import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { MutedText } from "../muted-text/muted-text";
import { CardTitle } from "./card";

export interface CardHeaderProps extends Omit<HTMLAttributes<HTMLDivElement>, "title"> {
  /** The card's heading. Rendered by `CardTitle`, so it is the surface's one `<h2>`. */
  readonly title: string;
  /** Optional supporting copy beneath the title. */
  readonly description?: string;
}

/**
 * Title-and-description pair for a card surface. Sourced from 11 hand-rolled
 * `CardHeading` functions across wallow-auth, which each rebuilt this stack.
 *
 * The `<h2>` lives HERE rather than at the call site, so the 20px card-heading
 * standard is guaranteed by construction rather than by `wallow/text-heading-variant`
 * catching each screen.
 */
export function CardHeader({
  title,
  description,
  className,
  ...rest
}: CardHeaderProps): ReactElement {
  return (
    <div className={cn("space-y-1", className)} {...rest}>
      <CardTitle>{title}</CardTitle>
      {description === undefined ? null : <MutedText>{description}</MutedText>}
    </div>
  );
}
```

**Step 4: Wire the exports — all four files in one commit**

`packages/ui/src/components/card/index.ts`, add to the `<name>.tsx` block:

```ts
export { CardHeader, type CardHeaderProps } from "./card-header";
```

`packages/ui/src/index.ts:62`, extend the card line:

```ts
export { Card, type CardProps, CardHeader, type CardHeaderProps, CardTitle, type CardTitleProps } from "./components/card";
```

`packages/ui/src/index.test.ts` — add `"CardHeader"` to `PUBLIC_RUNTIME_EXPORTS` and
`CardHeaderProps` to the `PublicTypeExports` tuple. Growing one alone turns the other red.

**Step 5: Run tests**

```bash
pnpm --filter @bc-solutions-coder/ui test
```

Expected: PASS, including `src/index.test.ts`.

**Step 6: Commit**

```bash
git add packages/ui/src
git commit -m "feat(ui): add CardHeader for the title-and-description pair"
```

---

### Task 2: `QuietLink`

**Files:**
- Create: `packages/ui/src/components/quiet-link/{quiet-link.tsx,quiet-link.styles.ts,index.ts,quiet-link.test.tsx,quiet-link.stories.tsx}`
- Modify: `packages/ui/src/index.ts`, `packages/ui/src/index.test.ts`

13 call sites, three spellings. The dominant one is `text-sm text-muted-foreground
hover:text-foreground` (9 sites); `hover:text-primary` appears twice and one site adds `block
text-center`. Standardise on the dominant spelling; callers needing the layout variants pass
`className`.

**Step 1: Write the failing test**

```tsx
// packages/ui/src/components/quiet-link/quiet-link.test.tsx
import { render, screen } from "@bc-solutions-coder/testing/browser";
import { expect, test } from "vitest";

import { QuietLink } from "./quiet-link";

test("renders an anchor to the given href", () => {
  render(<QuietLink href="/login">Back to sign in</QuietLink>);

  expect(screen.getByRole("link", { name: "Back to sign in" }).getAttribute("href")).toBe("/login");
});

test("merges a caller className over the recipe", () => {
  render(
    <QuietLink href="/login" className="block text-center">
      Back
    </QuietLink>,
  );

  expect(screen.getByRole("link", { name: "Back" }).className).toContain("text-center");
});
```

**Step 2: Run it and watch it fail**

```bash
pnpm --filter @bc-solutions-coder/ui test -- quiet-link
```

Expected: FAIL — unresolved import.

**Step 3: Write the recipe and the component**

```ts
// packages/ui/src/components/quiet-link/quiet-link.styles.ts
import { cva, type VariantProps } from "class-variance-authority";

/**
 * The muted secondary link. Sourced from 13x `text-sm text-muted-foreground
 * hover:text-foreground` across both apps — card footers ("Back to sign in",
 * "Cancel") and inline back-links.
 */
export const quietLinkRecipe = cva(
  "text-sm text-muted-foreground hover:text-foreground",
);

export type QuietLinkRecipeProps = VariantProps<typeof quietLinkRecipe>;
```

```tsx
// packages/ui/src/components/quiet-link/quiet-link.tsx
import type { AnchorHTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { quietLinkRecipe } from "./quiet-link.styles";

export type QuietLinkProps = AnchorHTMLAttributes<HTMLAnchorElement>;

/** A muted secondary link — card footers and back-links. */
export function QuietLink({ className, ...rest }: QuietLinkProps): ReactElement {
  return <a className={cn(quietLinkRecipe(), className)} {...rest} />;
}
```

```ts
// packages/ui/src/components/quiet-link/index.ts
export { QuietLink, type QuietLinkProps } from "./quiet-link";
export { quietLinkRecipe, type QuietLinkRecipeProps } from "./quiet-link.styles";
```

Note the barrel rule: `src/index.ts` gets the `<name>.tsx` block only — the recipe stays
reachable through the `@bc-solutions-coder/ui/quiet-link` subpath alone.

**Step 4: Write a story**

Copy the shape of an existing simple component's `.stories.tsx`. Stories are this catalog's
render coverage, not an optional extra.

**Step 5: Wire exports, run tests, commit**

```bash
pnpm --filter @bc-solutions-coder/ui test
git add packages/ui/src
git commit -m "feat(ui): add QuietLink for muted footer and back links"
```

---

## Phase 2 — App foundations

### Task 3: `shared/lib/error-code.ts`

**Files:**
- Create: `apps/wallow-auth/src/shared/lib/error-code.ts`
- Create: `apps/wallow-auth/src/shared/lib/error-code.test.ts`
- Modify: `apps/wallow-auth/src/features/login/auth-result.ts` (re-export, drop the local copy)

Three screens carry byte-identical `readMember` copies (`RegisterForm.tsx:145`,
`MfaEnrollForm.tsx:176`, `MfaChallengeForm.tsx:218`) and `auth-result.ts:115` exports a fourth.

**Step 1: Write the failing test**

```ts
// apps/wallow-auth/src/shared/lib/error-code.test.ts
import { expect, test } from "vitest";

import { readErrorCode, readMember } from "./error-code";

test("reads a member off an object", () => {
  expect(readMember({ code: "email_taken" }, "code")).toBe("email_taken");
});

test("returns undefined for a non-object", () => {
  expect(readMember("boom", "code")).toBeUndefined();
  expect(readMember(null, "code")).toBeUndefined();
});

test("returns undefined for an absent member", () => {
  expect(readMember({}, "code")).toBeUndefined();
});

test("readErrorCode returns a string code and ignores a non-string one", () => {
  expect(readErrorCode({ code: "invalid_client_id" })).toBe("invalid_client_id");
  expect(readErrorCode({ code: 400 })).toBeUndefined();
  expect(readErrorCode(new Error("network"))).toBeUndefined();
});
```

**Step 2: Run it and watch it fail**

```bash
pnpm --filter ./apps/wallow-auth test -- error-code
```

**Step 3: Implement**

```ts
// apps/wallow-auth/src/shared/lib/error-code.ts

/**
 * Read a member off an unknown rejection without asserting its shape.
 *
 * A network-level rejection carries neither `code` nor `status`, so narrowing is
 * STRUCTURAL rather than `instanceof WallowError` — that class ships from the
 * SDK's `./server` entry, which a screen may not import at all.
 */
export function readMember(cause: unknown, name: string): unknown {
  if (typeof cause !== "object" || cause === null || !(name in cause)) {
    return undefined;
  }

  return (cause as Record<string, unknown>)[name];
}

/**
 * The API's machine token for a failure, when there is one.
 *
 * These auth endpoints answer with a bare `{ succeeded, error }` rather than RFC
 * 7807, so `splitServerError` cannot read them and each screen maps the token to
 * copy itself. The token is matched against, NEVER rendered — an unrecognised one
 * falls to that screen's generic message rather than leaking Identity's own prose.
 */
export function readErrorCode(cause: unknown): string | undefined {
  const code: unknown = readMember(cause, "code");

  return typeof code === "string" ? code : undefined;
}
```

**Step 4: Point `auth-result.ts` at it**

Delete the local `readMember` body and re-export from `@shared/lib/error-code` so existing
importers keep working.

**Step 5: Run and commit**

```bash
pnpm --filter ./apps/wallow-auth test
git add apps/wallow-auth/src
git commit -m "refactor(wallow-auth): hoist readMember into shared/lib/error-code"
```

---

### Task 4: `AuthScreen`

**Files:**
- Create: `apps/wallow-auth/src/shared/components/auth-screen.tsx`
- Create: `apps/wallow-auth/src/shared/components/auth-screen.test.tsx`

**Step 1: Write the failing test**

```tsx
// apps/wallow-auth/src/shared/components/auth-screen.test.tsx
import { render, screen } from "@bc-solutions-coder/testing/browser";
import { expect, test } from "vitest";

import { AuthScreen } from "./auth-screen";

test("renders the title as the card's h2", () => {
  render(<AuthScreen title="Two-factor authentication">body</AuthScreen>);

  expect(screen.getByRole("heading", { level: 2 }).textContent).toBe("Two-factor authentication");
});

test("renders no error banner when error is null", () => {
  render(
    <AuthScreen title="T" error={null} errorTestId="mfa-challenge-error">
      body
    </AuthScreen>,
  );

  expect(screen.queryByTestId("mfa-challenge-error")).toBeNull();
});

test("renders the error banner under the given testid when error is set", () => {
  render(
    <AuthScreen title="T" error="Invalid code" errorTestId="mfa-challenge-error">
      body
    </AuthScreen>,
  );

  expect(screen.getByTestId("mfa-challenge-error").textContent).toBe("Invalid code");
});

test("renders the footer beneath the body", () => {
  render(
    <AuthScreen title="T" footer={<a href="/login">Back to sign in</a>}>
      body
    </AuthScreen>,
  );

  expect(screen.getByRole("link", { name: "Back to sign in" })).toBeTruthy();
});
```

**Step 2: Run it and watch it fail**

```bash
pnpm --filter ./apps/wallow-auth test -- auth-screen
```

**Step 3: Implement**

Mind `jsx-max-depth: 2` — the error branch goes in its own component.

```tsx
// apps/wallow-auth/src/shared/components/auth-screen.tsx
import { Card, CardHeader, ErrorBanner } from "@bc-solutions-coder/ui";
import type { ReactElement, ReactNode } from "react";

/** The banner slot, split out to stay inside the app's `jsx-max-depth` budget. */
function ScreenError({
  error,
  testId,
}: {
  readonly error: string | null | undefined;
  readonly testId: string | undefined;
}): ReactElement | null {
  if (error === null || error === undefined) {
    return null;
  }

  return <ErrorBanner data-testid={testId}>{error}</ErrorBanner>;
}

export interface AuthScreenProps {
  readonly title: string;
  readonly description?: string;
  /** Form-level failure copy. `null`/absent renders no banner. */
  readonly error?: string | null;
  /** The banner's testid — E2E contract, so each screen names its own. */
  readonly errorTestId?: string;
  readonly footer?: ReactNode;
  readonly children: ReactNode;
  /** Overrides `Card`'s padding/rhythm block for the measured outliers. */
  readonly spacing?: string;
}

/**
 * The skeleton all 16 wallow-auth screens open with: card surface, heading,
 * optional error banner, body, optional footer.
 *
 * App-local rather than catalog: the ordering and the error slot are this app's
 * composition, not a generic surface. The generic pieces it is built FROM
 * (`CardHeader`, `QuietLink`) do live in the catalog.
 */
export function AuthScreen({
  title,
  description,
  error,
  errorTestId,
  footer,
  children,
  spacing,
}: AuthScreenProps): ReactElement {
  return (
    <Card spacing={spacing}>
      <CardHeader title={title} description={description} />
      <ScreenError error={error} testId={errorTestId} />
      {children}
      {footer}
    </Card>
  );
}
```

**Step 4: Run and commit**

```bash
pnpm --filter ./apps/wallow-auth test
git add apps/wallow-auth/src
git commit -m "feat(wallow-auth): add the AuthScreen shell"
```

---

### Task 5: `useReturnUrlGuard`

**Files:**
- Create: `apps/wallow-auth/src/shared/hooks/use-return-url-guard.ts`
- Create: `apps/wallow-auth/src/shared/hooks/use-return-url-guard.test.tsx`

Lives in `shared/hooks/`, not a feature — 7 features use it and `wallow/zone-dag` forbids
feature-to-feature imports.

Behaviour: REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`). A nullish
returnUrl is the ordinary direct path and is accepted. A PRESENT value is checked; `""` is
present and unsafe. An unsafe value navigates to `/error?reason=invalid_redirect_uri` and the
caller renders nothing.

**Step 1: Write the failing test**

Render a probe component through a memory router. Assert three cases: nullish → `"accept"` and
no navigation; safe relative → `"accept"`; unsafe absolute and `""` → `"refuse"` plus one
navigation to the error href.

**Step 2: Run it and watch it fail**

```bash
pnpm --filter ./apps/wallow-auth test -- use-return-url-guard
```

**Step 3: Implement**

```ts
// apps/wallow-auth/src/shared/hooks/use-return-url-guard.ts
import { isSafeReturnUrl } from "@bc-solutions-coder/sdk";
import { useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";

/** The bail target for a refused returnUrl, shared by every screen that guards one. */
export const ERROR_HREF = "/error?reason=invalid_redirect_uri";

export type ReturnUrlVerdict = "accept" | "refuse";

/**
 * Decide whether a screen may act on its `returnUrl`, and navigate away if not.
 *
 * REFUSE, don't sanitize: an unsafe value routes to the error page rather than
 * silently falling back to "/", which would swallow the open-redirect attempt.
 *
 * An ABSENT returnUrl is not an attack — it is the ordinary direct path — so the
 * guard runs on a present value only. `""` IS present, and `isSafeReturnUrl("")`
 * is false, so it lands on refuse rather than on the nullish no-redirect case.
 *
 * A caller renders nothing on "refuse": the navigation is in flight, and a form
 * shown meanwhile invites the user to spend a one-time factor on a destination
 * already decided against.
 */
export function useReturnUrlGuard(returnUrl: string | undefined): ReturnUrlVerdict {
  const navigate = useNavigate();
  const verdict: ReturnUrlVerdict =
    returnUrl !== undefined && !isSafeReturnUrl(returnUrl) ? "refuse" : "accept";

  useEffect(() => {
    if (verdict === "refuse") {
      void navigate({ href: ERROR_HREF });
    }
  }, [verdict, navigate]);

  return verdict;
}
```

**Step 4: Run and commit**

```bash
pnpm --filter ./apps/wallow-auth test
git add apps/wallow-auth/src
git commit -m "feat(wallow-auth): add the shared useReturnUrlGuard hook"
```

---

### Task 6: Adopt the guard in the screens that already have one

**Files (each already carries a local `ERROR_HREF`):**
- `apps/wallow-auth/src/features/register/components/RegisterForm.tsx:99`
- `apps/wallow-auth/src/features/mfa-enroll/components/MfaEnrollForm.tsx:126`
- `apps/wallow-auth/src/features/consent/components/ConsentScreen.tsx:114`
- `apps/wallow-auth/src/features/login/auth-result.ts:105`

Replace each local constant and hand-rolled check with the hook. `MfaChallengeForm` is NOT in
this task — its guard also consults an allow-list probe and is handled in Task 7.

Run the app suite plus the auth E2E suite after this one; the guard is security-relevant.

```bash
pnpm --filter ./apps/wallow-auth test
./scripts/e2e.sh
git commit -m "refactor(wallow-auth): route returnUrl guards through useReturnUrlGuard"
```

---

### Task 7: `useRedirectVerdict` (mfa-challenge)

**Files:**
- Create: `apps/wallow-auth/src/features/mfa-challenge/hooks/use-redirect-verdict.ts`
- Create: `apps/wallow-auth/src/features/mfa-challenge/hooks/use-redirect-verdict.test.tsx`
- Modify: `MfaChallengeForm.tsx` — remove `localDecisionOf`/`verdictOf` wiring and the effect

This one is feature-local: it wraps the `accountValidateRedirectUri` probe, which only this
screen runs. It returns `"accept" | "pending" | "refuse"` plus the `validation.data` the
`redirect()` hand-off needs.

**Preserve exactly:** the probe stays scoped to `scopedClientId` (unscoped, the endpoint answers
against the union of every registered client's origins); the probe is `enabled` only for
`local === "ask"`; the allow-list verdict travels WITH the returnUrl into
`allowListedReturnUrl` rather than being re-derived.

---

### Task 8: `useEnrollmentStart` (mfa-enroll)

**Files:**
- Create: `apps/wallow-auth/src/features/mfa-enroll/hooks/use-enrollment-start.ts`
- Create: `apps/wallow-auth/src/features/mfa-enroll/hooks/use-enrollment-start.test.tsx`
- Modify: `MfaEnrollForm.tsx:443-552` — remove the effect, the `startedRef` and `exchanging`

The gnarliest effect in the repo. It returns `{ secret, qrUri, loading, error, beginSetup }`.

**Preserve exactly, all four are load-bearing:**

1. **Order.** The token exchange mints the `Identity.MfaPartial` cookie; an `enroll/totp` fired
   first has no session and 401s, blaming the wrong thing.
2. **Fire-once.** `startedRef` guards it — a second `enroll/totp` mints a second secret and
   invalidates the QR the user already scanned. A ref, not state, so the effect does not re-run.
3. **`exchanging` seeded from the prop.** The first paint of a token flow must already read as
   busy; the intro branch's "Begin setup" is a retry, and offering it before the cookie exists
   invites a guaranteed 401.
4. **`setExchanging(false)` AFTER `startEnroll({})`.** The two updates batch into one render, so
   no frame sits between the exchange and the start offering a live retry button.

Write the spec for all four before touching the component.

---

## Phase 3 — The 11 form migrations

One bead, one commit, one screen. Largest first. `ResetPasswordForm.tsx` is the reference shape.

### The per-screen recipe

**Step 1: Record the testid contract, before changing anything**

```bash
grep -rn "data-testid" apps/wallow-auth/src/features/<name>/ | grep -v '\.test\.'
grep -rn "<name>-" apps/wallow-auth/e2e/
```

Write the list into the bead. This is the acceptance criterion.

**Step 2: Write the zod schema next to the component**

Field names kebab-derive to testids: `confirmPassword` → `<prefix>-confirm-password`. Where the
derived id differs from what the E2E suite fills, the field takes an explicit `testId` — as
`ResetPasswordForm` does for `reset-password-confirm`.

**Step 3: Replace the state with `useAppForm`**

- generated `{operation}Mutation({ client: sdk.client })` passed WHOLE as `mutation` — never
  destructured, never cast; `TError` is inferred from the whole object
- `toVariables` only when the operation carries a path parameter
- `onSuccess` for the navigation
- `fallbackError` for the screen's generic message

**Step 4: Replace the markup**

- the `<form onSubmit>` block → `<AppForm form={form} testIdPrefix="<prefix>">`
- each `Field`/`Label`/`Input` trio → `<form.AppField name="x">{(f) => <f.TextField label="…" />}</form.AppField>`
- the submit → `<SubmitButton pendingLabel="…">…</SubmitButton>`
- the banner → `<FormError />` for form-level failures; the screen's code→copy mapper stays and
  feeds `AuthScreen`'s `error` prop where the failure is not RFC 7807
- the card skeleton → `AuthScreen`
- the footer link → `QuietLink`

**Step 5: Move the failure mapper into the feature's `api.ts`**

Export it as a pure function taking `unknown`. Use `readErrorCode` from Task 3. Keep the
comment explaining why an unrecognised code falls to the generic tail — that reasoning is not
obvious from the code.

**Step 6: Condense the comments**

Keep divergences from the oracle, security reasoning, and non-obvious constraints. Drop
line-by-line oracle narration and anything restating what the code says.

**Step 7: Verify**

```bash
pnpm --filter ./apps/wallow-auth test
pnpm lint && pnpm lint:tests
./scripts/e2e.sh
```

**Step 8: Commit**

```bash
git commit -m "refactor(<feature>): migrate <Screen> onto the forms layer"
```

### The order

| # | Screen | Lines | Screen-specific risk |
| --- | --- | --- | --- |
| 9a | `register/RegisterForm` | 763 | Passwordless toggle hides the password block conditionally — the schema must stay valid in both branches. The strength meter is custom UI under a field, not a field. 5 guard messages fire in the oracle's own order. |
| 9b | `mfa-enroll/MfaEnrollForm` | 698 | Only the CONFIRM is a form; the start is Task 8's hook. Three-way render branch (backup codes > secret > intro) stays. Clear the code on rejection, keep the secret. |
| 9c | `mfa-challenge/MfaChallengeForm` | 574 | Two testids for one field, branching on `useBackupCode`. The toggle clears code AND error. Depends on Task 7. |
| 9d | `invitation/InvitationScreen` | 532 | — |
| 9e | `login/LoginScreen` | 439 | Three sibling forms below it; migrate the shell first. |
| 9f | `accept-terms/AcceptTermsScreen` | 350 | — |
| 9g | `wallow-web mfa/MfaSettingsSection` | 331 | Do Task 10 first — the hook extraction is the bigger win here. |
| 9h | `login/OtpLoginForm` | 305 | Two `<form>` elements in one component (request, then verify). |
| 9i | `login/MagicLinkLoginForm` | 263 | Keep its `useEffect` — it is correct. |
| 9j | `wallow-web mfa/MfaEnrollFlow` | 234 | — |
| 9k | `login/PasswordLoginForm` | 197 | Three local field wrappers collapse to `TextField`/`CheckboxField`. |

---

## Phase 4 — `wallow-web` hooks

### Task 10: `useMfaSettings`

**Files:**
- Create: `apps/wallow-web/src/features/mfa/hooks/use-mfa-settings.ts` + spec
- Modify: `apps/wallow-web/src/features/mfa/components/MfaSettingsSection.tsx:184-217`

Absorbs the status query, the disable/regenerate mutations, and all 5 `useState`
(`enrolling`, `confirmAction`, `password`, `error`, `regeneratedCodes`). The component becomes
presentational over the returned object.

### Task 11: `useUserPicker`

**Files:**
- Create: `apps/wallow-web/src/features/organizations/hooks/use-user-picker.ts` + spec
- Modify: `apps/wallow-web/src/features/organizations/components/MemberList.tsx:386-428`

Absorbs the directory query and the `requestedOpen` combobox state.

---

## Phase 5 — Documentation

### Task 12: Update the guides the work invalidates

**Files:**
- `packages/ui/CLAUDE.md` — `CardHeader` and `QuietLink`; note that the card `<h2>` is now
  guaranteed by `CardHeader` rather than only by `wallow/text-heading-variant`
- `packages/forms/CLAUDE.md` — **delete the stale claim** that app specs resolve this package
  from `dist/` and need a rebuild first. In-repo the `exports` map points at `src/`;
  `publishConfig` swaps `dist/` in at pack time. `pnpm build` still matters for
  `pnpm check:exports`.
- `apps/CLAUDE.md` — the hooks convention: feature hooks in `features/<name>/hooks/`,
  cross-feature hooks in `shared/hooks/` because `wallow/zone-dag` forbids feature-to-feature
  imports
- `docs/development/forms.md` — the migrated screen list
- `docs/plans/2026-08-03/1722-component-composition-design.md` — mark `completed`

---

## Final gate

```bash
pnpm check
./scripts/run-tests.sh
./scripts/e2e.sh
```

Then: close the beads, `git pull --rebase && bd dolt push && git push`, and confirm
`git ls-remote origin refs/dolt/data` changed.
